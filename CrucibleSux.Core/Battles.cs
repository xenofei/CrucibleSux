namespace CrucibleSux.Core;

/// <summary>
/// The five 1-5 ratings XBMBattleDetail gives an enemy (columns 5-9). Read here as Strength, Intelligence, Physical
/// Resistance, Magical Resistance and Constitution, matching the beast attributes; that reading is unverified in game.
/// </summary>
public readonly record struct EnemyRatings(int Strength, int Intelligence, int PhysicalResistance, int MagicalResistance, int Constitution);

/// <summary>One enemy of a Crucible battle (one XBMBattleDetail subrow).</summary>
/// <param name="ResistMask">Bit i set when the enemy's BNpcResist row resists status i (same order as <see cref="BeastInfo.InflictsMask"/>).</param>
public sealed record EnemyInfo(uint NameId, string Name, Element Element, EnemyRatings Ratings, ushort ResistMask);

/// <summary>One battle of a board (an XBMContentBattle subrow): the boss battle is the board's first subrow.</summary>
public sealed record BattleInfo(int Board, int Index, bool IsBoss, IReadOnlyList<EnemyInfo> Enemies);
