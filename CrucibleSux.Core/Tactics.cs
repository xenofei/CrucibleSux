using System.Globalization;

namespace CrucibleSux.Core;

/// <summary>Who takes the hits: the character or the familiar that is out.</summary>
public enum Actor
{
    Player,
    Beast,
}

/// <summary>
/// The damage each actor takes while it holds aggro, from HP% samples (docs/research/16-tactics.md §2): HP% lost per
/// second over the last 20 s of tanking, and the largest single drop this battle. Reset it when a battle starts, and the
/// beast's side when another beast comes out.
/// </summary>
public sealed class DamageMeter
{
    /// <summary>The time between two aggro decisions: Snarl and Challenge share a 15 s cooldown.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(15);

    /// <summary>Points added to every projected loss, so one unexpected hit does not KO the tank.</summary>
    public const double Margin = 10;

    /// <summary>The loss per window assumed until an actor has tanked long enough to measure (the owner's 25% step).</summary>
    public const double DefaultLoss = 25;

    private static readonly TimeSpan Span = TimeSpan.FromSeconds(20);

    /// <summary>Tanking time before the measured rate replaces <see cref="DefaultLoss"/>; a shorter span is too noisy.</summary>
    private static readonly TimeSpan MinSample = TimeSpan.FromSeconds(5);

    private readonly Track player = new();
    private readonly Track beast = new();

    public void Reset()
    {
        this.player.Clear();
        this.beast.Clear();
    }

    public void ResetBeast() => this.beast.Clear();

    /// <param name="tanking">The enemies were attacking this actor.</param>
    public void Sample(Actor who, DateTime at, double hpPct, bool tanking) => this.Of(who).Add(at, hpPct, tanking);

    /// <summary>HP% lost per second while tanking, over the last 20 s of tanking.</summary>
    public double Dtps(Actor who) => this.Of(who).Dtps;

    /// <summary>The largest single drop while tanking this battle.</summary>
    public double BigHit(Actor who) => this.Of(who).BigHit;

    /// <summary>The HP% this actor is expected to lose if it tanks the next window: max(Dtps × 15 s, BigHit) + margin.</summary>
    public double ProjectedLoss(Actor who)
    {
        var t = this.Of(who);
        double expected = t.Seconds >= MinSample.TotalSeconds ? t.Dtps * Window.TotalSeconds : DefaultLoss;
        return Math.Max(expected, t.BigHit) + Margin;
    }

    private Track Of(Actor who) => who == Actor.Player ? this.player : this.beast;

    private sealed class Track
    {
        private readonly Queue<(double Seconds, double Lost)> spans = new();
        private DateTime last;
        private double lastHp = double.NaN;
        private double lost;

        public double Seconds { get; private set; }

        public double BigHit { get; private set; }

        public double Dtps => this.Seconds > 0 ? this.lost / this.Seconds : 0;

        public void Add(DateTime at, double hp, bool tanking)
        {
            if (tanking && !double.IsNaN(this.lastHp))
            {
                double seconds = (at - this.last).TotalSeconds;
                // A heal is not negative damage.
                double drop = Math.Max(0, this.lastHp - hp);
                this.spans.Enqueue((seconds, drop));
                this.Seconds += seconds;
                this.lost += drop;
                this.BigHit = Math.Max(this.BigHit, drop);
                while (this.spans.Count > 1 && this.Seconds - this.spans.Peek().Seconds >= Span.TotalSeconds)
                {
                    var old = this.spans.Dequeue();
                    this.Seconds -= old.Seconds;
                    this.lost -= old.Lost;
                }
            }
            this.last = at;
            this.lastHp = hp;
        }

        public void Clear()
        {
            this.spans.Clear();
            this.lastHp = double.NaN;
            this.lost = this.Seconds = this.BigHit = 0;
        }
    }
}

/// <summary>The HP bonuses still reachable at the clear (docs/research/15-combat-findings.md §2).</summary>
/// <param name="BeastsKnockedOut">Beasts KO'd on this board so far.</param>
public sealed record ScoreOutlook(bool NoBeastLeftBehind, bool Selfless, bool SelfPreservation, bool EconomicEthos, int BeastsKnockedOut)
{
    public const int SelflessPoints = 1000;
    public const int SelfPreservationPoints = 250;
    public const int EconomicEthosPoints = 750;

    /// <summary>Willing Sacrifice III / II / I for 1 / 2 / 3 beasts KO'd; 0 otherwise.</summary>
    public int WillingSacrifice => this.BeastsKnockedOut switch { 1 => 750, 2 => 500, 3 => 250, _ => 0 };

    /// <param name="recoveryAhead">A campsite rest before the clear will bring everyone back to 100%.</param>
    public static ScoreOutlook Of(int beastsKnockedOut, bool beastHurt, bool playerHurt, bool itemUsed, bool recoveryAhead) => new(
        NoBeastLeftBehind: beastsKnockedOut == 0,
        Selfless: beastsKnockedOut == 0 && (!beastHurt || recoveryAhead),
        SelfPreservation: !playerHurt || recoveryAhead,
        EconomicEthos: !itemUsed,
        BeastsKnockedOut: beastsKnockedOut);
}

