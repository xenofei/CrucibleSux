namespace CrucibleSux.Core;

/// <summary>One beast's score against one battle, with the reasons that make it up.</summary>
public sealed record BeastScore(BeastInfo Beast, int Rank, double Score, IReadOnlyList<string> Reasons);

public sealed record BattlePick(BattleInfo Battle, IReadOnlyList<BeastScore> Beasts);

/// <summary>The roster to bring to a board and the three to send into each battle.</summary>
public sealed record TeamPlan(BoardInfo Board, IReadOnlyList<BeastScore> Roster, IReadOnlyList<BattlePick> Battles);

/// <summary>
/// Experimental team builder. It scores each befriended beast against each battle from the sheets alone:
/// its attributes at its rank (synced to the board), whether its attack type meets the enemy's weaker resistance,
/// whether it can inflict a status the enemy does not resist, and how well it stands up to the enemy's attack type.
/// The enemy-rating reading (<see cref="EnemyRatings"/>) is unverified, so the UI labels these picks as suggestions.
/// </summary>
public static class TeamRecommender
{
    public const int BattleTeam = 3;

    /// <summary>The rank assumed for a befriended beast the plugin has not seen in the Bestiary.</summary>
    public const int UnseenRank = 1;

    public static TeamPlan Recommend(BoardInfo board, IReadOnlyList<BattleInfo> battles, IReadOnlyList<BeastInfo> owned, IReadOnlyDictionary<uint, BeastProgress> progress)
    {
        var picks = new List<BattlePick>();
        var totals = new Dictionary<uint, double>();
        foreach (var battle in battles)
        {
            var scored = owned.Select(b => Score(b, RankOf(b, progress, board), battle))
                .OrderByDescending(s => s.Score).ThenBy(s => s.Beast.Id).ToList();
            foreach (var s in scored)
                totals[s.Beast.Id] = totals.GetValueOrDefault(s.Beast.Id) + s.Score * (battle.IsBoss ? 2 : 1);
            picks.Add(new BattlePick(battle, scored));
        }

        var roster = owned
            .OrderByDescending(b => totals.GetValueOrDefault(b.Id)).ThenBy(b => b.Id)
            .Take(board.TeamSize)
            .Select(b => new BeastScore(b, RankOf(b, progress, board), totals.GetValueOrDefault(b.Id), []))
            .ToList();
        var inRoster = roster.Select(r => r.Beast.Id).ToHashSet();

        var battlePicks = picks
            .Select(p => p with { Beasts = p.Beasts.Where(s => inRoster.Contains(s.Beast.Id)).Take(BattleTeam).ToList() })
            .ToList();
        return new TeamPlan(board, roster, battlePicks);
    }

    /// <summary>The beast's rank on this board: what the Bestiary showed, synced down to the board's sync rank.</summary>
    public static int RankOf(BeastInfo beast, IReadOnlyDictionary<uint, BeastProgress> progress, BoardInfo board) =>
        Math.Min(progress.TryGetValue(beast.Id, out var p) && p.Rank is int r ? r : UnseenRank, board.SyncRank);

    public static BeastScore Score(BeastInfo beast, int rank, BattleInfo battle)
    {
        var s = beast.StatsAt(rank);
        var reasons = new List<string>();
        double attack = Math.Max(s.Strength, s.Intelligence);
        double score = attack + (s.PhysicalResistance + s.MagicalResistance) / 2.0 + s.Constitution / 2.0;

        foreach (var e in battle.Enemies)
        {
            // Lower enemy resistance rating for the beast's attack type: up to +30% of its attack.
            int resist = s.IsPhysical ? e.Ratings.PhysicalResistance : e.Ratings.MagicalResistance;
            int other = s.IsPhysical ? e.Ratings.MagicalResistance : e.Ratings.PhysicalResistance;
            if (resist < other)
            {
                score += attack * 0.3 * (other - resist) / 4.0;
                reasons.Add($"{(s.IsPhysical ? "physical" : "magical")} attacks hit {e.Name}'s weaker side");
            }

            // A status the enemy does not resist.
            ushort open = (ushort)(beast.InflictsMask & ~e.ResistMask);
            if (open != 0)
            {
                score += attack * 0.15;
                reasons.Add($"can inflict a status {e.Name} does not resist");
            }

            // Standing up to the enemy's attack type: physical damage types against physical resistance.
            bool enemyPhysical = e.Element is Element.Blunt or Element.Piercing or Element.Slashing
                || (e.Element == Element.None && e.Ratings.Strength >= e.Ratings.Intelligence);
            int defence = enemyPhysical ? s.PhysicalResistance : s.MagicalResistance;
            int otherDefence = enemyPhysical ? s.MagicalResistance : s.PhysicalResistance;
            if (defence > otherDefence)
            {
                score += (defence - otherDefence) * 0.5;
                reasons.Add($"sturdy against {e.Name}'s {(enemyPhysical ? "physical" : "magical")} attacks");
            }
        }
        return new BeastScore(beast, rank, Math.Round(score, 1), reasons.Distinct().ToList());
    }
}
