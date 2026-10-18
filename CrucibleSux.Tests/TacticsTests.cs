using CrucibleSux.Core;

namespace CrucibleSux.Tests;

public class TacticsTests
{
    private static readonly DateTime T0 = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Selfless and Self-preservation already gone: tanking costs nothing in score.</summary>
    private static readonly ScoreOutlook Lost = ScoreOutlook.Of(0, beastHurt: true, playerHurt: true, itemUsed: false, playerRecoveryAhead: false);

    private static readonly ScoreOutlook Intact = ScoreOutlook.Of(0, beastHurt: false, playerHurt: false, itemUsed: false, playerRecoveryAhead: false);

    private static AggroView View(double player, double beast, Actor holder, ScoreOutlook outlook, DateTime? now = null,
        bool final = false, bool recovery = false, PendingCast? cast = null, double potions = 0) =>
        new(now ?? T0, player, beast, holder, CooldownReady: true, final, recovery, outlook, cast, PotionPercent: potions);

    /// <summary>The familiar tanks for 10 s, losing 0.2% a second in small hits.</summary>
    private static void BeastTankedLightly(AggroAdvisor a, double fromHp)
    {
        for (int i = 0; i <= 40; i++)
            a.Meter.Sample(Actor.Beast, T0.AddSeconds(i * 0.25), fromHp - (i * 0.05), tanking: true);
    }

    [Fact]
    public void Safety_hands_off_even_when_score_prefers_the_current_tank()
    {
        // Final battle, Selfless intact: in score the character should tank, but at 50% it is not safe for 25% + margin.
        var d = new AggroAdvisor().Decide(View(50, 100, Actor.Player, Intact, final: true));

        Assert.Equal(AggroAction.Snarl, d.Action);
    }

    [Fact]
    public void When_neither_is_safe_the_familiar_takes_the_hits()
    {
        // The character keeps more HP after the projected loss, but its death would end the board.
        var d = new AggroAdvisor().Decide(View(60, 30, Actor.Player, Lost));

        Assert.Equal(AggroAction.Snarl, d.Action);
    }

    [Fact]
    public void Measured_damage_replaces_the_default_25_percent()
    {
        var fresh = new AggroAdvisor();
        // No data: 45 - (25 + 10) is below the familiar's floor of 15.
        Assert.Equal(AggroAction.Challenge, fresh.Decide(View(100, 45, Actor.Beast, Lost)).Action);

        var measured = new AggroAdvisor();
        BeastTankedLightly(measured, 47);
        Assert.Equal(13, measured.Meter.ProjectedLoss(Actor.Beast), 1);
        Assert.Equal(AggroAction.None, measured.Decide(View(100, 45, Actor.Beast, Lost, T0.AddSeconds(10))).Action);
    }

    [Fact]
    public void A_tankbuster_cast_hands_off_before_it_lands()
    {
        var a = new AggroAdvisor();
        BeastTankedLightly(a, 47);
        var at = T0.AddSeconds(10);
        Assert.Equal(AggroAction.None, a.Decide(View(100, 45, Actor.Beast, Lost, at)).Action);

        var d = a.Decide(View(100, 45, Actor.Beast, Lost, at, cast: new PendingCast("Crushing Blow", 3)));

        Assert.Equal(AggroAction.Challenge, d.Action);
        Assert.Contains("Crushing Blow", d.Reason);
    }

    [Fact]
    public void The_beast_tanks_once_Selfless_is_lost()
    {
        Assert.Equal(AggroAction.Snarl, new AggroAdvisor().Decide(View(100, 80, Actor.Player, Lost)).Action);
    }

    [Fact]
    public void With_Selfless_intact_the_character_tanks_while_safe()
    {
        Assert.Equal(AggroAction.Challenge, new AggroAdvisor().Decide(View(100, 100, Actor.Beast, Intact, final: true)).Action);
        // A campsite ahead heals only the character, so before the boss it tanks too.
        Assert.Equal(AggroAction.Challenge, new AggroAdvisor().Decide(View(100, 100, Actor.Beast, Intact, recovery: true)).Action);
        // Once an item has been used, potions undo the familiar's damage for free: nothing is lost either way, the tie goes to the beast.
        var ethosGone = ScoreOutlook.Of(0, beastHurt: false, playerHurt: false, itemUsed: true, playerRecoveryAhead: true);
        Assert.Equal(AggroAction.Snarl, new AggroAdvisor().Decide(View(100, 100, Actor.Player, ethosGone, recovery: true, potions: 50)).Action);
    }

    [Fact]
    public void Waits_a_window_after_a_handoff_unless_safety_forces_it()
    {
        var a = new AggroAdvisor();
        Assert.Equal(AggroAction.Snarl, a.Decide(View(100, 80, Actor.Player, Lost)).Action);

        // Score now prefers the character (Selfless intact, final battle), but the last handoff was 5 s ago.
        Assert.Equal(AggroAction.None, a.Decide(View(100, 100, Actor.Beast, Intact, T0.AddSeconds(5), final: true)).Action);
        Assert.Equal(AggroAction.Challenge, a.Decide(View(100, 100, Actor.Beast, Intact, T0.AddSeconds(16), final: true)).Action);

        // A character that is no longer safe is relieved at once.
        Assert.Equal(AggroAction.Snarl, a.Decide(View(50, 80, Actor.Player, Lost, T0.AddSeconds(17))).Action);
    }

    private static HealView Heal(double player, double beast, ScoreOutlook outlook, double? boss = 10, params HeldPotion[] potions) =>
        new(potions, player, beast, BeastLoss: 35, HandoffCanSave: true, IsFinalBattle: true, BossHp: boss, outlook);

    [Fact]
    public void Healing_spends_Economic_Ethos_only_for_a_bigger_bonus()
    {
        HeldPotion[] held = [new(0, 10), new(1, 23), new(2, 50)];

        // Selfless (1,000) is worth more than Economic Ethos (750): the fewest potions that reach 100%.
        var beast = HealAdvisor.Decide(Heal(100, 70, Intact, 10, held));
        Assert.Equal(Actor.Beast, beast?.Target);
        Assert.Equal(50, beast?.Potion.Percent);

        // Self-preservation alone (250) is not.
        Assert.Null(HealAdvisor.Decide(Heal(80, 100, Intact, 10, held)));
        // Once Economic Ethos is gone, topping up the character is free.
        Assert.NotNull(HealAdvisor.Decide(Heal(80, 100, Intact with { EconomicEthos = false }, 10, held)));
        // Not before the boss is low.
        Assert.Null(HealAdvisor.Decide(Heal(100, 70, Intact, 40, held)));
    }

    [Fact]
    public void Safety_heals_with_the_weakest_potion_that_is_enough()
    {
        var d = HealAdvisor.Decide(Heal(30, 100, Intact, null, new(0, 50), new(1, 10), new(2, 23)));

        Assert.Equal(Actor.Player, d?.Target);
        Assert.Equal(10, d?.Potion.Percent);
    }
}
