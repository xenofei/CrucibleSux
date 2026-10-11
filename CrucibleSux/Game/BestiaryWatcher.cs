using System;
using System.Collections.Generic;
using System.Linq;
using CrucibleSux.Core;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Text.ReadOnly;

namespace CrucibleSux.Game;

/// <summary>
/// Reads a beast's page in the Master's Bestiary (XBMMonsterBookDetail) when the player opens it: the attributes give
/// the rank (<see cref="RankEstimator"/>; the page shows no rank the client exposes) and the EXP line the EXP. What it
/// reads is kept per character in the configuration, so ranks survive restarts. Unverified in game: the node texts'
/// format and whether the page shows the attributes at the beast's current rank.
/// </summary>
public sealed class BestiaryWatcher : IDisposable
{
    public const string AddonName = "XBMMonsterBookDetail";

    private readonly IAddonLifecycle lifecycle;
    private readonly IPlayerState player;
    private readonly IPluginLog log;
    private readonly Configuration config;
    private readonly Action save;
    private readonly Dictionary<string, BeastInfo> byName;
    private string lastPage = string.Empty;
    private bool warned;

    /// <summary>The last page read, for the Beasts tab's "last seen" line.</summary>
    public string? LastRead { get; private set; }

    public BestiaryWatcher(IAddonLifecycle lifecycle, IPlayerState player, IPluginLog log, Configuration config, Action save, IEnumerable<BeastInfo> beasts)
    {
        this.lifecycle = lifecycle;
        this.player = player;
        this.log = log;
        this.config = config;
        this.save = save;
        this.byName = beasts.GroupBy(b => b.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        lifecycle.RegisterListener(AddonEvent.PostUpdate, AddonName, this.OnUpdate);
    }

    public void Dispose() => this.lifecycle.UnregisterListener(AddonEvent.PostUpdate, AddonName, this.OnUpdate);

    private unsafe void OnUpdate(AddonEvent type, AddonArgs args)
    {
        try
        {
            var addon = (AddonXBMMonsterBookDetail*)args.Addon.Address;
            if (addon == null || !addon->AtkUnitBase.IsVisible)
                return;

            string name = Text(addon->EntryName);
            int?[] stats =
            [
                Number(Text(addon->EntryStrengthCount)), Number(Text(addon->EntryIngelligenceCount)),
                Number(Text(addon->EntryPhysicalResistanceCount)), Number(Text(addon->EntryMagicalResistanceCount)),
                Number(Text(addon->EntryConstitutionCount)),
            ];
            string expText = Text(addon->EntryExpCount);
            string page = $"{name}|{string.Join(',', stats)}|{expText}";
            if (page == this.lastPage)
                return;
            this.lastPage = page;

            if (!this.byName.TryGetValue(name.Trim(), out var beast) || stats.Any(s => s is null) || this.player.ContentId == 0)
                return;

            var shown = new BeastStats(stats[0]!.Value, stats[1]!.Value, stats[2]!.Value, stats[3]!.Value, stats[4]!.Value);
            this.Record(beast, RankEstimator.FromStats(beast, shown), Number(expText));
        }
        catch (Exception ex)
        {
            if (!this.warned)
            {
                this.warned = true;
                this.log.Warning(ex, "The Bestiary page could not be read; beast ranks stay as last seen");
            }
        }
    }

    private void Record(BeastInfo beast, int? rank, int? exp)
    {
        if (!this.config.Characters.TryGetValue(this.player.ContentId, out var beasts))
            this.config.Characters[this.player.ContentId] = beasts = [];

        var saved = beasts.GetValueOrDefault(beast.Id);
        // An unreadable rank keeps the last one seen rather than forgetting it.
        int? newRank = rank ?? saved?.Rank;
        if (saved is not null && saved.Rank == newRank && saved.Exp == exp)
            return;

        beasts[beast.Id] = new SavedBeast { Rank = newRank, Exp = exp, RankIsEstimated = true, SeenUtc = DateTime.UtcNow };
        this.LastRead = rank is null ? $"{beast.Name}: rank not recognised" : $"{beast.Name}: rank {rank}";
        this.save();
    }

    private static unsafe string Text(AtkTextNode* node) =>
        node == null ? string.Empty : new ReadOnlySeStringSpan(node->NodeText.AsSpan()).ExtractText();

    /// <summary>The first whole number in a text ("1,234/2,000" reads 1234), or null when it has none.</summary>
    private static int? Number(string text)
    {
        int value = 0;
        bool any = false;
        foreach (char c in text)
        {
            if (char.IsAsciiDigit(c))
            {
                value = checked(value * 10 + (c - '0'));
                any = true;
            }
            else if (any && c is not (',' or '.' or ' '))
            {
                break;
            }
        }
        return any ? value : null;
    }
}
