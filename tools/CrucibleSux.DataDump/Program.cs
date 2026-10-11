using Lumina.Excel;
using Lumina.Excel.Sheets;

// Usage: CrucibleSux.DataDump --game "<FFXIV install>\game\sqpack"
// Prints the Crucible's sheets and the IDs Core pins, for a human to read on patch day. It reads the stable XBM* sheet
// types (UnknownN columns): the named ones in Lumina.Excel.Sheets.Experimental are flagged PendingExcelSchema. What each
// column means is written down in docs/data/xbm-columns.md.
string? sqpack = null;
for (int i = 0; i < args.Length - 1; i++)
    if (args[i] == "--game")
        sqpack = args[i + 1];
sqpack ??= Environment.GetEnvironmentVariable("CRUCIBLESUX_GAME_PATH");
if (sqpack is null || !Directory.Exists(sqpack))
{
    Console.Error.WriteLine("usage: CrucibleSux.DataDump --game \"<FFXIV install>\\game\\sqpack\" (or set CRUCIBLESUX_GAME_PATH)");
    return 1;
}

var data = new Lumina.GameData(sqpack);
Console.WriteLine($"# game {File.ReadAllText(Path.Combine(sqpack, "..", "ffxivgame.ver")).Trim()}");

ExcelSheet<T> Sheet<T>() where T : struct, IExcelRow<T> => data.GetExcelSheet<T>() ?? throw new InvalidOperationException($"sheet {typeof(T).Name} not found");
SubrowExcelSheet<T> Subsheet<T>() where T : struct, IExcelSubrow<T> => data.GetSubrowExcelSheet<T>() ?? throw new InvalidOperationException($"sheet {typeof(T).Name} not found");
var cfcs = Sheet<ContentFinderCondition>();
var pets = Sheet<Pet>();
var actions = Sheet<Lumina.Excel.Sheets.Action>();
var bnpcNames = Sheet<BNpcName>();

Console.WriteLine("\n## XBMContent (board): u0..u32 bonus points, u33 cfc, u34 team, u35 recRank, u36 syncRank");
foreach (var r in Sheet<XBMContent>())
{
    var cfc = cfcs.GetRowOrDefault(r.Unknown33);
    Console.WriteLine($"{r.RowId}: cfc={r.Unknown33} '{cfc?.Name}' territory={cfc?.TerritoryType.RowId} lvl={cfc?.ClassJobLevelRequired} ilvl={cfc?.ItemLevelRequired} " +
                      $"team={r.Unknown34} recRank={r.Unknown35} syncRank={r.Unknown36} " +
                      $"bonus=[{r.Unknown0},{r.Unknown1},{r.Unknown2},{r.Unknown3},{r.Unknown4},{r.Unknown5},{r.Unknown6},{r.Unknown7},{r.Unknown8},{r.Unknown9},{r.Unknown10},...]");
}

// The stable XBMPet type's column hash no longer matches the game (Lumina refuses it), so XBMPet is read raw.
var petSheet = data.Excel.GetSheet<RawRow>(name: "XBMPet");
Console.WriteLine("\n## XBMPet columns: " + string.Join(", ", petSheet.Columns.Select((c, i) => $"{i}:{c.Type}@{c.Offset}")));
Console.WriteLine("## XBMPet (beast), every column in order");
foreach (var r in petSheet)
{
    var cells = petSheet.Columns.Select((c, i) => c.Type == Lumina.Data.Structs.Excel.ExcelColumnDataType.String ? $"'{Trim(r.ReadStringColumn(i).ExtractText())}'" : r.ReadColumn(i).ToString());
    Console.WriteLine($"{r.RowId}: {string.Join(" | ", cells)}");
}

static string Trim(string s) => s.Length > 40 ? s[..40] + "…" : s;

Console.WriteLine("\n## XBMPet location (col 6 key, col 7 value) read as each candidate sheet");
var placeNames = Sheet<PlaceName>();
var territories = Sheet<TerritoryType>();
var maps = Sheet<Map>();
foreach (var r in petSheet)
{
    byte key = r.ReadUInt8Column(6);
    ushort loc = r.ReadUInt16Column(7);
    Console.WriteLine($"{r.RowId}: key={key} loc={loc} place='{placeNames.GetRowOrDefault(loc)?.Name}' " +
                      $"territory='{territories.GetRowOrDefault(loc)?.PlaceName.ValueNullable?.Name}' map='{maps.GetRowOrDefault(loc)?.PlaceName.ValueNullable?.Name}'");
}

Console.WriteLine("\n## Pet (first 60)");
foreach (var p in pets.Take(60))
    Console.WriteLine($"{p.RowId}: {p.Name}");
_ = actions;

Console.WriteLine("\n## XBMPetParamGrow (subrow = rank - 1): STR/INT/PR/MR/CON at rank 1 and the last rank");
foreach (var row in Subsheet<XBMPetParamGrow>())
{
    var a = row[0];
    var z = row[row.Count - 1];
    Console.WriteLine($"{row.RowId}: ranks={row.Count} r1={a.Unknown0}/{a.Unknown1}/{a.Unknown2}/{a.Unknown3}/{a.Unknown4} rMax={z.Unknown0}/{z.Unknown1}/{z.Unknown2}/{z.Unknown3}/{z.Unknown4}");
}

