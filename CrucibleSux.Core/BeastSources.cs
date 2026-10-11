namespace CrucibleSux.Core;

/// <summary>Where a wild beast roams, in the map coordinates the game's map shows.</summary>
public readonly record struct Spawn(uint Territory, float MapX, float MapY)
{
    /// <summary>World X for <see cref="MapX"/>; every capture zone's map has scale 100 and no offset.</summary>
    public float WorldX => ToWorld(this.MapX);

    /// <summary>World Z for <see cref="MapY"/>. The height is not known; ask the navmesh for the floor there.</summary>
    public float WorldZ => ToWorld(this.MapY);

    public static float ToWorld(float map) => ((map - 1) * 2048f / 41f) - 1024f;

    public static float ToMap(float world) => (41f * (world + 1024f) / 2048f) + 1;
}

/// <summary>
/// How a beast is befriended: captured in the open world, or a gourd bought from the Kornago merchant (or both).
/// </summary>
/// <param name="Beast">The XBMPet row.</param>
/// <param name="MinLevel">The lowest level of its wild form; Capture needs the target at or below the player's level.</param>
/// <param name="WildNames">BNpcName rows that count as this beast, most common first. The game has no mob-to-beast link in
/// its sheets, so these come from the wiki's Master's Bestiary, checked by name against the BNpcName sheet.</param>
/// <param name="Spawns">Where to look, best first.</param>
/// <param name="Gourd">The gourd item, 0 when there is none.</param>
/// <param name="Price">What the gourd costs; 0 when it is not sold (the Cu Sith's comes from a quest).</param>
/// <param name="GourdQuest">The quest that puts the gourd in the shop, or that rewards it.</param>
public sealed record BeastSource(
    uint Beast,
    string Name,
    Kin Kin,
    int MinLevel,
    IReadOnlyList<uint> WildNames,
    IReadOnlyList<Spawn> Spawns,
    uint Gourd = 0,
    int Price = 0,
    uint Currency = 0,
    uint GourdQuest = 0)
{
    public bool CanCapture => this.WildNames.Count > 0;

    public bool IsSold => this.Price > 0;
}

/// <summary>
/// All 50 beasts and where each comes from (docs/data/bst-progression.md, docs/research/08-beast-collection.md). Kin, zone
/// and gourds come from the game sheets (game 2026.09.15). Wild names and levels come from the wiki. Spawn points are the
/// centres of Teamcraft's recorded positions where it has them (MIT, ffxiv-teamcraft monsters.json); otherwise they are the
/// wiki's map coordinates (Teamcraft has none for the Central Shroud). None of this has been checked in game.
/// </summary>
public static class BeastSources
{
    public const uint CentralShroud = 148;
    public const uint SouthShroud = 153;
    public const uint NorthShroud = 154;
    public const uint MiddleLaNoscea = 134;
    public const uint LowerLaNoscea = 135;
    public const uint EasternLaNoscea = 137;
    public const uint WesternLaNoscea = 138;
    public const uint UpperLaNoscea = 139;
    public const uint WesternThanalan = 140;
    public const uint CentralThanalan = 141;
    public const uint SouthernThanalan = 146;
    public const uint MorDhona = 156;

    private const uint Faded = CrucibleIds.FadedRemnant;
    private const uint Bright = CrucibleIds.BrightRemnant;
    private const uint FadedShop = CrucibleIds.Quests.IntoTheCrucible;
    private const uint FadedShopLater = CrucibleIds.Quests.Gobsmacked;
    private const uint BrightShop = CrucibleIds.Quests.ABeastmastersPath;

    private static BeastSource Wild(uint beast, string name, Kin kin, int level, uint[] names, params Spawn[] spawns) =>
        new(beast, name, kin, level, names, spawns);

    private static BeastSource Bought(uint beast, string name, Kin kin, uint gourd, int price, uint currency, uint quest) =>
        new(beast, name, kin, 0, [], [], gourd, price, currency, quest);

    private static Spawn S(uint territory, float x, float y) => new(territory, x, y);

