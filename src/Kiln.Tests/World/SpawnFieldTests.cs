using Kiln.Core.Foundation;
using Kiln.Core.World;
using Xunit;

namespace Kiln.Tests.World;

/// <summary>
/// The population of one spawn field (WLD-03). Written against the failure modes the naive
/// version has: a camp that stays empty because nobody was watching it, a camp that trickles
/// back one creature at a time, and a camp that flickers when you stand on its edge.
/// </summary>
public class SpawnFieldTests
{
    private static SpawnFieldDef Def(int count = 4, double respawn = 20, double activation = 50) => new(
        Id: "spf_test",
        Entries: [new SpawnEntry("mob_a", 1.0)],
        Count: count,
        RespawnSeconds: respawn,
        ActivationRadius: activation,
        Radius: 8);

    private static SpawnField Field(SpawnFieldDef? def = null) =>
        new(def ?? Def(), new DeterministicRng(1).Fork("spawn"));

    private static SpawnFieldState Near(int alive, int headroom = 99) => new(10, alive, headroom);

    private static SpawnFieldState Far(int alive, int headroom = 99) => new(500, alive, headroom);

    [Fact]
    public void ArrivingFindsTheCampAlreadyStanding()
    {
        var field = Field();

        // One tick at the door, and the whole camp is there. Watching it assemble itself
        // would announce that the world is a machine.
        Assert.Equal(4, field.Tick(0.1, Near(0)).Count);
    }

    [Fact]
    public void ClearedCampComesBackAllAtOnce()
    {
        var field = Field(Def(count: 4, respawn: 20));
        field.Tick(0.1, Near(0));

        // Everything died. The field notices four missing and starts four timers.
        field.Tick(0.1, Near(0));

        for (var t = 0.0; t < 19; t += 0.5) Assert.Empty(field.Tick(0.5, Near(0)));

        // A trickle of single arrivals behind the player is what makes farming feel like
        // housework; the camp returns as a camp.
        Assert.Equal(4, field.Tick(2.0, Near(0)).Count);
    }

    [Fact]
    public void TimersRunWhileThePlayerIsAway()
    {
        var field = Field(Def(count: 3, respawn: 20));
        field.Tick(0.1, Near(0));

        // Walk away with the camp cleared.
        field.Tick(0.1, Far(0));
        Assert.False(field.Active);

        for (var t = 0.0; t < 30; t += 1.0) Assert.Empty(field.Tick(1.0, Far(0)));

        // Coming back five minutes later to a camp still empty is the bug this prevents.
        Assert.Equal(3, field.Tick(0.1, Near(0)).Count);
    }

    [Fact]
    public void AnUntouchedFieldNeverOwesMoreThanItsPopulation()
    {
        var field = Field(Def(count: 3, respawn: 5));
        field.Tick(0.1, Near(0));
        field.Tick(0.1, Far(0));

        for (var t = 0.0; t < 600; t += 5.0) field.Tick(5.0, Far(0));

        // A field left alone overnight must not empty a hundred creatures onto the player
        // the moment they walk back over the ridge.
        Assert.Equal(3, field.Tick(0.1, Near(0)).Count);
    }

    [Fact]
    public void TheEdgeDoesNotFlicker()
    {
        var def = Def(activation: 50);
        var field = Field(def);

        field.Tick(0.1, new SpawnFieldState(49, 0, 99));
        Assert.True(field.Active);

        // Standing just past the line keeps the camp alive; only the hysteresis band ends it.
        field.Tick(0.1, new SpawnFieldState(52, 4, 99));
        Assert.True(field.Active);
        Assert.False(field.ShouldClear);

        field.Tick(0.1, new SpawnFieldState(70, 4, 99));
        Assert.False(field.Active);
        Assert.True(field.ShouldClear);
    }

    [Fact]
    public void GlobalHeadroomCapsWhatArrivesThisTick()
    {
        var field = Field(Def(count: 5));

        Assert.Equal(2, field.Tick(0.1, Near(0, headroom: 2)).Count);

        // The rest are still owed, not forgotten.
        Assert.Equal(3, field.Ready);
        Assert.Equal(3, field.Tick(0.1, Near(2, headroom: 99)).Count);
    }

    [Fact]
    public void AFullCampAsksForNothing()
    {
        var field = Field(Def(count: 4));
        field.Tick(0.1, Near(0));

        for (var t = 0.0; t < 60; t += 1.0) Assert.Empty(field.Tick(1.0, Near(4)));

        Assert.Equal(0, field.Pending);
    }

    [Fact]
    public void WeightedEntriesStayOnTheirOwnStream()
    {
        var def = Def(count: 200) with { Entries = [new SpawnEntry("mob_a", 3.0), new SpawnEntry("mob_b", 1.0)] };
        var first = new SpawnField(def, new DeterministicRng(7).Fork("spawn")).Tick(0.1, Near(0));
        var again = new SpawnField(def, new DeterministicRng(7).Fork("spawn")).Tick(0.1, Near(0));

        Assert.Equal(first, again);
        Assert.Contains("mob_a", first);
        Assert.Contains("mob_b", first);
    }
}
