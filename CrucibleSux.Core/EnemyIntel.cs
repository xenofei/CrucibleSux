namespace CrucibleSux.Core;

/// <summary>XBMActionTarget: whom an enemy ability is aimed at, as the board's enemy intel shows it.</summary>
public enum IntelTarget : byte
{
    None = 0,
    Self = 1,
    Ground = 2,
    HighestEnmity = 3,
    Random = 4,
    Player = 5,

    /// <summary>Another enemy: a buff or heal.</summary>
    Allies = 6,
}

/// <summary>XBMActionEffectType: the shape the enemy intel shows for an ability.</summary>
public enum IntelShape : byte
{
    None = 0,
    SingleTarget = 1,
    Front = 2,
    Rear = 3,
    FrontRear = 4,
    Lateral = 5,
    Circle = 6,
    Ring = 7,
    CircleRing = 8,

    /// <summary>Hits the whole arena.</summary>
    Universal = 9,
    Cross = 10,
}

/// <summary>One of the two abilities the enemy intel lists for an enemy (an XBMBattleDetailAction row).</summary>
public sealed record EnemyAbility(uint ActionId, string Name, IntelTarget Target, IntelShape Shape, uint StatusId);

/// <summary>Which enemy dies first in a board battle, from the game's own enemy intel.</summary>
public static class TargetPriority
{
    /// <summary>
    /// 2 for an enemy that buffs or heals another (the succubus mage's Fanaticism, the younger tablitaur's Rallying Cheer);
    /// 1 for an add with an arena-wide attack (the Ogre's wisps explode with Scorched Earth when they reach you);
    /// 0 otherwise. A battle's main enemy is never raised for its own arena-wide attack: it has to die anyway.
    /// </summary>
    public static int Of(EnemyInfo enemy, bool isMain)
    {
        if (enemy.Abilities.Any(a => a.Target == IntelTarget.Allies))
            return 2;
        if (!isMain && enemy.Abilities.Any(a => a.Shape == IntelShape.Universal))
            return 1;
        return 0;
    }

    /// <summary>Every board enemy's priority by BNpcName row; the highest wins when a name appears twice.</summary>
    public static IReadOnlyDictionary<uint, int> ByName(IEnumerable<BattleInfo> battles)
    {
        var result = new Dictionary<uint, int>();
        foreach (var battle in battles)
        {
            for (int i = 0; i < battle.Enemies.Count; i++)
            {
                var e = battle.Enemies[i];
                result[e.NameId] = Math.Max(result.GetValueOrDefault(e.NameId), Of(e, i == 0));
            }
        }
        return result;
    }
}
