using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CrucibleSux.Core;
using CrucibleSux.Game;
using CrucibleSux.Integrations;
using Dalamud.Game.ClientState.Fates;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FateState = Dalamud.Game.ClientState.Fates.FateState;
using Mode = CrucibleSux.Integrations.RotationSolverIpc.Mode;

namespace CrucibleSux.Automation;

/// <summary>
/// The Beastmaster journey (docs/PLAN.md Phase 2b): asks the <see cref="ProgressionPlanner"/> for the next activity four
/// times a second and carries it out with the game calls in <see cref="GameActions"/>, vnavmesh for walking, Rotation Solver
/// Reborn for fighting and the session runner for boards. Progress is always read back from the game (quest sequence, beasts
/// owned, EXP, inventory), never assumed from what was sent. None of it has run in game yet.
/// </summary>
public sealed unsafe class ProgressionRunner : IDisposable
{
    public const uint BeastmasterWeapon = 47470;
    public const uint StarterCoffer = 52649;

    private static readonly TimeSpan TickEvery = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan StuckAfter = TimeSpan.FromMinutes(15);

    /// <summary>Shorter than <see cref="StuckAfter"/>, so a hunt that finds nothing skips its beast before the run counts as stuck.</summary>
    private static readonly TimeSpan HuntFor = TimeSpan.FromMinutes(12);
    private static readonly TimeSpan SearchPointFor = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan IdleZoneFor = TimeSpan.FromSeconds(60);

    /// <summary>A FATE whose progress has not moved for this long while the character is in it is left (a boss it cannot solo).</summary>
    private static readonly TimeSpan FateStallsAfter = TimeSpan.FromMinutes(3);

    /// <summary>How close to a FATE's centre the character goes when it sees none of the FATE's enemies.</summary>
    private const float FateCentre = 8f;

    /// <summary>The height difference from a FATE's centre beyond which the character is above or below it, not in it.</summary>
    private const float FateHeight = 10f;

    private static readonly TimeSpan FateReachFor = TimeSpan.FromMinutes(2);

    /// <summary>Open-world mobs fought while no FATE can be joined: this far, and from this many levels under the character.</summary>
    private const float GrindRange = 100f;
    private const int GrindLevelsBelow = 4;

    /// <summary>How far back a flight runs: mobs give up a chase once the character is far enough from where they started.</summary>
    private const float FleeBack = 40f;
    private const int Crumbs = 30;
    private static readonly TimeSpan FleeStuckAfter = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CrumbEvery = TimeSpan.FromSeconds(2);

    /// <summary>How long a zone left for having no FATEs is not chosen again for its measured rate.</summary>
    private static readonly TimeSpan ZoneCooldown = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan NobodyThereFor = TimeSpan.FromSeconds(30);
    private const int MaxFailures = 3;
    private const float TalkRange = 3f;
    private const float MeleeRange = 3f;

    /// <summary>A chase keeps its path while the target stays this close to where the path ends.</summary>
    private const float ChaseRepathBeyond = 4f;

    /// <summary>Where to look around a spawn point, in yalms from it: mobs roam, and only nearby ones are loaded.</summary>
    private static readonly Vector2[] SearchPattern = [new(0, 0), new(40, 0), new(0, 40), new(-40, 0), new(0, -40)];

    private readonly ProgressionReader reader;
    private readonly GameActions game;
    private readonly Travel travel;
    private readonly Dialogue dialogue;
    private readonly NavmeshIpc nav;
    private readonly RotationSolverIpc rotation;
    private readonly SessionRunner boards;
    private readonly Configuration config;
    private readonly IDalamudPluginInterface pi;
    private readonly IClientState client;
    private readonly IFateTable fates;
    private readonly IObjectTable objects;
    private readonly IDataManager data;
    private readonly IChatGui chat;
    private readonly INotificationManager notifications;
    private readonly ExpMeter meter = new();
    private readonly HashSet<uint> skipped = [];

    private DateTime nextTick;
    private DateTime nextAct;
    private DateTime nextChase;
    private DateTime lastProgress;
    private string? key;
    private string fingerprint = string.Empty;
    private int failures;
    private bool notifiedWait;
    private int gearSetup;
    private int gearTries;
    private int jobTries;

    /// <summary>When unlock-only mode saw the quest complete; its gear setup must finish within <see cref="UnlockSetupLimit"/>.</summary>
    private DateTime? unlockedAt;

    private static readonly TimeSpan UnlockSetupLimit = TimeSpan.FromMinutes(3);

    /// <summary>The board of the session the journey started and still owns, or 0; survives <see cref="Reset"/>.</summary>
    private int boardRun;

    // Per activity; cleared by Reset.
    private int stepIndex;
    private int visits;
    private bool sawEvent;
    private bool borrowed;
    private bool escortArrived;
    private DateTime? since;
    private uint hunt;
    private DateTime huntStarted;
    private int searchPoint;
    private ulong prey;
    private ushort fate;
    private uint buyItem;
    private int buyBefore;
    private int buyTries;

    // Levelling zone rotation; kept across activities.
    private uint zone;
    private List<uint> bandZones = [];
    private readonly HashSet<uint> unreachable = [];
    private readonly HashSet<ushort> stalled = [];
    private (ushort Fate, int Progress, DateTime At) fateWatch;
    private (ushort Fate, DateTime Since) headingTo;
    private uint leftIdle;
    private DateTime leftIdleAt;

    // Fights outside the Crucible; kept across activities.
    private readonly DamageMeter hpMeter = new();
    private readonly DangerMap danger = new();
    private readonly Queue<(DateTime At, Vector3 Pos)> crumbs = new();
    private bool wasInCombat;
    private DateTime? fleeingSince;
    private Vector3? fleeTo;
    private bool wasDead;

    /// <summary>Got away from a fight: rests until HP is full before carrying on (the owner's rule).</summary>
    private bool recovering;

    /// <summary>The enemies a flight ran from: not fought again as mobs while levelling.</summary>
    private readonly HashSet<ulong> fledFrom = [];

    private (DateTime At, Vector3 Pos) fleeCheck;
    private int fleeTurns;
    private (uint Status, uint Action) lastRefusal;
    private (CaptureMove Move, bool InReach) captureStep;
    private int attackTries;
    private ulong grindTarget;

    public ProgressionRunner(
        ProgressionReader reader,
        GameActions game,
        Travel travel,
        Dialogue dialogue,
        NavmeshIpc nav,
        RotationSolverIpc rotation,
        SessionRunner boards,
        Configuration config,
        IDalamudPluginInterface pi,
        IClientState client,
        IFateTable fates,
        IObjectTable objects,
        IDataManager data,
        IChatGui chat,
        INotificationManager notifications)
    {
        this.reader = reader;
        this.game = game;
        this.travel = travel;
        this.dialogue = dialogue;
        this.nav = nav;
        this.rotation = rotation;
        this.boards = boards;
        this.config = config;
        this.pi = pi;
        this.client = client;
        this.fates = fates;
        this.objects = objects;
        this.data = data;
        this.chat = chat;
        this.notifications = notifications;
        this.client.Logout += this.OnLogout;
    }

    public bool Running { get; private set; }

    /// <summary>What the runner is doing, for the Journey tab.</summary>
    public string Status { get; private set; } = "Not running.";

    /// <summary>Something only the player can do (assign a horn, answer a window); the run carries on once it is done.</summary>
    public string? Waiting { get; private set; }

    public Activity? Activity { get; private set; }

    /// <summary>Why the last run ended.</summary>
    public string? LastResult { get; private set; }

    /// <summary>Only "Strangers in the Wood" and the job's gear setup, then stop (the unlock offer).</summary>
    private JourneyGoal goal;

    private ProgressionOptions Options => this.goal switch
    {
        JourneyGoal.Unlock => new(Quests: true, Beasts: false, Leveling: false),
        // The quests' own beasts are collected without the beasts part; their levels need levelling.
        JourneyGoal.Crucible => new(Quests: true, Beasts: false, Leveling: true),
        _ => new(this.config.ProgressQuests, this.config.ProgressBeasts, this.config.ProgressLeveling),
    };

    public void Dispose()
    {
        this.client.Logout -= this.OnLogout;
        this.Stop("Crucible SUX was unloaded.");
    }