    public static readonly IReadOnlyList<BeastSource> All =
    [
        new(1, "Cu Sith", Kin.Beastkin, 0, [], [], QuestScripts.CuSithGourd, 0, 0, CrucibleIds.Quests.StrangersInTheWood),
        Wild(2, "squirrel", Kin.Beastkin, 1, [37], S(CentralShroud, 24, 16), S(NorthShroud, 29.2f, 26.7f)),
        Wild(3, "lamb", Kin.Beastkin, 3, [392], S(MiddleLaNoscea, 23.7f, 25.4f)),
        Wild(4, "pugil", Kin.Wavekin, 4, [640], S(MiddleLaNoscea, 21.1f, 18.7f)),
        Wild(5, "opo-opo", Kin.Beastkin, 5, [5], S(NorthShroud, 27.9f, 24.3f)),
        Wild(6, "dodo", Kin.Cloudkin, 4, [393], S(LowerLaNoscea, 30.8f, 17.9f)),
        Wild(7, "coblyn", Kin.Soulkin, 6, [276, 1041, 277], S(WesternThanalan, 20.3f, 28.1f), S(WesternThanalan, 21.6f, 22.4f)),
        Wild(8, "diremite", Kin.Vilekin, 10, [10], S(CentralShroud, 20, 19)),
        Wild(9, "megalocrab", Kin.Wavekin, 10, [561], S(MiddleLaNoscea, 15.4f, 14.5f)),
        Wild(10, "wespe", Kin.Vilekin, 1, [632, 641], S(CentralThanalan, 22.8f, 28.8f)),
        Wild(11, "vulture", Kin.Cloudkin, 6, [299, 1039, 13], S(WesternThanalan, 21.1f, 25.7f), S(WesternThanalan, 22.3f, 18.2f)),
        Wild(12, "mandragora", Kin.Seedkin, 5, [405, 107], S(MiddleLaNoscea, 21.6f, 17.1f)),
        Wild(13, "geshunpest", Kin.Ashkin, 14, [7466], S(CentralShroud, 19, 28)),
        Wild(14, "puk", Kin.Scalekin, 4, [401], S(MiddleLaNoscea, 20.5f, 19.7f)),
        Wild(15, "crab", Kin.Wavekin, 13, [636], S(WesternThanalan, 15.9f, 16.5f)) with { Gourd = 49806, Price = 10, Currency = Faded, GourdQuest = FadedShop },
        Wild(16, "mantis", Kin.Vilekin, 16, [644], S(WesternLaNoscea, 22.5f, 22.3f)),
        Bought(17, "slime", Kin.Ashkin, 49807, 20, Faded, FadedShop),
        Bought(18, "dullahan", Kin.Soulkin, 49808, 20, Faded, FadedShop),
        Wild(19, "bat", Kin.Cloudkin, 7, [364], S(LowerLaNoscea, 26.8f, 15.6f)),
        Wild(20, "flying trap", Kin.Seedkin, 10, [22], S(CentralShroud, 23, 26)),
        Wild(21, "ziz", Kin.Scalekin, 16, [1181], S(WesternLaNoscea, 24.5f, 23.6f)),
        Wild(22, "sabotender", Kin.Seedkin, 3, [287, 1045], S(WesternThanalan, 27.2f, 24.8f)),
        Wild(23, "golem", Kin.Soulkin, 29, [280], S(SouthernThanalan, 23.1f, 12.5f)),
        Wild(24, "apkallu", Kin.Cloudkin, 30, [341], S(EasternLaNoscea, 29.2f, 35.7f)),
        Wild(25, "adamantoise", Kin.Scalekin, 12, [244], S(CentralThanalan, 21.2f, 23.9f)),
        Wild(26, "buffalo", Kin.Beastkin, 8, [358], S(MiddleLaNoscea, 18.8f, 17.2f)),
        Wild(27, "uragnite", Kin.Wavekin, 14, [635], S(WesternThanalan, 16.9f, 14.7f)),
        Wild(28, "worm", Kin.Vilekin, 31, [290], S(SouthernThanalan, 18.9f, 34.8f)) with { Gourd = 49809, Price = 15, Currency = Faded, GourdQuest = FadedShop },
        Wild(29, "spriggan", Kin.Soulkin, 7, [317], S(CentralThanalan, 17.5f, 23.5f)),
        Wild(30, "goobbue", Kin.Beastkin, 12, [354], S(LowerLaNoscea, 27.6f, 21.3f)),
        Wild(31, "gigantoad", Kin.Wavekin, 4, [349], S(LowerLaNoscea, 24.5f, 22.5f)),
        Wild(32, "colibri", Kin.Cloudkin, 33, [639], S(EasternLaNoscea, 30.8f, 24.9f)),

        // The sheet places the coeurl in Outer La Noscea; the wiki and Teamcraft both find it in Upper La Noscea.
        Wild(33, "coeurl", Kin.Beastkin, 24, [9911, 28], S(UpperLaNoscea, 8.9f, 21.2f)),
        Wild(34, "raptor", Kin.Scalekin, 9, [120], S(CentralShroud, 31, 21)),
        Wild(35, "drake", Kin.Scalekin, 32, [264, 1024], S(SouthernThanalan, 24.8f, 38.4f)) with { Gourd = 49810, Price = 10, Currency = Faded, GourdQuest = FadedShop },
        Wild(36, "treant", Kin.Seedkin, 12, [128], S(CentralShroud, 27, 15), S(NorthShroud, 26.9f, 25.9f)),
        Bought(37, "antling", Kin.Vilekin, 49811, 20, Faded, FadedShopLater),
        Bought(38, "chimera", Kin.Beastkin, 49812, 30, Faded, FadedShopLater),
        Wild(39, "morbol", Kin.Seedkin, 31, [238], S(CentralShroud, 13, 22), S(SouthShroud, 20.5f, 28.6f)),
        Wild(40, "ghost", Kin.Ashkin, 7, [404], S(MiddleLaNoscea, 20.5f, 18.9f)),
        Wild(41, "salamander", Kin.Wavekin, 6, [196], S(CentralShroud, 26, 21)),
        Wild(42, "cobra", Kin.Scalekin, 45, [1851], S(MorDhona, 26.4f, 13.0f)),
        Bought(43, "hydra", Kin.Scalekin, 49813, 35, Bright, BrightShop),
        Bought(44, "damselfly", Kin.Vilekin, 49814, 35, Bright, BrightShop),
        Bought(45, "rotting goobbue", Kin.Ashkin, 49815, 35, Bright, BrightShop),
        Bought(46, "zu", Kin.Cloudkin, 49816, 35, Bright, BrightShop),
        Bought(47, "ice golem", Kin.Soulkin, 49817, 35, Bright, BrightShop),
        Bought(48, "Karlabos", Kin.Wavekin, 49818, 35, Bright, BrightShop),
        Bought(49, "rafflesia", Kin.Seedkin, 49819, 40, Bright, BrightShop),
        Bought(50, "behemoth", Kin.Beastkin, 49820, 40, Bright, BrightShop),
    ];

    public static BeastSource ById(uint beast) =>
        beast >= 1 && beast <= All.Count ? All[(int)beast - 1] : throw new ArgumentOutOfRangeException(nameof(beast), beast, "not a beast (1-50)");
}
