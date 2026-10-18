using CrucibleSux.Core;

namespace CrucibleSux.Tests;

public class ThreatTests
{
    private static ThreatView View(int[] attackers, int level = 10, double hp = 100, double loss = 0, bool mounted = false, bool riding = false, TimeSpan? fleeing = null) =>
        new(mounted, riding, level, attackers, hp, loss, Floor: 35, fleeing);

    [Fact]
    public void An_even_fight_is_fought()
    {
        Assert.Equal(ThreatMove.Fight, ThreatPolicy.Decide(View([10])));
        Assert.Equal(ThreatMove.Fight, ThreatPolicy.Decide(View([12, 9])));
    }

    [Fact]
    public void Too_strong_too_many_or_losing_too_fast_flees()
    {
        Assert.Equal(ThreatMove.Flee, ThreatPolicy.Decide(View([13])));
        Assert.Equal(ThreatMove.Flee, ThreatPolicy.Decide(View([8, 8, 8])));
        // A FATE's group at the synced level is the FATE, not a crowd to run from.
        Assert.Equal(ThreatMove.Fight, ThreatPolicy.Decide(View([10, 10, 10]) with { InFate = true }));
        // 60% now, losing 6% a second: 30% in 5 s, below the 35% floor.
        Assert.Equal(ThreatMove.Flee, ThreatPolicy.Decide(View([10], hp: 60, loss: 6)));
    }

    [Fact]
    public void A_mounted_character_stays_mounted()
    {
        // Riding on leaves the attackers behind, even a strong one; standing on the mount, it rides away.
        Assert.Equal(ThreatMove.Keep, ThreatPolicy.Decide(View([30], mounted: true, riding: true)));
        Assert.Equal(ThreatMove.Flee, ThreatPolicy.Decide(View([10], mounted: true, riding: false)));
    }

    [Fact]
    public void Fleeing_goes_on_until_it_works_or_a_minute_has_passed()
    {
        Assert.Equal(ThreatMove.Flee, ThreatPolicy.Decide(View([10], fleeing: TimeSpan.FromSeconds(20))));
        Assert.Equal(ThreatMove.Fight, ThreatPolicy.Decide(View([30], fleeing: ThreatPolicy.GiveUpAfter)));
    }

    [Fact]
    public void A_spot_is_dangerous_after_two_flees_from_it()
    {
        var map = new DangerMap();
        map.Record(148, 100, 100);
        Assert.False(map.IsDangerous(148, 105, 95));

        map.Record(148, 110, 100);
        Assert.True(map.IsDangerous(148, 105, 95));
        Assert.False(map.IsDangerous(148, 200, 200));
        Assert.False(map.IsDangerous(152, 105, 95));
    }
}