    /// <summary>
    /// What stops the journey from starting; empty when it can start. <paramref name="beforeSwitch"/> ignores the BossMod the
    /// player does not use, which <see cref="BossModGate"/> turns off before the start.
    /// </summary>
    /// <param name="goal">The goal to check a start for; only <see cref="JourneyGoal.Parts"/> needs parts turned on.</param>
    public IReadOnlyList<string> Checks(bool beforeSwitch = false, JourneyGoal goal = JourneyGoal.Parts)
    {
        var problems = new List<string>();
        if (this.game.Me is null)
            problems.Add("Log in to a character.");
        if (goal == JourneyGoal.Parts && !this.Options.Any)
            problems.Add("Turn on at least one part of the journey.");
        if (!this.Loaded("vnavmesh"))
            problems.Add("vnavmesh must be installed and enabled: it does the walking.");
        if (!this.Loaded("RotationSolver"))
            problems.Add("Rotation Solver Reborn must be installed and enabled: it does the fighting.");
        if (goal == JourneyGoal.Unlock && this.game.Me is not null && !this.CanUnlock())
            problems.Add("This character cannot take \"Strangers in the Wood\" now, or has finished it.");
        if (goal == JourneyGoal.Crucible && this.game.Me is not null && !this.CanUnlock() && !this.CanReachCrucible())
            problems.Add("This character cannot unlock Beastmaster now, or the First Board is already open.");
        string unused = BossModSwitch.OffFor(this.config.MechanicsPlugin);
        // Neither goal runs a board before it stops, so only the parts need the BossMod you do not use off.
        if (!beforeSwitch && goal == JourneyGoal.Parts && this.Loaded(unused))
            problems.Add($"Only the BossMod you use may be loaded, and {BossModSwitch.Name(unused)} is too. Disable it, or start from Crucible SUX so it can turn it off for the run.");
        if (this.boards.Current is not null)
            problems.Add("A board session is running; stop it first.");
        return problems;
    }

    /// <summary>What the journey would do next for the character as it stands (for the tab, while not running).</summary>
    public Activity? Preview()
    {
        var state = this.reader.Capture(out bool beastsUnknown);
        if (state is null)
            return null;
        if (beastsUnknown)
            return new(ActivityKind.Blocked, "Open the Master's Bestiary once so the game sends your beast list.");
        return this.Plan(state);
    }

    /// <summary>Whether the character can unlock Beastmaster and has not yet (<see cref="JourneyRules.CanOfferUnlock"/>).</summary>
    public bool CanUnlock() => this.reader.Capture(out _) is { } state && JourneyRules.CanOfferUnlock(state);

    /// <summary>Whether Beastmaster is unlocked and the First Board not yet open (<see cref="JourneyRules.CanOfferCrucible"/>).</summary>
    public bool CanReachCrucible() => this.reader.Capture(out _) is { } state && JourneyRules.CanOfferCrucible(state);

    /// <param name="goal">What to do whatever the parts say: unlock the job, or go on to the Crucible; <see cref="JourneyGoal.Parts"/> follows them.</param>
    public bool Start(JourneyGoal goal = JourneyGoal.Parts)
    {
        if (this.Running || this.Checks(goal: goal).Count > 0)
            return false;
        this.goal = goal;
        this.unlockedAt = null;
        this.gearSetup = this.gearTries = this.jobTries = 0;
        this.Running = true;
        this.failures = 0;
        this.skipped.Clear();
        this.key = null;
        this.boardRun = 0;
        this.rotation.ForceOff();
        this.lastProgress = DateTime.UtcNow;
        this.LastResult = null;
        this.Status = "Starting.";
        Plugin.Log.Information($"Journey started: {this.Options}");
        this.chat.Print("[Crucible SUX] Beastmaster journey started. \"/sux stop\" stops it.");
        return true;
    }

    public void Stop(string reason, bool done = false)
    {
        if (!this.Running)
            return;
        this.Running = false;
        this.goal = JourneyGoal.Parts;
        this.Reset();
        if (this.boardRun != 0 && this.boards.Current is not null)
            this.boards.Stop(reason);
        this.boardRun = 0;
        this.rotation.ForceOff();
        this.Status = reason;
        this.LastResult = reason;
        this.Waiting = null;
        this.Activity = null;
        Plugin.Log.Information($"Journey stopped: {reason}");
        this.chat.Print($"[Crucible SUX] Journey {(done ? "finished" : "stopped")}: {reason}");
        this.notifications.AddNotification(new Notification
        {
            Title = done ? "Crucible SUX: journey finished" : "Crucible SUX: journey stopped",
            Content = reason,
            Type = done ? NotificationType.Success : NotificationType.Warning,
            InitialDuration = TimeSpan.FromHours(12),
        });
    }

