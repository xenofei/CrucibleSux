using System.Globalization;
using System.Text;

namespace CrucibleSux.Core;

public enum LogKind
{
    Info,

    /// <summary>An enemy started a cast.</summary>
    Cast,

    /// <summary>A cast finished with the player outside its area, or was cut short.</summary>
    Resolve,

    /// <summary>A cast finished with the player inside its area.</summary>
    Hit,
    Hp,
    Status,
    Death,
    Beast,
    Target,
    Move,
    Nav,
    Combat,
    Window,
    Chat,
    Warn,
}

public sealed record LogLine(TimeSpan At, LogKind Kind, string Text, bool Important);

/// <summary>
/// One recording: a board run, or the journey's time in the open world. The player copies <see cref="Summary"/> (short
/// enough for one Discord message) or <see cref="Full"/> and pastes it into a report. It never holds the character's
/// name; the recorder replaces it before a line gets here.
/// </summary>
public sealed class CombatLog
{
    /// <summary>Lines kept per recording; about 2 hours of a busy board.</summary>
    public const int LineCap = 40_000;

    /// <summary>Discord's message limit without Nitro.</summary>
    public const int SummaryChars = 1_900;

    private readonly List<LogLine> lines = [];
    private readonly Dictionary<LogKind, int> counts = [];

    public CombatLog(string title, DateTime startedUtc, IReadOnlyList<string> header)
    {
        this.Title = title;
        this.StartedUtc = startedUtc;
        this.Header = header;
    }

    public string Title { get; }

    public DateTime StartedUtc { get; }

    public DateTime? EndedUtc { get; private set; }

    public IReadOnlyList<string> Header { get; }

    public IReadOnlyList<LogLine> Lines => this.lines;

    public int Count(LogKind kind) => this.counts.GetValueOrDefault(kind);

    public void Add(DateTime nowUtc, LogKind kind, string text, bool important = false)
    {
        if (this.EndedUtc is not null)
            return;
        this.counts[kind] = this.Count(kind) + 1;
        if (this.lines.Count < LineCap)
            this.lines.Add(new LogLine(nowUtc - this.StartedUtc, kind, text, important || kind is LogKind.Hit or LogKind.Death or LogKind.Warn));
        else if (this.lines.Count == LineCap)
            this.lines.Add(new LogLine(nowUtc - this.StartedUtc, LogKind.Warn, $"Log full at {LineCap} lines; later events are only counted.", true));
    }

    public void End(DateTime nowUtc) => this.EndedUtc ??= nowUtc;

    public TimeSpan Duration(DateTime nowUtc) => (this.EndedUtc ?? nowUtc) - this.StartedUtc;

    /// <summary>The header, the counts and the important lines, cut to <paramref name="maxChars"/>.</summary>
    public string Summary(DateTime nowUtc, int maxChars = SummaryChars)
    {
        var sb = new StringBuilder();
        this.Head(sb, nowUtc);
        sb.Append("Counts: ")
          .Append(CultureInfo.InvariantCulture, $"{this.Count(LogKind.Cast)} enemy casts, {this.Count(LogKind.Hit)} ended with you inside, ")
          .Append(CultureInfo.InvariantCulture, $"{this.Count(LogKind.Death)} KOs, {this.Count(LogKind.Hp)} HP changes, ")
          .Append(CultureInfo.InvariantCulture, $"{this.Count(LogKind.Target)} target changes, {this.Count(LogKind.Warn)} warnings\n");

        var important = this.lines.Where(l => l.Important).ToList();
        int shown = 0;
        foreach (var line in important)
        {
            string text = Format(line) + "\n";
            if (sb.Length + text.Length > maxChars - 60)
                break;
            sb.Append(text);
            shown++;
        }
        if (shown < important.Count)
            sb.Append(CultureInfo.InvariantCulture, $"... {important.Count - shown} more; copy the full log for everything.\n");
        return sb.ToString();
    }

    public string Full(DateTime nowUtc)
    {
        var sb = new StringBuilder();
        this.Head(sb, nowUtc);
        foreach (var line in this.lines)
            sb.Append(Format(line)).Append('\n');
        return sb.ToString();
    }

    public static string Format(LogLine line) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)line.At.TotalMinutes:00}:{line.At.Seconds:00}.{line.At.Milliseconds / 100} {line.Kind,-7} {line.Text}");

    private void Head(StringBuilder sb, DateTime nowUtc)
    {
        var d = this.Duration(nowUtc);
        sb.Append(CultureInfo.InvariantCulture, $"Crucible SUX log: {this.Title}, {this.StartedUtc:yyyy-MM-dd HH:mm} UTC, {(int)d.TotalMinutes}m {d.Seconds:00}s")
          .Append(this.EndedUtc is null ? " (still recording)\n" : "\n");
        foreach (var h in this.Header)
            sb.Append(h).Append('\n');
    }
}
