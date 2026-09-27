using Kiln.Core.World;
using Xunit;

namespace Kiln.Tests.World;

public class BorderTerrainTests
{
    private static readonly BorderShape Hills = new(Low: 22, High: 36, Rise: 11, Terrace: 0, Seed: 3);

    private static BorderTerrain Ring(params (double X, double Z)[] gates) =>
        new(Hills, inset: 84, keepClear: [], passes: gates);

    [Fact]
    public void TheMapItselfStaysFlat()
    {
        var ring = Ring();

        for (var x = -70.0; x <= 70; x += 7)
        {
            for (var z = -70.0; z <= 70; z += 7)
            {
                Assert.Equal(0, ring.Height(x, z), 6);
            }
        }
    }

    [Fact]
    public void MountainsStandAllTheWayRound()
    {
        // Every direction ends in a ridge well above a character's head, so no view from
        // inside finds the edge of the world.
        var ring = Ring();

        for (var i = 0; i < 72; i++)
        {
            var angle = i * Math.Tau / 72;
            var (x, z) = (Math.Cos(angle) * 140, Math.Sin(angle) * 140);

            Assert.True(ring.Height(x, z) > 12, $"only {ring.Height(x, z):0.0} m at {i * 5}°");
        }
    }

    [Fact]
    public void AGateOpensAPass_AndTheRangeClosesItFurtherOn()
    {
        var ring = Ring((74, -74));
        var shut = Ring();

        // Twenty-five metres past the gate, on its line: the valley floor, where there would have
        // been mountain.
        var (px, pz) = (92.0, -92.0);
        Assert.True(ring.Height(px, pz) < 4, $"pass floor at {ring.Height(px, pz):0.0} m");
        Assert.True(shut.Height(px, pz) > 8);

        // Far down the same line the next range stands.
        Assert.True(ring.Height(74 + 120, -74 - 120) > 12);

        // And to either side of the pass the mountains are untouched.
        Assert.Equal(shut.Height(140, 40), ring.Height(140, 40), 6);
    }

    [Fact]
    public void SomethingNearTheEdgeIsNeverBuried()
    {
        var clear = new BorderTerrain(Hills, 84, [(83, 0, 8)], []);

        Assert.Equal(0, clear.Height(86, 0), 6);
        Assert.True(clear.FootAt(0) >= 90.9);

        // Far round the ring the line is where it always was.
        Assert.Equal(Ring().FootAt(Math.PI), clear.FootAt(Math.PI), 6);
    }

    [Fact]
    public void TheFenceClosesTheMap_BehindTheGateAndInsideTheGround()
    {
        var ring = Ring((74, -74));
        var fence = ring.Fence(limit: 89);

        Assert.Equal(360, fence.Count);

        foreach (var (x, z) in fence)
        {
            Assert.True(Math.Max(Math.Abs(x), Math.Abs(z)) <= 89.5, $"fence at ({x:0}, {z:0}) is off the ground");
            Assert.True(Math.Sqrt((x * x) + (z * z)) > 60, $"fence at ({x:0}, {z:0}) cuts into the map");
        }

        // The gate itself is inside: the post nearest its direction stands past it.
        var gate = Math.Sqrt(2 * 74.0 * 74);
        var (fx, fz) = fence[315];
        Assert.True(Math.Sqrt((fx * fx) + (fz * fz)) > gate + 4);
    }

    [Fact]
    public void MesasStepInLedges()
    {
        var mesa = new BorderTerrain(Hills with { Terrace = 6 }, 84, [], []);
        var ledges = 0;

        for (var r = 90.0; r < 160; r += 0.5)
        {
            var h = mesa.Height(r, 10);
            var frac = h / 6 - Math.Floor(h / 6);

            if (frac < 0.02) ledges++;
        }

        // Most of the way up is tread, not slope.
        Assert.True(ledges > 30, $"{ledges} samples on a ledge");
    }

    [Fact]
    public void SameSeed_SameSkyline()
    {
        Assert.Equal(Ring().Height(120, 55), Ring().Height(120, 55), 9);
        Assert.NotEqual(Ring().Height(120, 55), new BorderTerrain(Hills with { Seed = 4 }, 84, [], []).Height(120, 55));
    }
}