Console.WriteLine("\n## XBMElement");
foreach (var r in Sheet<XBMElement>())
    Console.WriteLine($"{r.RowId}: {r.Unknown0}");

Console.WriteLine("\n## XBMContentBattle (row.subrow -> battle detail row)");
foreach (var row in Subsheet<XBMContentBattle>())
    Console.WriteLine($"{row.RowId}: [{string.Join(',', row.Select(b => b.Unknown0))}]");

Console.WriteLine("\n## XBMBattleDetail: u0 name, u1 ?, u2/u3 actions, u4 resist, u5..u9 ?, u10 element");
foreach (var row in Subsheet<XBMBattleDetail>())
    foreach (var d in row)
        Console.WriteLine($"{row.RowId}.{d.SubrowId}: name={d.Unknown0} '{bnpcNames.GetRowOrDefault(d.Unknown0)?.Singular}' u1={d.Unknown1} act={d.Unknown2}/{d.Unknown3} resist={d.Unknown4} " +
                          $"u5..9={d.Unknown5},{d.Unknown6},{d.Unknown7},{d.Unknown8},{d.Unknown9} element={d.Unknown10}");

Console.WriteLine("\n## BNpcResist rows used by the battles");
var resists = Sheet<BNpcResist>();
foreach (uint id in Subsheet<XBMBattleDetail>().SelectMany(row => row).Select(d => (uint)d.Unknown4).Distinct().Order())
    if (resists.TryGetRow(id, out var res))
        Console.WriteLine($"{id}: {string.Join(' ', typeof(BNpcResist).GetProperties().Where(p => p.Name.StartsWith("Unknown")).Select(p => $"{p.Name[7..]}={p.GetValue(res)}"))}");

Console.WriteLine("\n## XBMItemType");
foreach (var r in Sheet<XBMItemType>())
    Console.WriteLine($"{r.RowId}: {r.Unknown0}");

Console.WriteLine("\n## XBMItem: u2 name, u4 short description, u11 icon, u12 price, u13 type");
foreach (var r in Sheet<XBMItem>())
    Console.WriteLine($"{r.RowId}: type={r.Unknown13} '{r.Unknown2}' price={r.Unknown12} :: {r.Unknown4}");

Console.WriteLine("\n## XBMScoreRank");
foreach (var r in Sheet<XBMScoreRank>())
    Console.WriteLine($"{r.RowId}: {r.Unknown0}");

Console.WriteLine("\n## XBMScoreBonus: name, description, always active");
foreach (var r in Sheet<XBMScoreBonus>())
    Console.WriteLine($"{r.RowId}: {string.Join(" | ", typeof(XBMScoreBonus).GetProperties().Where(p => p.Name.StartsWith("Unknown")).Select(p => p.GetValue(r)))}");

Console.WriteLine("\n## ClassJob 43");
var bst = Sheet<ClassJob>().GetRow(43);
Console.WriteLine($"{bst.Abbreviation} '{bst.Name}' expIndex={bst.ExpArrayIndex} category={bst.ClassJobCategory.RowId} unlockQuest={bst.UnlockQuest.RowId}");

Console.WriteLine("\n## Beastmaster quests (ClassJobCategory 203) and quests named like the Crucible's");
string[] names = ["Into the Crucible", "Gobsmacked", "A Beastmaster's Path", "Free for All", "Mastery Rematch", "Blazing Trails", "Bonds Unbroken", "Master of the Lhu"];
foreach (var q in Sheet<Quest>())
{
    string name = q.Name.ExtractText();
    if (q.ClassJobCategory0.RowId == 203 || names.Any(n => name.Equals(n, StringComparison.OrdinalIgnoreCase)))
        Console.WriteLine($"{q.RowId}: '{name}' lvl={q.ClassJobLevel[0]} cat={q.ClassJobCategory0.RowId} prev={string.Join(',', q.PreviousQuest.Select(p => p.RowId).Where(i => i != 0))}");
}

Console.WriteLine("\n## Achievements 4020-4080");
var ach = Sheet<Achievement>();
for (uint id = 4020; id <= 4080; id++)
    if (ach.TryGetRow(id, out var a) && !a.Name.IsEmpty)
        Console.WriteLine($"{id}: '{a.Name}' :: {a.Description.ExtractText()} (type={a.Type} key={a.Key.RowId} data=[{string.Join(',', a.Data.Select(d => d.RowId).Where(x => x != 0))}])");

Console.WriteLine("\n## Items: remnants, gourds, Beastmaster gear (ClassJobCategory 203)");
foreach (var it in Sheet<Item>())
{
    string name = it.Name.ExtractText();
    if (name.Contains("Remnant", StringComparison.OrdinalIgnoreCase) || name.Contains("Kornago", StringComparison.OrdinalIgnoreCase))
        Console.WriteLine($"{it.RowId}: '{name}' ilvl={it.LevelItem.RowId}");
    else if (it.ClassJobCategory.RowId == 203 && it.EquipSlotCategory.RowId != 0)
        Console.WriteLine($"gear {it.RowId}: '{name}' ilvl={it.LevelItem.RowId} lvl={it.LevelEquip} slot={it.EquipSlotCategory.RowId}");
}

return 0;