/// <summary>An enemy cast the intel aims at the highest enmity or a single target: a tankbuster on whoever tanks.</summary>
public sealed record PendingCast(string Name, double SecondsLeft);

/// <summary>What the aggro advisor reads each tick.</summary>
/// <param name="BeastHp">The familiar's HP%, or null when none is out.</param>
/// <param name="Holder">Whom the enemies are attacking, or null when nobody.</param>
/// <param name="CooldownReady">Snarl and Challenge (one shared cooldown) can be used now.</param>
/// <param name="RecoveryAhead">A campsite rest lies between this battle and the clear.</param>
public sealed record AggroView(
    DateTime Now,
    double PlayerHp,
    double? BeastHp,
    Actor? Holder,
    bool CooldownReady,
    bool IsFinalBattle,
    bool RecoveryAhead,
    ScoreOutlook Outlook,
    PendingCast? Cast = null,
    double PlayerFloor = AggroAdvisor.DefaultPlayerFloor,
    double BeastFloor = AggroAdvisor.DefaultBeastFloor);

public enum AggroAction
{
    None,

    /// <summary>The familiar takes aggro and covers the character.</summary>
    Snarl,

    /// <summary>The character takes aggro back.</summary>
    Challenge,
}

public sealed record AggroDecision(AggroAction Action, string Reason)
{
    public static AggroDecision None(string reason) => new(AggroAction.None, reason);
}

/// <summary>
/// Decides who tanks the next window (docs/research/16-tactics.md §3): safety first, then the cheaper actor in score,
/// ties to the beast. After a handoff it waits a window before handing off again, unless safety forces it.
/// </summary>
public sealed class AggroAdvisor
{
    /// <summary>The character's death expels you from the board, so its floor is higher.</summary>
    public const double DefaultPlayerFloor = 35;

    public const double DefaultBeastFloor = 15;

    private DateTime lastHandoff = DateTime.MinValue;

    public DamageMeter Meter { get; } = new();

    /// <summary>A new battle: forget its damage and the last handoff.</summary>
    public void Reset()
    {
        this.Meter.Reset();
        this.lastHandoff = DateTime.MinValue;
    }

    public AggroDecision Decide(AggroView v)
    {
        if (v.BeastHp is not { } beastHp)
            return AggroDecision.None("no familiar out");
        if (v.Holder is not { } holder)
            return AggroDecision.None("no enemy is attacking");
        if (!v.CooldownReady)
            return AggroDecision.None("cooldown");

        double Hp(Actor a) => a == Actor.Player ? v.PlayerHp : beastHp;
        double Floor(Actor a) => a == Actor.Player ? v.PlayerFloor : v.BeastFloor;
        // HP above the floor once the projected loss is taken; negative means unsafe.
        double Headroom(Actor a, double loss) => Hp(a) - loss - Floor(a);
        var other = holder == Actor.Player ? Actor.Beast : Actor.Player;

        // A tankbuster is assumed to hit at least as hard as the no-data default; a bigger measured hit raises it.
        if (v.Cast is { } cast)
        {
            double hit = Math.Max(this.Meter.BigHit(holder), DamageMeter.DefaultLoss) + DamageMeter.Margin;
            if (Headroom(holder, hit) < 0 && Headroom(other, hit) > Headroom(holder, hit))
                return this.HandTo(other, v.Now, Inv($"{cast.Name} lands in {cast.SecondsLeft:0.0}s; {Name(holder)} at {Hp(holder):0}% would fall below its floor"));
        }

        double lossP = this.Meter.ProjectedLoss(Actor.Player);
        double lossB = this.Meter.ProjectedLoss(Actor.Beast);
        bool safeP = Headroom(Actor.Player, lossP) >= 0;
        bool safeB = Headroom(Actor.Beast, lossB) >= 0;
        string hp = Inv($"you {v.PlayerHp:0}% (-{lossP:0}), familiar {beastHp:0}% (-{lossB:0})");

        Actor want;
        string why;
        bool forced = true;
        if (safeP != safeB)
        {
            want = safeP ? Actor.Player : Actor.Beast;
            why = $"only {Name(want)} is safe for the next window";
        }
        else if (!safeP)
        {
            // The character's death loses the board; a KO'd beast costs about 250 (No Beast Left Behind for Willing Sacrifice III).
            want = Actor.Beast;
            why = "neither is safe; the familiar takes it, since your death ends the board";
        }
        else
        {
            // Letting a safe actor take the window costs only the bonus its lost HP forfeits; a safe tank is not KO'd.
            int costB = v.Outlook.Selfless && !v.RecoveryAhead ? ScoreOutlook.SelflessPoints : 0;
            int costP = v.Outlook.SelfPreservation && !v.RecoveryAhead ? ScoreOutlook.SelfPreservationPoints : 0;
            want = costP < costB ? Actor.Player : Actor.Beast;
            why = Inv($"both safe; tanking costs you {costP}, the familiar {costB}");
            forced = false;
        }

        if (want == holder)
            return AggroDecision.None($"{Name(want)} already tanks: {why}");
        if (!forced && v.Now - this.lastHandoff < DamageMeter.Window)
            return AggroDecision.None($"holding for one window after the last handoff: {why}");
        return this.HandTo(want, v.Now, $"{why}; {hp}");
    }

