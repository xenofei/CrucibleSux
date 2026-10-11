using System;
using System.Linq;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace CrucibleSux.Automation;

/// <summary>What the current task expects from the game's dialogue windows.</summary>
/// <param name="Quest">The quest's name as the client shows it: a JournalAccept or JournalResult for it is confirmed, and a
/// menu entry carrying it is picked.</param>
/// <param name="Choice">The answer to pick when a menu asks a question (an English client's text).</param>
/// <param name="Yes">Whether a yes/no box whose text contains this is answered "Yes". Null answers none.</param>
public sealed record DialogueExpect(string? Quest = null, string? Choice = null, string? Yes = null);

/// <summary>
/// Answers the dialogue windows a task opens (docs/research/10-quest-engine-apis.md §2): advances Talk, picks a menu entry
/// only when it matches what the task expects, accepts and completes only the expected quest, and answers a yes/no box only
/// when it names what the task is doing. Anything else is left for the player and reported through <see cref="Waiting"/>.
/// </summary>
public sealed unsafe class Dialogue(GameActions game)
{
    private const int JournalAcceptButton = 44;
    private const int JournalResultButton = 37;
    private static readonly TimeSpan ClickEvery = TimeSpan.FromMilliseconds(300);

    private DateTime nextClick;

    /// <summary>A window the task did not expect, which the player must answer; null when there is none.</summary>
    public string? Waiting { get; private set; }

    /// <summary>Handles at most one window per call. Returns true while a dialogue window is open.</summary>
    public bool Tick(DialogueExpect expect)
    {
        var now = DateTime.UtcNow;
        this.Waiting = null;

        if (game.Addon("SelectYesno") is var yesno && yesno != null)
        {
            string text = GameActions.AllText(yesno);
            if (expect.Yes is { } yes && text.Contains(yes, StringComparison.OrdinalIgnoreCase))
                this.Click(now, () => GameActions.Callback(yesno, 0));
            else
                this.Waiting = $"A yes/no box the plugin does not expect: {FirstLine(text)}";
            return true;
        }

        foreach (var (name, icon) in new[] { ("SelectString", false), ("SelectIconString", true) })
        {
            var menu = game.Addon(name);
            if (menu == null)
                continue;
            var entries = GameActions.MenuEntries(menu, icon);
            int pick = Find(entries, expect.Choice);
            if (pick < 0)
                pick = Find(entries, expect.Quest);
            if (pick >= 0)
                this.Click(now, () => GameActions.Callback(menu, pick));
            else
                this.Waiting = $"A menu the plugin does not expect: {string.Join(" / ", entries)}";
            return true;
        }

        foreach (var (name, button) in new[] { ("JournalAccept", JournalAcceptButton), ("JournalResult", JournalResultButton) })
        {
            var journal = game.Addon(name);
            if (journal == null)
                continue;
            if (expect.Quest is { } quest && GameActions.AllText(journal).Contains(quest, StringComparison.OrdinalIgnoreCase))
                this.Click(now, () => GameActions.ClickButton(journal, (uint)button));
            else
                this.Waiting = $"{name} for a quest the plugin is not running.";
            return true;
        }

        if (game.Addon("Talk") is var talk && talk != null)
        {
            this.Click(now, () =>
            {
                GameActions.ClickAnywhere(talk);
                return true;
            });
            return true;
        }
        return false;
    }

    private void Click(DateTime now, Func<bool> click)
    {
        if (now < this.nextClick)
            return;
        this.nextClick = now + ClickEvery;
        click();
    }

    private static int Find(string[] entries, string? wanted) =>
        string.IsNullOrEmpty(wanted) ? -1 : Array.FindIndex(entries, e => e.Contains(wanted, StringComparison.OrdinalIgnoreCase));

    private static string FirstLine(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? string.Empty;
}
