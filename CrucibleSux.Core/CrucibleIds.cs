namespace CrucibleSux.Core;

/// <summary>
/// Game IDs the plugin pins, read from game 2026.09.15.0000.0000 with tools/CrucibleSux.DataDump. Refresh them on patch
/// day (docs/data/xbm-columns.md); the GameData tests compare them with the sheets when a game install is available.
/// </summary>
public static class CrucibleIds
{
    public const uint BeastmasterJob = 43;

    /// <summary>Central Shroud, where Lauda admits players to the Crucible (near Bentbranch Meadows).</summary>
    public const uint LaudaTerritory = 148;

    public const int TotalBeasts = 50;
    public const int MaxRank = 25;

    /// <summary>Beast EXP a single familiar can earn on one board (Crucible of the Unbroken wiki, 7.56).</summary>
    public const int MaxExpPerBoard = 255;

    public const uint FadedRemnant = 51734;
    public const uint BrightRemnant = 51735;

    public static class Quests
    {
        public const uint StrangersInTheWood = 71026;
        public const uint MasterOfTheLhu = 71029;
        public const uint IntoTheCrucible = 71030;
        public const uint Gobsmacked = 71032;
        public const uint ABeastmastersPath = 71034;
        public const uint BondsUnbroken = 71035;
        public const uint FreeForAll = 71036;
        public const uint MasteryRematch = 71037;

        /// <summary>Unlocks degrees and the rankings after the Second Master's Board.</summary>
        public const uint BlazingTrails = 71045;
    }

    public static class Achievements
    {
        /// <summary>Wild Child I-V: Beastmaster level 10/20/30/40/50.</summary>
        public static readonly uint[] JobLevel = [4023, 4024, 4025, 4026, 4027];

        /// <summary>A Beast in the Hand I-V: befriend 10/20/30/40/50 beasts.</summary>
        public static readonly uint[] Befriended = [4028, 4029, 4030, 4031, 4032];

        /// <summary>Friend to the Fierce I-V: rank 25 with 10/20/30/40/50 beasts.</summary>
        public static readonly uint[] Rank25 = [4056, 4057, 4058, 4059, 4060];

        public const uint FreeingTheBeast = 4053;
        public const uint CrucibleMaster = 4054;
        public const uint UnbrokenEndgame = 4055;

        /// <summary>Beastkin..Ashkin Mate: each kin's Gambit bonus on the Second Master's Board, in <see cref="Kin"/> order.</summary>
        public static readonly uint[] KinMates = [4061, 4062, 4063, 4064, 4065, 4066, 4067, 4068];

        /// <summary>Beast of the Best, Running Feral, Crucible Contender.</summary>
        public static readonly uint[] Rankings = [4075, 4076, 4077];
    }
}
