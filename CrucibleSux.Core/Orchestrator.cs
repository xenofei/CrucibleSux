namespace CrucibleSux.Core;

/// <param name="MaxAttempts">Tries per queued run before the session gives up.</param>
/// <param name="StartWithin">How long the executor has to report that it is running.</param>
/// <param name="ReachBoardWithin">How long the walk to Lauda, the menus and team setup may take.</param>
/// <param name="StuckAfter">Inside a board, how long nothing may change before the watchdog stops the run.</param>
/// <param name="SettleWithin">After the board is left, how long the executor has to stop by itself.</param>
public sealed record OrchestratorSettings(
    int MaxAttempts,
    TimeSpan StartWithin,
    TimeSpan ReachBoardWithin,
    TimeSpan StuckAfter,
    TimeSpan SettleWithin,
    TimeSpan PauseBetweenRuns)
{
    public static OrchestratorSettings Default { get; } = new(
        2, TimeSpan.FromSeconds(20), TimeSpan.FromMinutes(6), TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
}

/// <summary>What the game and the executor look like on one tick.</summary>
/// <param name="ExecutorRunning">The board driver (AutoDuty) is running.</param>
/// <param name="ResultShown">The board's result screen is open.</param>
/// <param name="Activity">A fingerprint of what the player is doing; any change counts as progress for the watchdog.</param>
public sealed record Observation(DateTime Now, uint Territory, bool ExecutorRunning, bool ResultShown, long Activity, IReadOnlySet<int> ClearedBoards);

public enum CommandKind
{
    /// <summary>Start one run of <see cref="Command.Board"/>.</summary>
    StartBoard,

    /// <summary>Stop the executor.</summary>
    StopExecutor,
}

public sealed record Command(CommandKind Kind, BoardInfo? Board = null);

public enum SessionPhase
{
    /// <summary>Between runs.</summary>
    Waiting,

    /// <summary>The start command was sent; waiting for the executor to report it is running.</summary>
    Starting,

    /// <summary>On the way to the board: Lauda, the menus, team setup.</summary>
    Travelling,

    /// <summary>Inside the board's instance.</summary>
    InBoard,

    /// <summary>The board was left; waiting for the executor to stop.</summary>
    Leaving,

    /// <summary>The session is over; <see cref="Orchestrator.Report"/> says how it went.</summary>
    Ended,
}

/// <summary>
/// The session queue and its watchdog (docs/PLAN.md F5, F9). It decides; the plugin observes the game, carries out the
/// commands and stores the records. A run that fails outside a board is retried up to <see cref="OrchestratorSettings.MaxAttempts"/>
/// times; anything that leaves the character inside a board ends the session, since only a person should judge that state.
/// </summary>
public sealed class Orchestrator
{
    private readonly List<QueueEntry> queue;
    private readonly OrchestratorSettings settings;
    private readonly List<RunRecord> runs = [];
    private int entry;
    private int done;
    private DateTime phaseSince;
    private DateTime runStarted;
    private DateTime nextStart;
    private long lastActivity;
    private bool resultSeen;
    private bool wasCleared;
    private RunOutcome leftWith;
    private string leftDetail = string.Empty;
    private DateTime? busySince;
    private DateTime? stoppedSince;

    /// <summary>How long a stopped executor may stay inside a board after the result screen before it counts as stopped there.</summary>
    private static readonly TimeSpan ExitGrace = TimeSpan.FromSeconds(30);

    public Orchestrator(IEnumerable<QueueEntry> queue, OrchestratorSettings settings, DateTime now)
    {
        this.queue = queue.Where(e => e.Runs > 0).ToList();
        if (this.queue.Count == 0)
            throw new ArgumentException("The queue is empty.", nameof(queue));
        this.settings = settings;
        this.StartedUtc = now;
        this.nextStart = now;
        this.Status = "Starting";
    }

    public DateTime StartedUtc { get; }

    public SessionPhase Phase { get; private set; } = SessionPhase.Waiting;

    public IReadOnlyList<QueueEntry> Queue => this.queue;

    /// <summary>The queue line being worked on, or the queue's length once it is done.</summary>
    public int EntryIndex => this.entry;

    public BoardInfo? Board => this.entry < this.queue.Count ? Boards.ByNumber(this.queue[this.entry].Board) : null;

    /// <summary>Which run of the current line this is, from 1.</summary>
    public int RunNumber => this.done + 1;

    /// <summary>The current try at this run, from 1; 0 before the first start.</summary>
    public int Attempt { get; private set; }

    /// <summary>When the watchdog last saw something change.</summary>
    public DateTime LastProgress { get; private set; }

    /// <summary>One line for the window: what the session is doing now.</summary>
    public string Status { get; private set; }

    public IReadOnlyList<RunRecord> Runs => this.runs;

    public SessionReport? Report { get; private set; }

    public IReadOnlyList<Command> Tick(Observation o)
    {
        if (this.Phase == SessionPhase.Ended)
            return [];
        var board = this.Board!;
        if (o.ResultShown)
            this.resultSeen = true;
        if (o.Activity != this.lastActivity)
        {
            this.lastActivity = o.Activity;
            this.LastProgress = o.Now;
        }

        switch (this.Phase)
        {
            case SessionPhase.Waiting:
                if (o.Now < this.nextStart)
                    return [];
                if (o.ExecutorRunning)
                {
                    this.busySince ??= o.Now;
                    if (o.Now - this.busySince > this.settings.SettleWithin)
                    {
                        this.End(o.Now, false, "AutoDuty kept running when the next run was due, so the session stopped.");
                        return [];
                    }
                    this.Status = "Waiting for AutoDuty to stop before the next run";
                    return [];
                }
                this.busySince = null;
                this.Attempt++;
                this.runStarted = o.Now;
                this.resultSeen = false;
                this.stoppedSince = null;
                this.wasCleared = o.ClearedBoards.Contains(board.Number);
                this.LastProgress = o.Now;
                this.Enter(SessionPhase.Starting, o.Now, $"Starting the {board.Name}");
                return [new Command(CommandKind.StartBoard, board)];

            case SessionPhase.Starting:
                if (o.ExecutorRunning)
                {
                    this.Enter(SessionPhase.Travelling, o.Now, $"Heading to the {board.Name}");
                    return this.Tick(o);
                }
                if (o.Now - this.phaseSince > this.settings.StartWithin)
                {
                    // Stop it anyway, in case AutoDuty starts late with settings the runner is about to restore.
                    this.Finish(o, RunOutcome.NotStarted, "AutoDuty did not start the board.", insideBoard: false);
                    return [new Command(CommandKind.StopExecutor)];
                }
                return [];

            case SessionPhase.Travelling:
                if (o.Territory == board.Territory)
                {
                    this.Enter(SessionPhase.InBoard, o.Now, $"Playing the {board.Name}");
                    this.LastProgress = o.Now;
                    return [];
                }
                if (!o.ExecutorRunning)
                {
                    this.Finish(o, RunOutcome.Failed, "AutoDuty stopped before the board began.", insideBoard: false);
                    return [];
                }
                if (o.Now - this.phaseSince > this.settings.ReachBoardWithin)
                {
                    this.Finish(o, RunOutcome.TimedOut, $"The board was not reached within {Minutes(this.settings.ReachBoardWithin)}.", insideBoard: false);
                    return [new Command(CommandKind.StopExecutor)];
                }
                return [];

            case SessionPhase.InBoard:
                if (o.Territory != board.Territory)
                {
                    this.leftWith = this.resultSeen ? RunOutcome.Finished : RunOutcome.Failed;
                    this.leftDetail = this.resultSeen ? "Played to the result screen." : "The board was left without a result screen.";
                    this.Enter(SessionPhase.Leaving, o.Now, $"Leaving the {board.Name}");
                    return this.Tick(o);
                }
                if (!o.ExecutorRunning)
                {
                    // After the result screen AutoDuty may stop a moment before the territory changes on the way out.
                    this.stoppedSince ??= o.Now;
                    if (this.resultSeen && o.Now - this.stoppedSince <= ExitGrace)
                        return [];
                    this.Finish(o, this.resultSeen ? RunOutcome.Finished : RunOutcome.Failed, "AutoDuty stopped inside the board.", insideBoard: true);
                    return [];
                }
                this.stoppedSince = null;
                if (o.Now - this.LastProgress > this.settings.StuckAfter)
                {
                    this.Finish(o, RunOutcome.TimedOut, $"Nothing changed inside the board for {Minutes(this.settings.StuckAfter)}.", insideBoard: true);
                    return [new Command(CommandKind.StopExecutor)];
                }
                return [];

            case SessionPhase.Leaving:
                if (!o.ExecutorRunning)
                {
                    this.Finish(o, this.leftWith, this.leftDetail, insideBoard: false);
                    return [];
                }
                if (o.Now - this.phaseSince > this.settings.SettleWithin)
                {
                    this.Finish(o, this.leftWith, this.leftDetail + " AutoDuty kept running afterwards and was stopped.", insideBoard: false);
                    return [new Command(CommandKind.StopExecutor)];
                }
                return [];

            default:
                return [];
        }
    }

    /// <summary>The executor refused the start command: there is no point retrying, so the session ends.</summary>
    public void StartFailed(DateTime now, string reason)
    {
        if (this.Phase != SessionPhase.Starting)
            return;
        this.Record(now, RunOutcome.NotStarted, reason, firstClear: false);
        this.End(now, false, $"AutoDuty could not start the {this.Board!.Name}: {reason}");
    }

    /// <summary>The player (or logging out, or unloading) stopped the session.</summary>
    public IReadOnlyList<Command> Cancel(DateTime now, string reason)
    {
        if (this.Phase == SessionPhase.Ended)
            return [];
        bool running = this.Phase != SessionPhase.Waiting;
        if (running)
            this.Record(now, RunOutcome.Cancelled, reason, firstClear: false);
        this.End(now, false, reason);
        return running ? [new Command(CommandKind.StopExecutor)] : [];
    }

    private void Enter(SessionPhase phase, DateTime now, string status)
    {
        this.Phase = phase;
        this.phaseSince = now;
        this.Status = status;
    }

    private void Finish(Observation o, RunOutcome outcome, string detail, bool insideBoard)
    {
        var board = this.Board!;
        bool firstClear = !this.wasCleared && o.ClearedBoards.Contains(board.Number);
        if (firstClear && outcome != RunOutcome.Finished)
        {
            // The game's own record outranks the result-screen guess.
            outcome = RunOutcome.Finished;
            detail += " The game reports the board cleared.";
        }
        this.Record(o.Now, outcome, detail, firstClear);

        if (outcome == RunOutcome.Finished)
        {
            this.Attempt = 0;
            if (++this.done >= this.queue[this.entry].Runs)
            {
                this.entry++;
                this.done = 0;
            }
            if (insideBoard)
                this.End(o.Now, false, $"Stopped with the character still inside the {board.Name}: {detail}");
            else if (this.entry >= this.queue.Count)
                this.End(o.Now, true, "Every queued run finished.");
            else
                this.Wait(o.Now);
            return;
        }

        if (insideBoard)
            this.End(o.Now, false, $"Stopped with the character still inside the {board.Name}: {detail}");
        else if (this.Attempt >= this.settings.MaxAttempts)
            this.End(o.Now, false, $"Gave up on the {board.Name} after {this.Attempt} attempt(s): {detail}");
        else
            this.Wait(o.Now);
    }

    private void Record(DateTime now, RunOutcome outcome, string detail, bool firstClear) =>
        this.runs.Add(new RunRecord(this.runStarted, now - this.runStarted, this.Board!.Number, this.Attempt, outcome, firstClear, detail));

    private void Wait(DateTime now)
    {
        this.nextStart = now + this.settings.PauseBetweenRuns;
        this.Enter(SessionPhase.Waiting, now, "Between runs");
    }

    private void End(DateTime now, bool goalReached, string ending)
    {
        this.Enter(SessionPhase.Ended, now, ending);
        this.Report = new SessionReport(this.StartedUtc, now, this.runs.ToList(), goalReached, ending);
    }

    private static string Minutes(TimeSpan t) => t.TotalMinutes >= 1 ? $"{t.TotalMinutes:0.#} min" : $"{t.TotalSeconds:0} s";
}
