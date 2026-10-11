namespace CrucibleSux.Core;

/// <summary>The eight beast classes, numbered as XBMPet's Classification column and ordered as the Gambit bonuses.</summary>
public enum Kin : byte
{
    Unknown = 0,
    Beastkin = 1,
    Vilekin = 2,
    Cloudkin = 3,
    Seedkin = 4,
    Wavekin = 5,
    Scalekin = 6,
    Soulkin = 7,
    Ashkin = 8,
}

/// <summary>XBMElement rows: six aspects and three physical damage types.</summary>
public enum Element : byte
{
    None = 0,
    Fire = 1,
    Wind = 2,
    Earth = 3,
    Lightning = 4,
    Ice = 5,
    Water = 6,
    Blunt = 7,
    Piercing = 8,
    Slashing = 9,
}

/// <summary>The five beast attributes, in XBMPetParamGrow's column order.</summary>
public readonly record struct BeastStats(int Strength, int Intelligence, int PhysicalResistance, int MagicalResistance, int Constitution)
{
    public bool IsPhysical => this.Strength >= this.Intelligence;
}

/// <summary>
/// A beast as the game data describes it (one XBMPet row).
/// </summary>
/// <param name="Id">The XBMPet row, 1-50.</param>
/// <param name="GrowByRank">XBMPetParamGrow subrows; index 0 is rank 1.</param>
/// <param name="StatPercent">XBMPet's five per-attribute percentages (columns 22-26), in attribute order.</param>
/// <param name="InflictsMask">Bit i set when XBMPet's i-th status flag is set; the same order as BNpcResist's columns.</param>
public sealed record BeastInfo(
    uint Id,
    string Name,
    Kin Kin,
    uint Icon,
    int SatietyMax,
    IReadOnlyList<BeastStats> GrowByRank,
    IReadOnlyList<int> StatPercent,
    ushort InflictsMask)
{
    public int MaxRank => this.GrowByRank.Count;

    /// <summary>
    /// The beast's attributes at <paramref name="rank"/>: the grow row scaled by the beast's percentages, rounded down.
    /// Inferred from the sheets, not yet checked against the game's Bestiary (docs/data/xbm-columns.md).
    /// </summary>
    public BeastStats StatsAt(int rank)
    {
        if (this.GrowByRank.Count == 0)
            return default;
        var g = this.GrowByRank[Math.Clamp(rank, 1, this.GrowByRank.Count) - 1];
        int P(int value, int index) => index < this.StatPercent.Count ? value * this.StatPercent[index] / 100 : value;
        return new BeastStats(P(g.Strength, 0), P(g.Intelligence, 1), P(g.PhysicalResistance, 2), P(g.MagicalResistance, 3), P(g.Constitution, 4));
    }
}

/// <summary>What the plugin knows of one of the player's beasts beyond the unlock flag.</summary>
/// <param name="Rank">The rank, or null when not yet seen in the Bestiary.</param>
/// <param name="RankIsEstimated">True when the rank was worked out from the Bestiary's attribute values.</param>
public sealed record BeastProgress(uint Id, int? Rank, int? Exp, bool RankIsEstimated, DateTime SeenUtc);
