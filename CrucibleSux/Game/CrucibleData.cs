using System;
using System.Collections.Generic;
using System.Linq;
using CrucibleSux.Core;
using Dalamud.Plugin.Services;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace CrucibleSux.Game;

/// <summary>
/// The Crucible's sheets read into Core's models, once at load. Column meanings are in docs/data/xbm-columns.md. The
/// stable XBM* types are used (UnknownN columns) because the named ones in Lumina.Excel.Sheets.Experimental carry the
/// PendingExcelSchema error; XBMPet is read raw because its stable type's column hash no longer matches the game.
/// </summary>
public sealed class CrucibleData
{
    private const int StatusFlags = 11;

    public IReadOnlyList<BeastInfo> Beasts { get; }

    /// <summary>Where each beast is befriended, as the player would read it; empty when the sheet gives no place.</summary>
    public IReadOnlyDictionary<uint, string> Locations { get; }

    /// <summary>XBMPet's own description of each beast (column 8); the hint for beasts with no place.</summary>
    public IReadOnlyDictionary<uint, string> Descriptions { get; }

    /// <summary>Each board's battles, boss battle first.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<BattleInfo>> Battles { get; }

    public CrucibleData(IDataManager data)
    {
        var petNames = data.GetExcelSheet<Pet>();
        var places = data.GetExcelSheet<PlaceName>();
        var grow = data.GetSubrowExcelSheet<XBMPetParamGrow>();
        var xbmPet = data.Excel.GetSheet<RawRow>(name: "XBMPet");

        var beasts = new List<BeastInfo>();
        var locations = new Dictionary<uint, string>();
        var descriptions = new Dictionary<uint, string>();
        foreach (var r in xbmPet)
        {
            int pet = r.ReadInt32Column(0);
            if (pet <= 0)
                continue;

            var growRows = grow.TryGetRow(r.ReadUInt8Column(2), out var g)
                ? g.Select(s => new BeastStats(s.Unknown0, s.Unknown1, s.Unknown2, s.Unknown3, s.Unknown4)).ToList()
                : [];
            ushort inflicts = 0;
            for (int i = 0; i < StatusFlags; i++)
            {
                if (r.ReadBoolColumn(11 + i))
                    inflicts |= (ushort)(1 << i);
            }

            var name = petNames.GetRowOrDefault((uint)pet)?.Name.ExtractText() ?? $"Beast {r.RowId}";
            beasts.Add(new BeastInfo(
                r.RowId,
                Capitalize(name),
                (Kin)r.ReadUInt8Column(1),
                r.ReadUInt32Column(4),
                r.ReadUInt8Column(3),
                growRows,
                Enumerable.Range(22, 5).Select(c => (int)r.ReadUInt8Column(c)).ToList(),
                inflicts));

            // Location key 1: column 7 is a PlaceName row (the zone). Key 2 marks the special beasts, whose source is
            // not decoded yet (docs/data/xbm-columns.md); their description says how to get them.
            if (r.ReadUInt8Column(6) == 1 && places.GetRowOrDefault(r.ReadUInt16Column(7)) is { } place)
                locations[r.RowId] = place.Name.ExtractText();
            descriptions[r.RowId] = r.ReadStringColumn(8).ExtractText();
        }

        this.Beasts = beasts;
        this.Locations = locations;
        this.Descriptions = descriptions;
        this.Battles = ReadBattles(data);
    }

    private static Dictionary<int, IReadOnlyList<BattleInfo>> ReadBattles(IDataManager data)
    {
        var contentBattles = data.GetSubrowExcelSheet<XBMContentBattle>();
        var details = data.GetSubrowExcelSheet<XBMBattleDetail>();
        var resists = data.GetExcelSheet<BNpcResist>();
        var names = data.GetExcelSheet<BNpcName>();

        var result = new Dictionary<int, IReadOnlyList<BattleInfo>>();
        foreach (var board in Boards.All)
        {
            if (!contentBattles.TryGetRow((uint)board.Number, out var row))
                continue;

            var battles = new List<BattleInfo>();
            for (int i = 0; i < row.Count; i++)
            {
                uint detailRow = row[i].Unknown0;
                if (!details.TryGetRow(detailRow, out var enemies))
                    continue;

                var list = enemies
                    .Where(e => e.Unknown0 != 0)
                    .Select(e => new EnemyInfo(
                        e.Unknown0,
                        Capitalize(names.GetRowOrDefault(e.Unknown0)?.Singular.ExtractText() ?? $"Enemy {e.Unknown0}"),
                        (Element)e.Unknown10,
                        new EnemyRatings(e.Unknown5, e.Unknown6, e.Unknown7, e.Unknown8, e.Unknown9),
                        resists.GetRowOrDefault(e.Unknown4) is { } res ? ResistMask(res) : (ushort)0))
                    .ToList();
                battles.Add(new BattleInfo(board.Number, i, i == 0, list));
            }
            result[board.Number] = battles;
        }
        return result;
    }

    private static ushort ResistMask(BNpcResist r)
    {
        bool[] flags = [r.Unknown0, r.Unknown1, r.Unknown2, r.Unknown3, r.Unknown4, r.Unknown5, r.Unknown6, r.Unknown7, r.Unknown8, r.Unknown9, r.Unknown10];
        ushort mask = 0;
        for (int i = 0; i < flags.Length; i++)
        {
            if (flags[i])
                mask |= (ushort)(1 << i);
        }
        return mask;
    }

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
