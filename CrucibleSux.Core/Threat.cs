namespace CrucibleSux.Core;

/// <summary>What the journey does when something attacks the character outside the Crucible.</summary>
public enum ThreatMove
{
    /// <summary>Get off the mount if needed, target the attacker and fight it.</summary>
    Fight,

    /// <summary>Sprint (or ride) back the way it came until the enemies give up.</summary>
    Flee,

    /// <summary>Riding on: the trip goes on, and the attackers are left behind.</summary>
    Keep,
}

/// <param name="Riding">The character is mounted and moving.</param>
/// <param name="AttackerLevels">The levels of the enemies attacking the character or its beast.</param>
/// <param name="LossPerSecond">HP% the character is losing per second in this fight.</param>
/// <param name="FleeingFor">How long it has been fleeing, or null when it is not.</param>
/// <param name="InFate">In a FATE, enemies come in groups at the synced level, so only their strength and the HP count.</param>
public sealed record ThreatView(
    bool Mounted,
    bool Riding,
    int Level,
    IReadOnlyList<int> AttackerLevels,
    double HpPct,
    double LossPerSecond,
    double Floor,
    TimeSpan? FleeingFor,
    bool InFate = false);

/// <summary>
/// Fight or flee (docs/research/18-threats.md): flee from an enemy 3 or more levels above the character, from 3 or more
/// enemies at once, or when the HP lost in the next 5 s would take the character below its floor. A mounted character
/// stays mounted: it rides on while moving, and rides away when it is standing. After a minute of fleeing without getting
/// away, it fights.
/// </summary>
public static class ThreatPolicy
{
    public const int TooStrongLevels = 3;
    public const int TooMany = 3;
    public static readonly TimeSpan Horizon = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(1);

    public static ThreatMove Decide(ThreatView v)
    {
        if (v.FleeingFor is { } fleeing)
            return fleeing < GiveUpAfter ? ThreatMove.Flee : ThreatMove.Fight;
        if (v.Mounted)
            return v.Riding ? ThreatMove.Keep : ThreatMove.Flee;
        bool tooStrong = v.AttackerLevels.Any(l => l >= v.Level + TooStrongLevels);
        bool tooMany = !v.InFate && v.AttackerLevels.Count >= TooMany;
        bool losing = v.HpPct - (v.LossPerSecond * Horizon.TotalSeconds) < v.Floor;
        return tooStrong || tooMany || losing ? ThreatMove.Flee : ThreatMove.Fight;
    }
}

/// <summary>
/// Spots where the character had to flee, per zone. A spot that made it flee twice is dangerous, and the journey stops
/// sending the character there: a search point or a FATE within <see cref="Radius"/> of it is skipped.
/// </summary>
public sealed class DangerMap
{
    public const float Radius = 30f;
    public const int DangerousAfter = 2;

    private readonly List<(uint Territory, float X, float Z, int Count)> spots = [];

    public void Record(uint territory, float x, float z)
    {
        int i = this.Find(territory, x, z);
        if (i < 0)
            this.spots.Add((territory, x, z, 1));
        else
            this.spots[i] = this.spots[i] with { Count = this.spots[i].Count + 1 };
    }

    public bool IsDangerous(uint territory, float x, float z) => this.Find(territory, x, z) is int i && i >= 0 && this.spots[i].Count >= DangerousAfter;

    public void Clear() => this.spots.Clear();

    private int Find(uint territory, float x, float z) =>
        this.spots.FindIndex(s => s.Territory == territory && ((s.X - x) * (s.X - x)) + ((s.Z - z) * (s.Z - z)) <= Radius * Radius);
}
