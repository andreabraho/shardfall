using Kiln.Core.World;
using Xunit;

namespace Kiln.Tests.World;

/// <summary>Shrines, discovery and the fee (WLD-02, WLD-09).</summary>
public class FastTravelTests
{
    private static ZoneGraph Graph() => new(
        [
            new Zone("zone_hub", "$n", ZoneKind.Hub, new LevelBand(1, 3), "", [], ["shr_hub"]),
            new Zone("zone_far", "$n", ZoneKind.Wilds, new LevelBand(15, 19), "", [], ["shr_far"]),
            new Zone("zone_deep", "$n", ZoneKind.Dungeon, new LevelBand(19, 23), "",
                [], ["shr_deep_mouth", "shr_deep_vault"]),
        ],
        [
            new Shrine("shr_hub", "$n", "zone_hub"),
            new Shrine("shr_far", "$n", "zone_far"),
            new Shrine("shr_deep_mouth", "$n", "zone_deep"),
            new Shrine("shr_deep_vault", "$n", "zone_deep", FastTravel: false),
        ]);

    private static FastTravelNetwork Net() => new(Graph());

    [Fact]
    public void DiscoveryIsReportedOnceAndAnchorsFollowEveryVisit()
    {
        var net = Net();

        Assert.True(net.Discover("shr_hub"));
        Assert.False(net.Discover("shr_hub"));
        Assert.Equal("shr_hub", net.Anchor);

        net.Discover("shr_far");
        Assert.Equal("shr_far", net.Anchor);
    }

    [Fact]
    public void YouCannotTravelSomewhereYouHaveNeverStood()
    {
        var net = Net();
        net.Discover("shr_hub");

        Assert.Equal(TravelRefusal.NotDiscovered, net.Quote("shr_far", 100_000, false).Refusal);
    }

    [Fact]
    public void ADungeonCheckpointIsNotADoorIn()
    {
        var net = Net();
        net.Discover("shr_hub");
        net.Discover("shr_deep_vault");

        // It saves and it respawns you; it does not let you skip the dungeon you already walked.
        Assert.Equal(TravelRefusal.NotATravelPoint, net.Quote("shr_deep_vault", 100_000, false).Refusal);
        Assert.DoesNotContain(net.Destinations(), s => s.Id == "shr_deep_vault");
    }

    [Fact]
    public void FastTravelIsNotAnEscapeFromAFight()
    {
        var net = Net();
        net.Discover("shr_far");
        net.Discover("shr_hub");

        Assert.Equal(TravelRefusal.InCombat, net.Quote("shr_far", 100_000, true).Refusal);
    }

    [Fact]
    public void TheFeeRisesWithTheDestinationBandButStaysPocketChange()
    {
        var net = Net();
        net.Discover("shr_far");
        net.Discover("shr_hub");

        var home = net.CostTo("shr_hub");
        var far = net.CostTo("shr_far");

        Assert.True(far > home);

        // The fee exists to make travel a decision, not a toll gate: by the time walking is
        // tedious it has to be affordable, so it stays far under a single band's income.
        Assert.True(far < 1000);
    }

    [Fact]
    public void NotEnoughYangIsRefusedWithThePriceAttached()
    {
        var net = Net();
        net.Discover("shr_far");
        net.Discover("shr_hub");

        var quote = net.Quote("shr_far", 5, false);

        Assert.False(quote.Allowed);
        Assert.Equal(TravelRefusal.NotEnoughYang, quote.Refusal);
        Assert.Equal(net.CostTo("shr_far"), quote.Cost);
    }

    [Fact]
    public void TravellingToWhereYouAreStandingIsRefused()
    {
        var net = Net();
        net.Discover("shr_hub");

        Assert.Equal(TravelRefusal.AlreadyHere, net.Quote("shr_hub", 100_000, false).Refusal);
    }
}
