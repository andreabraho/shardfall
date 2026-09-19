using Kiln.Core.World;
using Xunit;

namespace Kiln.Tests.World;

/// <summary>
/// The shape of the world (WLD-01). These are the checks the validator runs on real content,
/// proved here against deliberately broken worlds.
/// </summary>
public class ZoneGraphTests
{
    private static Zone Z(string id, ZoneKind kind, int min, int max, params string[] exits) =>
        new(id, $"$zone.{id}.name", kind, new LevelBand(min, max), "",
            exits.Select(e => new ZoneExit(e)).ToList(), [$"shr_{id}"]);

    private static ZoneGraph Graph(params Zone[] zones) =>
        new(zones, zones.SelectMany(z => z.Shrines).Select(s => new Shrine(s, $"$shrine.{s}.name", "")));

    [Fact]
    public void AZoneNothingLinksToIsFound()
    {
        var graph = Graph(
            Z("zone_hub", ZoneKind.Hub, 1, 3, "zone_a"),
            Z("zone_a", ZoneKind.Wilds, 3, 7),
            Z("zone_lost", ZoneKind.Wilds, 7, 11));

        Assert.Equal(["zone_lost"], graph.Orphans());
    }

    [Fact]
    public void AGatedZoneIsStillReachable()
    {
        var graph = new ZoneGraph(
            [
                new Zone("zone_hub", "$n", ZoneKind.Hub, new LevelBand(1, 3), "",
                    [new ZoneExit("zone_a", RequiredLevel: 30)], ["shr_hub"]),
                Z("zone_a", ZoneKind.Wilds, 3, 7),
            ],
            [new Shrine("shr_hub", "$n", "zone_hub")]);

        // Behind a level gate is "not yet", not "unreachable". Only a zone nothing leads to
        // at all is a bug.
        Assert.Empty(graph.Orphans());
    }

    [Fact]
    public void ALevelNoZoneCoversIsFound()
    {
        var graph = Graph(
            Z("zone_hub", ZoneKind.Hub, 1, 3, "zone_a"),
            Z("zone_a", ZoneKind.Wilds, 3, 7, "zone_b"),
            Z("zone_b", ZoneKind.Wilds, 10, 14));

        // 8 and 9 leave the player with nothing to do but grind a zone that has stopped paying.
        Assert.Equal([8, 9], graph.BandGaps());
    }

    [Fact]
    public void ContiguousBandsLeaveNoGap()
    {
        var graph = Graph(
            Z("zone_hub", ZoneKind.Hub, 1, 3, "zone_a"),
            Z("zone_a", ZoneKind.Wilds, 3, 7, "zone_b"),
            Z("zone_b", ZoneKind.Wilds, 7, 11));

        Assert.Empty(graph.BandGaps());
    }

    [Fact]
    public void ExitsAreHiddenUntilTheirGateOpens()
    {
        var graph = new ZoneGraph(
            [
                new Zone("zone_a", "$n", ZoneKind.Wilds, new LevelBand(3, 7), "",
                    [
                        new ZoneExit("zone_open"),
                        new ZoneExit("zone_levelled", RequiredLevel: 10),
                        new ZoneExit("zone_story", RequiredQuest: "qst_x"),
                    ],
                    []),
            ],
            []);

        var early = graph.OpenExits("zone_a", 5, new HashSet<string>()).Select(e => e.To).ToList();
        Assert.Equal(["zone_open"], early);

        var later = graph.OpenExits("zone_a", 12, new HashSet<string> { "qst_x" }).Select(e => e.To).ToList();
        Assert.Equal(["zone_open", "zone_levelled", "zone_story"], later);
    }

    [Theory]
    [InlineData(20, ZoneDanger.Trivial)]
    [InlineData(12, ZoneDanger.Fair)]
    [InlineData(8, ZoneDanger.Fair)]
    [InlineData(6, ZoneDanger.Dangerous)]
    [InlineData(2, ZoneDanger.Lethal)]
    public void TheBandIsTheOnlyDifficultySignalTheWorldGives(int level, ZoneDanger expected)
    {
        var zone = Z("zone_a", ZoneKind.Wilds, 8, 12);

        Assert.Equal(expected, zone.DangerFor(level));
    }
}