    /// <summary>Called every framework tick; does its work four times a second.</summary>
    public void Update()
    {
        if (!this.Running)
            return;
        var now = DateTime.UtcNow;
        if (now < this.nextTick)
            return;
        this.nextTick = now + TickEvery;
        try
        {
            this.Tick(now);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "The journey failed");
            this.Stop($"Stopped after an error: {ex.Message}");
        }
    }

    private void Tick(DateTime now)
    {
        this.Waiting = null;
        if (this.boardRun != 0 && this.BoardTick(now))
            return;
        var state = this.reader.Capture(out bool beastsUnknown);
        if (state is null)
        {
            this.Stop("No character is loaded.");
            return;
        }

        // KO'd: accept the return to the home point; once back on its feet, the run counts a problem and goes on.
        if (this.game.Me is ICharacter { IsDead: true })
        {
            this.wasDead = true;
            this.fleeingSince = null;
            this.rotation.Release();
            this.dialogue.Tick(new(Yes: "home point"));
            this.Wait(now, "The character was KO'd: returning to the home point.");
            return;
        }
        if (this.wasDead)
        {
            this.wasDead = false;
            this.Fail(now, "The character was KO'd and returned to its home point.");
            return;
        }
        this.Watch(now);

        if (this.goal == JourneyGoal.Crucible && state.IsComplete(CrucibleIds.Quests.IntoTheCrucible))
        {
            this.Stop("The First Board is open: queue it in the Run tab, or turn on more of the journey.", done: true);
            return;
        }
        if (this.goal == JourneyGoal.Unlock && state.IsComplete(CrucibleIds.Quests.StrangersInTheWood))
        {
            // The job setup needs no beast list; it waits for the quest's last window and event to close.
            this.unlockedAt ??= now;
            if (now - this.unlockedAt > UnlockSetupLimit)
                this.Stop($"Beastmaster is unlocked, but its gear setup did not finish within {UnlockSetupLimit.TotalMinutes:0} minutes: change to Beastmaster and set its gear up by hand.");
            else if (this.dialogue.Tick(new(Quest: this.reader.QuestName(CrucibleIds.Quests.StrangersInTheWood))))
                this.Waiting = this.dialogue.Waiting;
            else if (!this.game.InEvent && !this.game.IsBusy && this.EnsureJob(now))
                this.Stop("Beastmaster is unlocked and its gear is set up.", done: true);
            return;
        }
        if (beastsUnknown)
        {
            this.Wait(now, "Open the Master's Bestiary once so the game sends your beast list.");
            return;
        }

        var activity = this.Plan(state);
        string key = Key(activity);
        if (key != this.key)
        {
            if (this.key is not null)
                this.failures = 0;
            this.Reset();
            this.key = key;
            this.Activity = activity;
            this.lastProgress = now;
            Plugin.Log.Information($"Journey: {activity.Kind}: {activity.Why}");
        }

        switch (activity.Kind)
        {
            case ActivityKind.Done:
                this.Stop(activity.Why, done: true);
                return;
            case ActivityKind.Blocked:
                this.Stop(activity.Why);
                return;
        }

        // Only what the game shows counts as progress; waiting on the player (see Wait) also keeps the clock fresh. The check
        // comes before the windows and busy states below, so a dialogue or cutscene that never ends is caught too.
        // While levelling, a zone change is not progress (a dry band would rotate forever); the FATE's progress is.
        bool levelling = activity.Kind == ActivityKind.Level;
        string fateKey = levelling && this.Fate(this.fate) is { } watched ? $"{key}|{watched.FateId}:{watched.Progress}" : key;
        string fp = JourneyRules.Progress(fateKey, state, this.reader.Experience(), levelling ? 0 : this.client.TerritoryType);
        if (fp != this.fingerprint)
        {
            this.fingerprint = fp;
            this.lastProgress = now;
        }
        if (now - this.lastProgress > StuckAfter)
        {
            this.Fail(now, $"Nothing changed for {StuckAfter.TotalMinutes} minutes ({activity.Why})");
            return;
        }

        // A striking dummy's fight lingers after its quest step, and blocks job changes, summons, teleports and talks.
        bool dummyStep = activity is { Kind: ActivityKind.Quest, Step.Kind: StepKind.FightDummy or StepKind.TemperedRelease or StepKind.Borrow };
        if (!dummyStep && this.LeaveDummyCombat())
            return;
        if (this.Recover())
            return;

        // A board run belongs to AutoDuty, including its windows.
        if (activity.Kind != ActivityKind.RunBoard && this.dialogue.Tick(this.Expect(activity, state)))
        {
            if (this.dialogue.Waiting is { } w)
                this.Wait(now, w);
        }
        else if (!this.game.IsBusy)
        {
            switch (activity.Kind)
            {
                case ActivityKind.Quest:
                    this.DoQuest(now, activity, state);
                    break;
                case ActivityKind.Level:
                    this.DoLevel(now, state);
                    break;
                case ActivityKind.Capture:
                    this.DoCapture(now, activity, state);
                    break;
                case ActivityKind.BuyGourd when !this.FightBack(now):
                    this.DoBuy(now, activity, state);
                    break;
                case ActivityKind.UseGourd when !this.FightBack(now):
                    this.DoUseGourd(now, activity);
                    break;
                case ActivityKind.RunBoard:
                    this.DoBoard(now, activity);
                    break;
            }
        }

        if (this.Waiting is null)
            this.notifiedWait = false;
    }

    /// <summary>
    /// While a board the journey started runs, the session runner owns the game, whatever the plan would say meanwhile. Once
    /// it ends, a clear lets the plan move on, a stop by the player stops the journey, and anything else is a problem.
    /// Returns true while the tick belongs to the board.
    /// </summary>
    private bool BoardTick(DateTime now)
    {
        var board = Boards.ByNumber(this.boardRun);
        if (this.boards.Current is { } o)
        {
            // The session runner has its own watchdog inside the board.
            this.Say($"{board.Name} through AutoDuty: {o.Status}");
            this.lastProgress = now;
            return true;
        }

        this.boardRun = 0;
        // AutoDuty drove the rotation plugin during the board, so what this side last sent no longer holds.
        this.rotation.ForceOff();
        var report = this.boards.LastReport;
        string ending = report is null ? "." : $": {report.Ending}";
        switch (JourneyRules.Classify(report))
        {
            case BoardEnd.Cleared:
                this.failures = 0;
                this.lastProgress = now;
                return false;
            case BoardEnd.Stopped:
                this.Stop($"The {board.Name} session was stopped{ending}");
                return true;
            default:
                this.Fail(now, $"The {board.Name} run ended without a clear{ending}");
                return true;
        }
    }

    /// <summary>The planner's activity, minus the beasts this run has given up on.</summary>
    private Activity Plan(ProgressionState state)
    {
        var a = ProgressionPlanner.Next(state, this.Options);
        if (a.Kind != ActivityKind.Capture || this.skipped.Count == 0)
            return a;
        var left = a.Beasts!.Where(b => !this.skipped.Contains(b)).ToList();
        if (left.Count > 0)
            return a with { Beasts = left };
        string names = string.Join(", ", a.Beasts!.Select(b => BeastSources.ById(b).Name));
        return new(ActivityKind.Blocked, $"{a.Why} No {names} could be captured this run; try again later or capture one by hand.");
    }

    private static string Key(Activity a) =>
        $"{a.Kind}|{a.Quest}|{a.Step?.Sequence}|{a.Level}|{a.Item}|{a.Board}|{string.Join(",", a.Beasts ?? [])}";

    private DialogueExpect Expect(Activity a, ProgressionState s) => a.Kind switch
    {
        ActivityKind.Quest => new(Quest: this.reader.QuestName(a.Quest), Choice: a.Step?.Choice),
        ActivityKind.UseGourd => new(Yes: this.reader.ItemName(a.Item)),
        ActivityKind.BuyGourd => new(Choice: KornagoShop.For(Purchase(a, s).Currency).Name),
        _ => new(),
    };

    /// <summary>What to buy next for a gourd: the gourd, or first the Bright Remnants it is short of (2 Faded each).</summary>
    private static (uint Item, int Quantity, uint Currency) Purchase(Activity a, ProgressionState s)
    {
        var src = BeastSources.All.First(b => b.Gourd == a.Item);
        int bright = s.Count(CrucibleIds.BrightRemnant);
        return src.Currency == CrucibleIds.BrightRemnant && bright < src.Price
            ? (CrucibleIds.BrightRemnant, src.Price - bright, CrucibleIds.FadedRemnant)
            : (src.Gourd, 1, src.Currency);
    }

    /// <summary>Forgets the current activity's progress and lets go of movement and the rotation.</summary>
    private void Reset()
    {
        this.travel.Reset();
        // Chasing moves the player through vnavmesh directly, outside the trip Travel knows about.
        this.nav.Stop();
        this.rotation.Release();
        var shop = this.game.Addon("ShopExchangeItem");
        if (shop != null)
            shop->Close(true);
        this.stepIndex = 0;
        this.visits = 0;
        this.sawEvent = false;
        this.borrowed = false;
        this.escortArrived = false;
        this.since = null;
        this.hunt = 0;
        this.searchPoint = 0;
        this.prey = 0;
        this.fate = 0;
        this.buyItem = 0;
        this.buyTries = 0;
    }

    private void Say(string status) => this.Status = status;

    private void Wait(DateTime now, string what)
    {
        this.Waiting = what;
        this.Status = what;
        this.lastProgress = now;
        if (this.notifiedWait)
            return;
        this.notifiedWait = true;
        this.chat.Print($"[Crucible SUX] Waiting for you: {what}");
        this.notifications.AddNotification(new Notification { Title = "Crucible SUX needs you", Content = what, Type = NotificationType.Info });
    }

    private void Fail(DateTime now, string reason)
    {
        this.failures++;
        Plugin.Log.Warning($"Journey problem {this.failures}/{MaxFailures}: {reason}");
        if (this.hunt != 0)
            this.skipped.Add(this.hunt);
        if (this.failures >= MaxFailures)
        {
            this.Stop($"{reason} ({MaxFailures} problems in a row).");
            return;
        }
        this.chat.Print($"[Crucible SUX] {reason} Trying again.");
        this.Reset();
        this.lastProgress = now;
    }

    /// <summary>Throttles game calls that need time to take effect.</summary>
    private bool Ready(DateTime now, double seconds)
    {
        if (now < this.nextAct)
            return false;
        this.nextAct = now + TimeSpan.FromSeconds(seconds);
        return true;
    }

    private bool Loaded(string internalName) =>
        this.pi.InstalledPlugins.Any(p => p.IsLoaded && p.InternalName.Equals(internalName, StringComparison.OrdinalIgnoreCase));

    // --- Job ------------------------------------------------------------------------------------------------------

    /// <summary>
    /// True once the character is a Beastmaster with its gear on. The first time (after the unlock quest) it does what the
    /// game's own tutorial does: equip the axe, save a gear set, open the starter coffer, equip the recommended gear and
    /// save the set again (docs/research/10-quest-engine-apis.md §5).
    /// </summary>
    private bool EnsureJob(DateTime now)
    {
        if (this.game.OnBeastmaster)
            return this.FinishGearSetup(now);
        if (this.game.InCombat || !this.Ready(now, 3))
            return false;
        this.Say("Changing to Beastmaster.");
        if (this.game.Mounted)
        {
            this.game.UseGeneral(GameActions.Dismount);
            return false;
        }
        if (this.game.EquipBeastmasterGearset())
            return false;
        if (this.game.EquipMainHand(BeastmasterWeapon))
        {
            this.gearSetup = 1;
            this.gearTries = 0;
            return false;
        }
        // The game can refuse the move for a moment (an event closing, the job change still landing).
        if (++this.jobTries <= 10)
            return false;
        this.Stop("Beastmaster has no gear set, and its weapon is not in your inventory or armoury.");
        return false;
    }

    private bool FinishGearSetup(DateTime now)
    {
        if (this.gearSetup == 0)
            return true;
        if (!this.Ready(now, 2))
            return false;
        this.Say("Setting up Beastmaster's gear.");
        switch (this.gearSetup)
        {
            case 1:
                this.game.CreateGearset();
                this.gearSetup = 2;
                break;
            case 2:
                if (this.game.ItemCount(StarterCoffer) > 0 && ++this.gearTries <= 5)
                    this.game.UseItem(StarterCoffer);
                else
                    (this.gearSetup, this.gearTries) = (3, 0);
                break;
            case 3:
                if (this.game.EquipRecommended() || ++this.gearTries > 20)
                    this.gearSetup = 4;
                break;
            default:
                if (!this.game.UpdateBeastmasterGearset())
                    Plugin.Log.Warning("Journey: no Beastmaster gear set to save the recommended gear into.");
                this.gearSetup = 0;
                return true;
        }
        return false;
    }

    // --- Quests ---------------------------------------------------------------------------------------------------

    private void DoQuest(DateTime now, Activity a, ProgressionState s)
    {
        var q = QuestScripts.ById(a.Quest);
        if (!q.AnyJob && !this.EnsureJob(now))
            return;
        if (a.Step is not { } first)
        {
            this.Say($"Accepting {q.Name}.");
            this.Visit(now, q.Giver);
            return;
        }

        var steps = q.At(first.Sequence).ToList();
        var step = steps[this.stepIndex % steps.Count];
        this.Say($"{q.Name}: {Describe(step)}");
        if (step.Need?.Summoned is uint b and not 0 && !this.Summon(now, b))
            return;

        bool done = step.Kind switch
        {
            StepKind.Talk or StepKind.Interact => this.Visit(now, step.At),
            StepKind.UseEventItem => this.Visit(now, step.At, useItem: step.Arg),
            StepKind.Emote => this.Visit(now, step.At, emote: (ushort)step.Arg),
            StepKind.FightDummy => this.FightDummy(now, step.At),
            StepKind.TemperedRelease => this.Summon(now, step.Arg) && this.OnDummy(now, step.At, GameActions.TemperedRelease),
            StepKind.Borrow => this.Borrow(now, step, s),
            StepKind.Battle => this.Battle(now, step.At),
            StepKind.Escort => this.Escort(now, step),
            _ => false,
        };
        if (done)
        {
            // A sequence with several steps moves on only when all are done; the sequence itself says when.
            this.stepIndex++;
            this.travel.Reset();
            this.since = null;
            this.escortArrived = false;
        }
    }

    private static string Describe(QuestStep step) => step.Kind switch
    {
        StepKind.Talk => "talking to the next person.",
        StepKind.Interact => "examining the marked spot.",
        StepKind.UseEventItem => "handing over the item.",
        StepKind.Emote => "using the emote.",
        StepKind.FightDummy => "fighting the striking dummy.",
        StepKind.TemperedRelease => "Tempered Release on the dummy.",
        StepKind.Borrow => "Borrow on the dummy.",
        StepKind.Battle => "the quest battle.",
        StepKind.Escort => "the escort (on foot).",
        _ => step.Kind.ToString(),
    };

    /// <summary>
    /// Goes to <paramref name="at"/> and interacts with its object; true once an event has started and ended there. A bare
    /// position counts as done on arrival. With <paramref name="useItem"/>, the item is used on the object only after an
    /// interaction did not move the quest on (the NPC's own event usually takes it).
    /// </summary>
    private bool Visit(DateTime now, Spot at, uint useItem = 0, ushort emote = 0, bool mount = true)
    {
        if (this.game.InEvent)
        {
            this.sawEvent = true;
            return false;
        }
        if (this.sawEvent)
        {
            this.sawEvent = false;
            this.visits++;
            return true;
        }
        if (this.FightBack(now))
            return false;
        this.rotation.Release();

        var target = at.DataId == 0 ? null : this.game.Nearest(at.DataId);
        var point = target?.Position ?? new Vector3(at.X, at.Y, at.Z);
        var t = this.travel.Tick(at.Territory, point, target is null ? at.Radius : TalkRange, mount: mount);
        if (t == TravelState.Failed)
        {
            this.Fail(now, this.travel.Problem ?? "The destination cannot be reached.");
            return false;
        }
        if (t == TravelState.Moving)
            return false;
        if (target is null)
        {
            if (at.DataId == 0)
                return true;
            this.since ??= now;
            if (now - this.since > NobodyThereFor)
                this.Fail(now, $"Nobody to talk to at ({at.X:0}, {at.Z:0}) in {this.ZoneName(at.Territory)}.");
            return false;
        }
        if (this.game.Mounted)
        {
            if (this.Ready(now, 1.5))
                this.game.UseGeneral(GameActions.Dismount);
            return false;
        }
        if (!this.Ready(now, 2))
            return false;
        if (emote != 0)
            this.game.Emote(emote, target);
        else if (useItem != 0 && this.visits > 0)
            this.game.UseEventItem(useItem, target);
        else
            this.game.Interact(target);
        return false;
    }

    /// <summary>True when <paramref name="beast"/> is out. Assigning a beast to a horn has no known API, so that is asked of the player.</summary>
    private bool Summon(DateTime now, uint beast)
    {
        if (this.game.SummonedBeast() == beast)
            return true;
        int slot = this.game.HornOf(beast);
        if (slot < 0)
        {
            this.Wait(now, $"Put the {BeastSources.ById(beast).Name} on a Battlehorn in the Master's Bestiary. The journey carries on once it is there.");
            return false;
        }
        if (this.game.InCombat)
            return false;
        if (this.Ready(now, 2))
        {
            if (this.game.Mounted)
                this.game.UseGeneral(GameActions.Dismount);
            else
                this.game.UseAction(GameActions.Battlehorns[slot]);
        }
        return false;
    }

    /// <summary>The striking dummy nearest <paramref name="at"/>, walking there first; null until one is in reach.</summary>
    private IGameObject? Dummy(DateTime now, Spot at)
    {
        var dummy = this.game.Nearest(at.DataId);
        if (dummy is not null && this.Chase(now, dummy))
            return dummy;
        if (dummy is not null)
            return null;
        var t = this.travel.Tick(at.Territory, new Vector3(at.X, at.Y, at.Z), 10);
        if (t == TravelState.Failed)
            this.Fail(now, this.travel.Problem ?? "The striking dummies cannot be reached.");
        else if (t == TravelState.Arrived && (this.since ??= now) < now - NobodyThereFor)
            this.Fail(now, "No striking dummy found near the Bannock.");
        return null;
    }

    private bool FightDummy(DateTime now, Spot at)
    {
        if (this.Dummy(now, at) is not { } dummy)
            return false;
        this.game.Target(dummy);
        this.rotation.Set(Mode.Manual);
        return false;
    }

    /// <summary>Uses <paramref name="action"/> on a dummy; true once the game took it.</summary>
    private bool OnDummy(DateTime now, Spot at, uint action)
    {
        this.rotation.Release();
        if (this.Dummy(now, at) is not { } dummy || !this.Ready(now, 1.5))
            return false;
        this.game.Target(dummy);
        if (this.game.UseAction(action, dummy.GameObjectId))
            return true;
        // The hotbar's Auto-attack: the "attack" action is refused in this patch.
        this.game.EnsureAutoAttack(dummy);
        return false;
    }

    private bool Borrow(DateTime now, QuestStep step, ProgressionState s)
    {
        var kin = BeastSources.All.Where(b => b.Kin == step.Kin && s.Owned.Contains(b.Beast)).ToList();
        var horned = kin.FirstOrDefault(b => this.game.HornOf(b.Beast) >= 0);
        if (horned is null)
        {
            this.Wait(now, $"Put one of your {step.Kin} beasts ({string.Join(", ", kin.Select(b => b.Name))}) on a Battlehorn. The journey carries on once it is there.");
            return false;
        }
        if (!this.Summon(now, horned.Beast))
            return false;
        if (!this.borrowed)
        {
            this.borrowed = this.OnDummy(now, step.At, GameActions.Borrow);
            return false;
        }
        if (!this.OnDummy(now, step.At, step.Arg))
            return false;
        this.borrowed = false;
        return true;
    }

    private bool Battle(DateTime now, Spot at)
    {
        if (this.game.InCombat)
        {
            this.rotation.Set(Mode.Auto, targetFreely: true);
            return false;
        }
        var t = this.travel.Tick(at.Territory, new Vector3(at.X, at.Y, at.Z), at.Radius);
        if (t == TravelState.Failed)
            this.Fail(now, this.travel.Problem ?? "The battle cannot be reached.");
        else if (t == TravelState.Arrived)
        {
            if (this.game.Mounted && this.Ready(now, 1.5))
                this.game.UseGeneral(GameActions.Dismount);
            this.rotation.Set(Mode.Auto, targetFreely: true);
        }
        return false;
    }

    /// <summary>Walks (never rides) to the escort's next stop, fighting off what attacks, then talks to the escorted NPC.</summary>
    private bool Escort(DateTime now, QuestStep step)
    {
        if (this.FightBack(now))
            return false;
        this.rotation.Release();
        if (!this.escortArrived)
        {
            var t = this.travel.Tick(step.At.Territory, new Vector3(step.At.X, step.At.Y, step.At.Z), step.At.Radius, mount: false);
            if (t == TravelState.Failed)
                this.Fail(now, this.travel.Problem ?? "The escort's destination cannot be reached.");
            if (t != TravelState.Arrived)
                return false;
            // The talk below walks to the NPC; the stop must not pull the player back to the waypoint.
            this.escortArrived = true;
            this.travel.Reset();
        }
        return this.Visit(now, step.At with { DataId = step.Arg }, mount: false);
    }

    /// <summary>
    /// Deals with whatever attacks the character or its beast (<see cref="ThreatPolicy"/>): rides on when mounted and moving,
    /// flees from a fight it should not take, and otherwise targets the attacker and walks to it, since the rotation plugin
    /// fights but never moves the character. False when there is nothing to do here (no fight, or riding on); true while it
    /// handles the fight.
    /// </summary>
    private bool FightBack(DateTime now)
    {
        if (!this.game.InCombat)
            return false;
        var attackers = this.Attackers();
        // In combat with nothing real attacking (a striking dummy's lingering combat): the task goes on, and walking on drops it.
        if (attackers.Count == 0 && !this.game.Mounted && this.fleeingSince is null)
            return false;
        switch (this.Assess(now, attackers, inFate: false))
        {
            case ThreatMove.Keep:
                return false;
            case ThreatMove.Flee:
                this.Flee(now, attackers);
                return true;
        }
        // Only after a flee that did not get away: a mounted character cannot fight.
        if (this.game.Mounted)
        {
            if (this.nav.IsBusy)
                this.nav.Stop();
            if (this.Ready(now, 1.5))
                this.game.UseGeneral(GameActions.Dismount);
            this.Say("Getting off the mount to fight.");
            return true;
        }
        this.rotation.Set(Mode.Auto, targetFreely: true);
        if (attackers.FirstOrDefault() is { } attacker)
        {
            this.game.Target(attacker);
            if (this.Chase(now, attacker))
                this.game.EnsureAutoAttack(attacker);
            this.Say($"Fighting off {attacker.Name.TextValue}.");
        }
        else
        {
            if (this.nav.IsBusy)
                this.nav.Stop();
            this.Say("Fighting off an attacker.");
        }
        return true;
    }

    /// <summary>Flees when the fight is not one to take, as <see cref="FightBack"/> would; true while it does.</summary>
    private bool FleeIfThreatened(DateTime now, bool inFate)
    {
        if (!this.game.InCombat)
            return false;
        var attackers = this.Attackers();
        if (this.Assess(now, attackers, inFate) != ThreatMove.Flee)
            return false;
        this.Flee(now, attackers);
        return true;
    }

    private ThreatMove Assess(DateTime now, IReadOnlyList<IBattleNpc> attackers, bool inFate)
    {
        if (this.game.Me is not ICharacter me)
            return ThreatMove.Fight;
        return ThreatPolicy.Decide(new ThreatView(
            this.game.Mounted,
            this.game.Mounted && this.nav.IsBusy && this.fleeingSince is null,
            me.Level,
            [.. attackers.Select(a => (int)a.Level)],
            Pct(me),
            this.hpMeter.Dtps(Actor.Player),
            this.config.PlayerFloor,
            this.fleeingSince is { } since ? now - since : null,
            inFate));
    }

    /// <summary>
    /// Runs (sprinting, or riding) back to where the character last was out of combat, at least <see cref="FleeBack"/> away,
    /// or straight away from the attackers when there is no such place, until the enemies give up. The spot goes into the
    /// <see cref="danger"/> map.
    /// </summary>
    private void Flee(DateTime now, IReadOnlyList<IBattleNpc> attackers)
    {
        if (this.game.Me is not { } me)
            return;
        var here = me.Position;
        if (this.fleeingSince is null)
        {
            this.fleeingSince = now;
            this.danger.Record(this.client.TerritoryType, here.X, here.Z);
            this.fleeTo = this.crumbs.Reverse().Select(c => (Vector3?)c.Pos).FirstOrDefault(p => Vector3.Distance(p!.Value, here) >= FleeBack)
                ?? this.Away(here, attackers);
            this.fledFrom.UnionWith(attackers.Select(a => a.GameObjectId));
            this.fleeCheck = (now, here);
            this.fleeTurns = 0;
            this.rotation.Release();
            this.travel.Reset();
            string who = string.Join(", ", attackers.Select(a => $"{a.Name.TextValue} (level {a.Level})"));
            Plugin.Log.Information($"Journey: fleeing from {(who.Length > 0 ? who : "an attacker")}");
        }
        // Still in the fight on arrival: keep going, away from them.
        if (this.fleeTo is { } to && Vector3.Distance(to, here) < 5 && !this.nav.IsBusy)
            this.fleeTo = this.Away(here, attackers);
        // Not moving (a rock, a wall, a point it cannot reach): turn 60, then 120 degrees either way and try again.
        if (now - this.fleeCheck.At >= FleeStuckAfter)
        {
            if (Vector3.Distance(this.fleeCheck.Pos, here) < 1f)
            {
                this.fleeTurns++;
                float turn = (this.fleeTurns % 2 == 1 ? 1 : -1) * 60f * ((this.fleeTurns + 1) / 2);
                this.fleeTo = this.Away(here, attackers, turn);
                if (this.nav.IsBusy)
                    this.nav.Stop();
                Plugin.Log.Information($"Journey: the flight is not moving; turning {turn:0} degrees");
            }
            this.fleeCheck = (now, here);
        }
        if (!this.game.Mounted && this.Ready(now, 1))
            this.game.UseGeneral(GameActions.Sprint);
        if (!this.nav.IsBusy && this.fleeTo is { } dest)
            this.nav.MoveTo(dest, fly: this.game.InFlight);
        this.Say($"Fleeing from {(attackers.Count == 1 ? attackers[0].Name.TextValue : $"{attackers.Count} enemies")}: the fight is not one to take.");
    }

    /// <summary>
    /// A point <see cref="FleeBack"/> yalms from <paramref name="here"/>, away from the attackers (turned by
    /// <paramref name="turnDegrees"/>), that a path can reach.
    /// </summary>
    private Vector3 Away(Vector3 here, IReadOnlyList<IBattleNpc> attackers, float turnDegrees = 0)
    {
        var from = attackers.Count == 0 ? here : attackers.Select(a => a.Position).Aggregate((a, b) => a + b) / attackers.Count;
        var dir = new Vector3(here.X - from.X, 0, here.Z - from.Z);
        dir = dir.LengthSquared() < 0.01f ? new Vector3(1, 0, 0) : Vector3.Normalize(dir);
        if (turnDegrees != 0)
            dir = Vector3.Transform(dir, Quaternion.CreateFromAxisAngle(Vector3.UnitY, turnDegrees * MathF.PI / 180f));
        var point = here + (dir * FleeBack);
        return this.nav.Reachable(point) ?? this.nav.Floor(point) ?? point;
    }

    /// <summary>
    /// After a flight: stands still out of combat until HP is full, so the character does not walk back into a fight it
    /// just left. A new fight is left to <see cref="FightBack"/>. True while it rests.
    /// </summary>
    private bool Recover()
    {
        if (!this.recovering || this.game.InCombat || this.game.Me is not ICharacter me)
            return false;
        if (me.CurrentHp >= me.MaxHp)
        {
            this.recovering = false;
            Plugin.Log.Information("Journey: back to full HP after the flight");
            return false;
        }
        if (this.nav.IsBusy)
            this.nav.Stop();
        this.rotation.Release();
        this.Say(FormattableString.Invariant($"Resting after the flight: HP {Pct(me):0}%, carrying on at 100%."));
        return true;
    }

    /// <summary>Every tick: the HP lost in a fight, where the character has been out of combat, and the end of a flight.</summary>
    private void Watch(DateTime now)
    {
        if (this.game.Me is not ICharacter me)
            return;
        bool combat = this.game.InCombat;
        if (combat)
        {
            if (!this.wasInCombat)
                this.hpMeter.Reset();
            this.hpMeter.Sample(Actor.Player, now, Pct(me), tanking: true);
        }
        else
        {
            if (this.fleeingSince is not null)
            {
                Plugin.Log.Information("Journey: got away");
                this.recovering = true;
                this.fleeingSince = null;
                this.fleeTo = null;
                if (this.nav.IsBusy)
                    this.nav.Stop();
            }
            if (this.crumbs.Count == 0 || now - this.crumbs.Last().At >= CrumbEvery)
            {
                this.crumbs.Enqueue((now, me.Position));
                while (this.crumbs.Count > Crumbs)
                    this.crumbs.Dequeue();
            }
        }
        this.wasInCombat = combat;
    }

    /// <summary>
    /// Walks away from a striking dummy whose fight is all that keeps the character in combat, until the game ends it
    /// (a dummy never dies, and does not pull again). True while it does.
    /// </summary>
    private bool LeaveDummyCombat()
    {
        if (!this.game.InCombat || this.game.Me is not { } me || this.Attackers().Count > 0)
            return false;
        var ours = this.objects.Where(o => o.OwnerId == me.EntityId).Select(o => o.GameObjectId).Append(me.GameObjectId).ToHashSet();
        if (this.objects.OfType<IBattleNpc>().FirstOrDefault(o => GameActions.IsTrainingDummy(o) && ours.Contains(o.TargetObjectId)) is not { } dummy)
            return false;
        this.rotation.Release();
        if (!this.nav.IsBusy)
            this.nav.MoveTo(this.Away(me.Position, [dummy]), fly: this.game.InFlight);
        this.Say("Walking away from the striking dummy so its fight ends.");
        return true;
    }

    /// <summary>The living enemies attacking the player or the player's beast: the current target first, then nearest first.</summary>
    private IReadOnlyList<IBattleNpc> Attackers()
    {
        if (this.game.Me is not { } me)
            return [];
        var ours = this.objects.Where(o => o.OwnerId == me.EntityId).Select(o => o.GameObjectId).Append(me.GameObjectId).ToHashSet();
        ulong current = me.TargetObject?.GameObjectId ?? 0;
        return [.. this.objects.OfType<IBattleNpc>()
            .Where(o => GameActions.IsHostile(o) && !o.IsDead && o.CurrentHp > 0 && o.IsTargetable && ours.Contains(o.TargetObjectId)
                && !GameActions.IsTrainingDummy(o))
            .OrderBy(o => o.GameObjectId == current ? 0 : 1)
            .ThenBy(o => this.game.DistanceTo(o.Position))];
    }

    private IBattleNpc? Attacker() => this.Attackers().FirstOrDefault();

    private static double Pct(ICharacter c) => c.MaxHp == 0 ? 100 : 100.0 * c.CurrentHp / c.MaxHp;

    /// <summary>Walks into melee range of a moving target; true when there.</summary>
    private bool Chase(DateTime now, IGameObject target)
    {
        // Airborne: land first. A flying path takes seconds to compute and wanders at short range; the chase goes on the ground.
        if (this.game.InFlight)
        {
            if (this.nav.IsBusy)
                this.nav.Stop();
            if (this.Ready(now, 1.5))
                this.game.UseGeneral(GameActions.Dismount);
            return false;
        }
        float distance = this.game.DistanceTo(target.Position) - target.HitboxRadius;
        if (distance <= MeleeRange)
        {
            if (this.nav.IsBusy)
                this.nav.Stop();
            if (!this.game.Mounted)
                return true;
            if (this.Ready(now, 1.5))
                this.game.UseGeneral(GameActions.Dismount);
            return false;
        }
        // A new path starts where the character stood when it was asked for, which it has passed by the time the path
        // arrives; asking again while the path still leads to the target turns the character back and forth, most of all at
        // the game's low background frame rate. So a path is kept while it still ends near the target.
        var path = this.nav.IsBusy ? this.nav.Waypoints : [];
        bool leadsThere = path.Count > 0 && Vector3.Distance(path[^1], target.Position) <= ChaseRepathBeyond;
        if (!leadsThere && !this.nav.Pathfinding && now >= this.nextChase)
        {
            this.nextChase = now + TimeSpan.FromSeconds(1);
            this.nav.MoveCloseTo(target.Position, MeleeRange - 0.5f + target.HitboxRadius);
        }
        return false;
    }

    // --- Beasts ---------------------------------------------------------------------------------------------------

    private void DoCapture(DateTime now, Activity a, ProgressionState s)
    {
        if (!this.EnsureJob(now))
            return;
        if (this.hunt == 0 || !a.Beasts!.Contains(this.hunt))
        {
            // Prefer a beast in this zone, then the planner's order.
            this.hunt = a.Beasts!.OrderBy(b => BeastSources.ById(b).Spawns.Any(sp => sp.Territory == this.client.TerritoryType) ? 0 : 1).First();
            this.huntStarted = now;
            this.searchPoint = 0;
            this.since = null;
            this.prey = 0;
        }
        var src = BeastSources.ById(this.hunt);
        if (now - this.huntStarted > HuntFor)
        {
            this.Fail(now, $"No {src.Name} captured in {HuntFor.TotalMinutes} minutes; it is skipped for this run.");
            return;
        }

        if (this.Prey(src, s.JobLevel) is not { } target)
        {
            this.prey = 0;
            if (this.FightBack(now))
                return;
            this.rotation.Release();
            this.Search(now, src);
            return;
        }

        // Another mob attacking comes first: weakening the prey while something else hits would get the character KO'd.
        if (this.FleeIfThreatened(now, inFate: false))
            return;
        if (this.Attacker() is { } other && other.GameObjectId != target.GameObjectId && this.FightBack(now))
            return;
        if (target.GameObjectId != this.prey)
            this.attackTries = 0;
        this.prey = target.GameObjectId;
        this.since = null;
        int hp = target.MaxHp == 0 ? 100 : (int)(100L * target.CurrentHp / target.MaxHp);
        bool marked = GameActions.HasStatus(target, GameActions.InterestCaptured);
        var move = CaptureTactics.Decide(hp, s.JobLevel - target.Level, marked);
        this.Say($"Capturing a {src.Name}: {move switch { CaptureMove.Weaken => "wearing it down", CaptureMove.Capture => "using Capture", _ => "finishing it" }} (HP {hp}%).");
        bool inReach = this.Chase(now, target);
        if ((move, inReach) != this.captureStep)
        {
            Plugin.Log.Information($"Journey: capture of a {src.Name} (level {target.Level}, HP {hp}%): {move}, {(inReach ? "in reach" : $"{this.game.DistanceTo(target.Position):0.0} y away")}");
            this.captureStep = (move, inReach);
        }
        if (!inReach)
            return;
        this.game.Target(target);
        switch (move)
        {
            case CaptureMove.Weaken:
                this.rotation.Release();
                // In the fight: keep the swings on (a beast that pulled the character first leaves them off).
                if (this.game.InCombat)
                {
                    this.attackTries = 0;
                    if (this.Ready(now, 1.5))
                        this.game.EnsureAutoAttack(target);
                    break;
                }
                if (!this.Ready(now, 1.5))
                    break;
                // Until the fight starts: the hotbar's Auto-attack twice (what worked in game; the attack action was refused),
                // then the attack action twice, then Capture, an attack too.
                this.attackTries++;
                string sent;
                if (this.attackTries <= 2 && this.game.IsAutoAttacking)
                    break;
                if (this.attackTries <= 2)
                    sent = $"the hotbar Auto-attack: {this.game.UseGeneral(GameActions.AutoAttackGeneral)}";
                else if (this.attackTries <= 4 && this.Accepts(GameActions.AutoAttack, target, "auto-attack"))
                    sent = $"the attack action: {this.game.StartAutoAttack(target)}";
                else if (this.Accepts(GameActions.Capture, target, "Capture"))
                    sent = $"Capture to open the fight: {this.game.UseAction(GameActions.Capture, target.GameObjectId)}";
                else
                    break;
                Plugin.Log.Information($"Journey: capture try {this.attackTries} on the {target.Name.TextValue}, sent {sent}");
                break;
            case CaptureMove.Capture:
                this.rotation.Release();
                if (this.Ready(now, 1) && this.Accepts(GameActions.Capture, target, "Capture"))
                    this.game.UseAction(GameActions.Capture, target.GameObjectId);
                break;
            default:
                this.rotation.Set(Mode.Manual);
                break;
        }
    }

    /// <summary>
    /// Whether the game would take <paramref name="action"/> on <paramref name="target"/> now. A refusal is not sent (each
    /// one shows "cannot execute" in chat); its code goes into the plugin log and the status line once per change.
    /// </summary>
    private bool Accepts(uint action, IGameObject target, string what)
    {
        uint status = this.game.ActionStatus(action, target.GameObjectId);
        if (status != 0 && (status, action) != this.lastRefusal)
            Plugin.Log.Warning($"Journey: the game refuses {what} on {target.Name.TextValue} (status {status})");
        this.lastRefusal = status == 0 ? default : (status, action);
        if (status != 0)
            this.Say($"The game refuses {what} on the {target.Name.TextValue} for now (status {status}).");
        return status == 0;
    }

    /// <summary>The wild form of <paramref name="src"/> to go after: the one already engaged, else the nearest free one.</summary>
    private IBattleNpc? Prey(BeastSource src, int level)
    {
        var me = this.game.Me;
        if (me is null)
            return null;
        // Free, or fighting the player or the player's beast (which often pulls it first); never someone else's.
        var ours = this.objects.Where(o => o.OwnerId == me.EntityId).Select(o => o.GameObjectId).Append(me.GameObjectId).ToHashSet();
        bool Fits(IBattleNpc o) =>
            GameActions.IsHostile(o) && src.WildNames.Contains(o.NameId) && o.Level <= level && o.CurrentHp > 0 && o.IsTargetable
            && (GameActions.HasNoTarget(o) || ours.Contains(o.TargetObjectId));
        var mobs = this.objects.OfType<IBattleNpc>().Where(Fits).ToList();
        return mobs.FirstOrDefault(o => o.GameObjectId == this.prey) ?? mobs.OrderBy(o => this.game.DistanceTo(o.Position)).FirstOrDefault();
    }

    /// <summary>Walks a small pattern around each spawn point in turn until the beast's wild form comes into view.</summary>
    private void Search(DateTime now, BeastSource src)
    {
        int points = src.Spawns.Count * SearchPattern.Length;
        if (this.searchPoint >= points * 2)
        {
            this.Fail(now, $"No {src.Name} found at its spawn points (the data may be wrong); it is skipped for this run.");
            return;
        }
        var spawn = src.Spawns[this.searchPoint / SearchPattern.Length % src.Spawns.Count];
        var offset = SearchPattern[this.searchPoint % SearchPattern.Length];
        var point = Travel.ToPoint(spawn) + new Vector3(offset.X, 0, offset.Y);
        if (this.danger.IsDangerous(spawn.Territory, point.X, point.Z))
        {
            this.searchPoint++;
            return;
        }
        this.Say($"Looking for a {src.Name} in {this.ZoneName(spawn.Territory)} ({spawn.MapX:0.0}, {spawn.MapY:0.0}).");

        var t = this.travel.Tick(spawn.Territory, point, 8, heightKnown: false);
        if (t == TravelState.Moving)
            return;
        if (t == TravelState.Failed && this.client.TerritoryType != spawn.Territory)
        {
            this.Fail(now, this.travel.Problem ?? $"{this.ZoneName(spawn.Territory)} cannot be reached.");
            return;
        }
        // Arrived, or this point is off the mesh: look a while, then try the next point.
        this.since ??= now;
        if (t == TravelState.Failed || now - this.since > SearchPointFor)
        {
            this.searchPoint++;
            this.since = null;
            this.travel.Reset();
        }
    }

    private void DoUseGourd(DateTime now, Activity a)
    {
        if (!this.EnsureJob(now))
            return;
        this.Say($"Using the {this.reader.ItemName(a.Item)}.");
        if (this.game.ItemCount(a.Item) > 0 && !this.game.InCombat && this.Ready(now, 3))
            this.game.UseItem(a.Item);
    }

    /// <summary>
    /// Buys a gourd from the Kornago merchant; Bright Remnants it is short of are first exchanged for Faded ones. The exchange
    /// dialog must name the item before it is confirmed, and the purchase counts only when the inventory shows it.
    /// </summary>
    private void DoBuy(DateTime now, Activity a, ProgressionState s)
    {
        var (item, quantity, currency) = Purchase(a, s);
        var shop = KornagoShop.For(currency);
        int index = KornagoShop.Position(shop.Lines, item, s.IsComplete);
        string name = this.reader.ItemName(item);
        if (name.Length == 0)
        {
            // The exchange is confirmed by finding this name in the dialog; an empty name would match anything.
            this.Stop($"Item {item} has no name in the game data, so the purchase cannot be checked.");
            return;
        }
        if (index < 0)
        {
            this.Stop($"The shop does not list the {name} yet.");
            return;
        }

        int have = this.game.ItemCount(item);
        if (this.buyItem != item)
        {
            this.buyItem = item;
            this.buyBefore = have;
            this.buyTries = 0;
        }
        if (have > this.buyBefore)
        {
            // Bought; the next tick plans again (the gourd next, or using it).
            var done = this.game.Addon("ShopExchangeItem");
            if (done != null)
                done->Close(true);
            this.buyItem = 0;
            return;
        }
        this.Say($"Buying {quantity} {name} from the Kornago merchant.");

        var dialog = this.game.Addon("ShopExchangeItemDialog");
        if (dialog != null)
        {
            if (!this.Ready(now, 1))
                return;
            if (GameActions.AllText(dialog).Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                GameActions.Callback(dialog, 0);
                return;
            }
            dialog->Close(true);
            this.Stop($"The exchange window did not name the {name}, so nothing was bought. The shop layout may have changed.");
            return;
        }
        var window = this.game.Addon("ShopExchangeItem");
        if (window != null)
        {
            if (!this.Ready(now, 2))
                return;
            if (++this.buyTries > 5)
            {
                this.Stop($"The shop did not sell the {name} after 5 tries.");
                return;
            }
            GameActions.Callback(window, 0, index, quantity);
            return;
        }
        this.Visit(now, KornagoShop.Spot);
    }

    // --- Boards ---------------------------------------------------------------------------------------------------

    /// <summary>Starts one run of the board through the session runner; <see cref="BoardTick"/> follows it from there.</summary>
    private void DoBoard(DateTime now, Activity a)
    {
        var board = Boards.ByNumber(a.Board);
        if (!this.EnsureJob(now))
            return;
        if (this.boards.Current is not null)
        {
            this.Wait(now, "A board session is already running; the journey continues when it ends.");
            return;
        }
        var checks = this.boards.Start([new QueueEntry(a.Board, 1)]);
        if (this.boards.Current is null)
        {
            var fail = checks.FirstOrDefault(c => c.Status == CheckStatus.Fail);
            this.Stop($"The {board.Name} cannot start{(fail is null ? "." : $": {fail.Label}: {fail.Detail}")}");
            return;
        }
        this.boardRun = a.Board;
        this.Say($"Starting the {board.Name} through AutoDuty.");
    }

    // --- Levelling ------------------------------------------------------------------------------------------------

    /// <summary>
    /// FATEs in the zones for the current level band (docs/research/09-leveling.md), best expected EXP per second first.
    /// The zone changes when it has had nothing to join for a minute, or when another zone of the band has measured
    /// clearly faster.
    /// </summary>
    private void DoLevel(DateTime now, ProgressionState s)
    {
        if (!this.EnsureJob(now))
            return;
        uint here = this.client.TerritoryType;
        this.meter.Record(now, here, s.JobLevel, this.reader.Experience());
        var zones = LevelingZones.For(s.JobLevel).ToList();
        // A new level band applies between FATEs, so a level gained mid-FATE does not walk away from its completion.
        if (!zones.SequenceEqual(this.bandZones) && this.Fate(this.fate) is not { State: FateState.Running })
        {
            this.bandZones = zones;
            this.zone = zones.Contains(here) ? here : zones[0];
            this.meter.Reset();
            this.unreachable.Clear();
            this.stalled.Clear();
        }
        zones = this.bandZones;

        if (here != this.zone)
        {
            // Teleporting waits out a fight, so fight it.
            if (this.FightBack(now))
                return;
            this.rotation.Release();
            this.Say($"Going to {this.ZoneName(this.zone)} for FATEs.");
            if (this.travel.Tick(this.zone, Vector3.Zero, float.MaxValue) == TravelState.Failed)
            {
                Plugin.Log.Information($"Journey: skipping {this.ZoneName(this.zone)}: {this.travel.Problem}");
                this.unreachable.Add(this.zone);
                this.travel.Reset();
                var reachable = zones.Where(z => !this.unreachable.Contains(z)).ToList();
                if (reachable.Count == 0)
                {
                    this.unreachable.Clear();
                    this.Fail(now, "No levelling zone for this level can be reached; attune the aetherytes there.");
                    return;
                }
                this.zone = reachable.Contains(here) ? here : reachable[0];
            }
            return;
        }

        if (this.Fate(this.fate) is { State: FateState.Running } f && this.game.Me is { } me)
        {
            this.since = null;
            float distance = Vector2.Distance(new(f.Position.X, f.Position.Z), new(me.Position.X, me.Position.Z));
            // On a ledge above (or a hollow below) the FATE, the character is not in it, whatever the map distance says.
            bool offLevel = MathF.Abs(me.Position.Y - f.Position.Y) > FateHeight;
            if ((distance > f.Radius * 0.6f || offLevel) && !this.game.InCombat)
            {
                // A FATE it cannot get to (a cliff, no way down) is given up rather than tried forever.
                if (this.headingTo.Fate != f.FateId)
                    this.headingTo = (f.FateId, now);
                else if (now - this.headingTo.Since > FateReachFor)
                {
                    Plugin.Log.Information($"Journey: the FATE {f.Name} could not be reached in {FateReachFor.TotalMinutes:0} minutes; leaving it");
                    this.stalled.Add(f.FateId);
                    this.fate = 0;
                    this.travel.Reset();
                    return;
                }
                this.Say($"Heading to the FATE \"{f.Name}\" (level {f.Level}, {f.Progress}%).");
                if (this.travel.Tick(here, f.Position, MathF.Min(f.Radius * 0.4f, FateHeight)) == TravelState.Failed)
                    this.fate = 0;
                return;
            }
            this.travel.Reset();
            if (this.game.Mounted)
            {
                if (this.Ready(now, 1.5))
                    this.game.UseGeneral(GameActions.Dismount);
                return;
            }
            var fm = FateManager.Instance();
            if (fm != null && s.JobLevel > f.MaxLevel && fm->SyncedFateId != f.FateId && this.Ready(now, 3))
                fm->LevelSync();
            this.rotation.Set(Mode.Auto, targetFreely: true);

            if (this.fateWatch.Fate != f.FateId || f.Progress > this.fateWatch.Progress)
                this.fateWatch = (f.FateId, f.Progress, now);
            else if (now - this.fateWatch.At > FateStallsAfter)
            {
                Plugin.Log.Information($"Journey: leaving the FATE {f.Name}: no progress for {FateStallsAfter.TotalMinutes:0} minutes");
                this.stalled.Add(f.FateId);
                this.fate = 0;
                return;
            }

            // The rotation fights what is in reach; the character walks to the FATE's enemies itself.
            if (this.FleeIfThreatened(now, inFate: true))
                return;
            if ((this.Attacker() ?? this.FateEnemy(f)) is { } enemy)
            {
                this.game.Target(enemy);
                if (this.Chase(now, enemy))
                    this.game.EnsureAutoAttack(enemy);
                this.Say($"FATE \"{f.Name}\": {f.Progress}%.");
            }
            else
            {
                // Nothing to fight from here: closer to the centre, where the FATE's enemies are.
                if (!this.nav.IsBusy && Vector2.Distance(new(f.Position.X, f.Position.Z), new(me.Position.X, me.Position.Z)) > FateCentre)
                    this.nav.MoveCloseTo(f.Position, FateCentre, fly: this.game.InFlight);
                this.Say($"FATE \"{f.Name}\": {f.Progress}%, looking for its enemies.");
            }
            return;
        }

        this.fate = 0;
        if (this.game.InCombat)
        {
            // A fight in progress: fled when not worth taking, else fought (target, walk over, auto-attack, the rotation).
            if (this.FightBack(now))
                return;
            this.rotation.Set(Mode.Auto, targetFreely: true);
            this.Say("Finishing the fight.");
            return;
        }

        uint avoid = now - this.leftIdleAt < ZoneCooldown ? this.leftIdle : 0;
        if (JourneyRules.BetterZone(zones, here, this.meter.Rate, avoid) is { } better)
        {
            this.rotation.Release();
            this.zone = better;
            return;
        }
        if (FatePicker.Pick(this.Candidates().Where(c => !this.stalled.Contains(c.Id)), s.JobLevel) is { } pick)
        {
            this.rotation.Release();
            this.fate = pick.Id;
            this.travel.Reset();
            return;
        }
        // No FATE to join: nearby mobs give EXP meanwhile, and the zone stays while there are any.
        if (this.Grind(now, s.JobLevel))
        {
            this.since = null;
            return;
        }
        this.rotation.Release();
        this.grindTarget = 0;
        this.since ??= now;
        this.Say($"Waiting for a FATE in {this.ZoneName(here)}.");
        if (now - this.since > IdleZoneFor)
        {
            this.zone = JourneyRules.NextZone(zones, here, this.meter.Rate);
            this.leftIdle = here;
            this.leftIdleAt = now;
            this.since = null;
        }
    }

    /// <summary>
    /// Fights the nearest free open-world mob from <see cref="GrindLevelsBelow"/> levels under the character up to its level,
    /// within <see cref="GrindRange"/>, while no FATE can be joined. False when there is none.
    /// </summary>
    private bool Grind(DateTime now, int level)
    {
        if (this.game.Me is not { } me)
            return false;
        var ours = this.objects.Where(o => o.OwnerId == me.EntityId).Select(o => o.GameObjectId).Append(me.GameObjectId).ToHashSet();
        bool Fits(IBattleNpc o) =>
            GameActions.IsHostile(o) && !o.IsDead && o.CurrentHp > 0 && o.IsTargetable && !GameActions.IsTrainingDummy(o) && !this.fledFrom.Contains(o.GameObjectId)
            && o.Level <= level && o.Level >= level - GrindLevelsBelow
            && (GameActions.HasNoTarget(o) || ours.Contains(o.TargetObjectId))
            && ((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)o.Address)->FateId == 0
            && this.game.DistanceTo(o.Position) <= GrindRange
            && !this.danger.IsDangerous(this.client.TerritoryType, o.Position.X, o.Position.Z);
        var mob = this.objects.SearchById(this.grindTarget) is IBattleNpc kept && Fits(kept)
            ? kept
            : this.objects.OfType<IBattleNpc>().Where(Fits).OrderBy(o => this.game.DistanceTo(o.Position)).FirstOrDefault();
        if (mob is null)
            return false;
        if (mob.GameObjectId != this.grindTarget)
            Plugin.Log.Information($"Journey: no FATE to join; fighting a {mob.Name.TextValue} (level {mob.Level}) meanwhile");
        this.grindTarget = mob.GameObjectId;
        this.Say($"No FATE to join yet: fighting a {mob.Name.TextValue} (level {mob.Level}) meanwhile.");
        if (!this.Chase(now, mob))
            return true;
        this.rotation.Set(Mode.Manual);
        // The hotbar's Auto-attack starts the fight (the "attack" action is refused in this patch); the rotation follows.
        if (this.Ready(now, 1.5))
            this.game.EnsureAutoAttack(mob);
        return true;
    }

    private IEnumerable<FateCandidate> Candidates()
    {
        for (int i = 0; i < this.fates.Length; i++)
        {
            var f = this.fates[i];
            if (f is null || f.State != FateState.Running || this.danger.IsDangerous(this.client.TerritoryType, f.Position.X, f.Position.Z))
                continue;
            yield return new FateCandidate(f.FateId, f.Level, f.MaxLevel, f.Progress, f.TimeRemaining, this.game.DistanceTo(f.Position), f.HasBonus, NeedsHandIn(f));
        }
    }

    /// <summary>The current target when it is alive and hostile, else the nearest living enemy of FATE <paramref name="fateId"/>.</summary>
    private IGameObject? FateEnemy(IFate f)
    {
        if (this.objects.LocalPlayer?.TargetObject is IBattleNpc t && GameActions.IsHostile(t) && !t.IsDead && t.CurrentHp > 0 && !GameActions.IsTrainingDummy(t))
            return t;
        var alive = this.objects.OfType<IBattleNpc>()
            .Where(o => GameActions.IsHostile(o) && !o.IsDead && o.CurrentHp > 0 && o.IsTargetable && !GameActions.IsTrainingDummy(o))
            .ToList();
        // The FATE's own enemies; failing that, any enemy inside its circle (the mobs' FATE id is not always set).
        return alive.Where(o => ((FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)o.Address)->FateId == f.FateId)
                .OrderBy(o => this.game.DistanceTo(o.Position)).FirstOrDefault()
            ?? alive.Where(o => Vector2.Distance(new(o.Position.X, o.Position.Z), new(f.Position.X, f.Position.Z)) <= f.Radius)
                .OrderBy(o => this.game.DistanceTo(o.Position)).FirstOrDefault();
    }

    /// <summary>A collection FATE: it asks for an item to be handed in.</summary>
    private static bool NeedsHandIn(IFate f) => f.Address != IntPtr.Zero && ((FateContext*)f.Address)->EventItem != 0;

    private IFate? Fate(ushort id)
    {
        if (id == 0)
            return null;
        for (int i = 0; i < this.fates.Length; i++)
        {
            if (this.fates[i] is { } f && f.FateId == id)
                return f;
        }
        return null;
    }

    private string ZoneName(uint territory) =>
        this.data.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>().GetRowOrDefault(territory)?.PlaceName.ValueNullable?.Name.ExtractText() ?? $"zone {territory}";

    private void OnLogout(int type, int code) => this.Stop("Logged out.");
}