    private AggroDecision HandTo(Actor to, DateTime now, string reason)
    {
        this.lastHandoff = now;
        return new(to == Actor.Beast ? AggroAction.Snarl : AggroAction.Challenge, reason);
    }

    private static string Name(Actor a) => a == Actor.Player ? "you" : "the familiar";

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}

/// <summary>A healing potion held for this board: its inventory slot and the HP% it restores (10/23/36/50).</summary>
public sealed record HeldPotion(int Slot, int Percent);

/// <summary>What the heal advisor reads.</summary>
/// <param name="BeastLoss">The familiar's projected loss for the next window (<see cref="DamageMeter.ProjectedLoss"/>).</param>
/// <param name="HandoffCanSave">A Snarl or Challenge this window would move the hits to an actor that is safe.</param>
/// <param name="BossHp">The final battle's main enemy's HP%, or null.</param>
public sealed record HealView(
    IReadOnlyList<HeldPotion> Potions,
    double PlayerHp,
    double? BeastHp,
    double BeastLoss,
    bool HandoffCanSave,
    bool IsFinalBattle,
    double? BossHp,
    ScoreOutlook Outlook,
    double PlayerFloor = AggroAdvisor.DefaultPlayerFloor,
    double BeastFloor = AggroAdvisor.DefaultBeastFloor);

public sealed record HealDecision(HeldPotion Potion, Actor Target, string Reason);

/// <summary>
/// When to drink a potion (docs/research/16-tactics.md §4): for safety, or in the final battle once the boss is low, to
/// secure Selfless or Self-preservation when they are worth more than the Economic Ethos a potion forfeits.
/// </summary>
public static class HealAdvisor
{
    /// <summary>Boss HP% below which the final battle's top-up starts.</summary>
    public const double BossLow = 15;

    /// <summary>The Beast Potions by XBMItem row, and the HP% each restores to you or a familiar.</summary>
    public static readonly IReadOnlyDictionary<uint, int> Potions = new Dictionary<uint, int> { [76] = 10, [77] = 23, [78] = 36, [79] = 50 };

    public static HealDecision? Decide(HealView v)
    {
        if (v.Potions.Count == 0)
            return null;

        // Safety: any potion, the weakest that is enough first.
        if (v.PlayerHp < v.PlayerFloor)
            return new(Weakest(v.Potions, v.PlayerFloor - v.PlayerHp), Actor.Player, "you are below your floor");
        if (v.BeastHp is { } b && b - v.BeastLoss <= 0 && !v.HandoffCanSave)
            return new(Weakest(v.Potions, v.BeastLoss + v.BeastFloor - b), Actor.Beast, "the next window would KO the familiar");

        if (!v.IsFinalBattle || v.BossHp is not { } boss || boss >= BossLow)
            return null;

        // Securing bonuses: the familiar first (Selfless is worth more), then the character from what is left.
        var left = v.Potions.ToList();
        List<HeldPotion>? beastSet = v.BeastHp is { } bh && bh < 100 && v.Outlook.NoBeastLeftBehind ? SmallestSet(left, 100 - bh) : null;
        if (beastSet is not null)
            left.RemoveAll(beastSet.Contains);
        List<HeldPotion>? playerSet = v.PlayerHp < 100 ? SmallestSet(left, 100 - v.PlayerHp) : null;

        int worth = (beastSet is null ? 0 : ScoreOutlook.SelflessPoints) + (playerSet is null ? 0 : ScoreOutlook.SelfPreservationPoints);
        int cost = v.Outlook.EconomicEthos ? ScoreOutlook.EconomicEthosPoints : 0;
        if (worth <= cost)
            return null;
        string reason = $"securing {worth} points for {cost}";
        return beastSet is not null ? new(beastSet[0], Actor.Beast, reason) : new(playerSet![0], Actor.Player, reason);
    }

    private static HeldPotion Weakest(IReadOnlyList<HeldPotion> potions, double need) =>
        potions.Where(p => p.Percent >= need).MinBy(p => p.Percent) ?? potions.MaxBy(p => p.Percent)!;

    /// <summary>The fewest potions that restore at least <paramref name="need"/>, wasting the least; null when they cannot.</summary>
    private static List<HeldPotion>? SmallestSet(List<HeldPotion> potions, double need)
    {
        List<HeldPotion>? best = null;
        int n = Math.Min(potions.Count, 16);
        for (int mask = 1; mask < 1 << n; mask++)
        {
            var set = Enumerable.Range(0, n).Where(i => (mask & (1 << i)) != 0).Select(i => potions[i]).ToList();
            if (set.Sum(p => p.Percent) < need)
                continue;
            if (best is null || set.Count < best.Count || (set.Count == best.Count && set.Sum(p => p.Percent) < best.Sum(p => p.Percent)))
                best = set;
        }
        return best?.OrderByDescending(p => p.Percent).ToList();
    }
}
