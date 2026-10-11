namespace CrucibleSux.Core;

/// <summary>One line of a SpecialShop: the item, and the quest that must be complete before the shop lists it (0 for none).</summary>
public readonly record struct ShopLine(uint Item, uint Quest);

/// <summary>
/// The Kornago merchant's two exchange shops (ENpc 1059408 near Bentbranch; SpecialShop 1771019 and 1771020, game
/// 2026.09.15). The shop window lists only the lines whose quest is complete, so a line's position in the window depends on
/// the player's quests. Nothing here has been checked in game: the plugin confirms the exchange dialog names the item before
/// it buys anything.
/// </summary>
public static class KornagoShop
{
    public const uint Merchant = 1059408;
    public const uint FadedShop = 1771019;
    public const uint BrightShop = 1771020;

    /// <summary>The menu entries the merchant offers, as the SpecialShop names read in an English client.</summary>
    public const string FadedShopName = "Faded Remnants of Resilience";
    public const string BrightShopName = "Bright Remnants of Resilience";

    /// <summary>Where the merchant stands in the Central Shroud (Level sheet, rounded).</summary>
    public static readonly Spot Spot = new(CrucibleIds.LaudaTerritory, 19.8f, -6.0f, 55.0f, Merchant, 4);

    private const uint Q30 = CrucibleIds.Quests.IntoTheCrucible;
    private const uint Q32 = CrucibleIds.Quests.Gobsmacked;
    private const uint Q34 = CrucibleIds.Quests.ABeastmastersPath;
    private const uint Q36 = CrucibleIds.Quests.FreeForAll;
    private const uint Q37 = CrucibleIds.Quests.MasteryRematch;

    public static readonly IReadOnlyList<ShopLine> Faded =
    [
        new(49806, Q30), new(49807, Q30), new(49808, Q30), new(49809, Q30), new(49810, Q30), new(49811, Q32), new(49812, Q32),
        new(51744, Q30), new(51748, Q30), new(52446, Q32), new(51747, Q32), new(CrucibleIds.BrightRemnant, Q34), new(51994, Q30),
    ];

    public static readonly IReadOnlyList<ShopLine> Bright =
    [
        new(49813, Q34), new(49814, Q34), new(49815, Q34), new(49816, Q34), new(49817, Q34), new(49818, Q34), new(49819, Q34),
        new(49820, Q34), new(CrucibleIds.FadedRemnant, Q34),
        .. Enumerable.Range(50749, 7).Select(i => new ShopLine((uint)i, Q34)),
        .. Enumerable.Range(50756, 14).Select(i => new ShopLine((uint)i, Q36)),
        .. Enumerable.Range(50770, 9).Select(i => new ShopLine((uint)i, Q37)),
        new(52369, Q34), new(52370, Q34), new(52371, Q34),
    ];

    /// <summary>The shop that sells <paramref name="item"/>, paid in <paramref name="currency"/>.</summary>
    public static (uint Shop, string Name, IReadOnlyList<ShopLine> Lines) For(uint currency) =>
        currency == CrucibleIds.BrightRemnant ? (BrightShop, BrightShopName, Bright) : (FadedShop, FadedShopName, Faded);

    /// <summary>The line's index in the shop window as the player sees it, or -1 when the window does not list it.</summary>
    public static int Position(IReadOnlyList<ShopLine> lines, uint item, Func<uint, bool> questComplete)
    {
        int shown = 0;
        foreach (var line in lines)
        {
            bool listed = line.Quest == 0 || questComplete(line.Quest);
            if (line.Item == item)
                return listed ? shown : -1;
            if (listed)
                shown++;
        }
        return -1;
    }
}
