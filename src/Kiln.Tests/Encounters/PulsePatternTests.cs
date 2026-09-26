using Kiln.Core.Encounters;
using Xunit;

namespace Kiln.Tests.Encounters;

public class PulsePatternTests
{
    [Fact]
    public void TheGapsAreSafe_AndBetweenThemIsStruck()
    {
        // Four gaps of 35°, the first centred on 0.
        var pattern = PulsePattern.For(ShardPhase.One, 0);

        foreach (var gap in (double[])[0, 90, 180, 270, 15, -15, 105])
        {
            Assert.False(pattern.Hits(gap), $"{gap}° is in a gap");
        }

        foreach (var struck in (double[])[45, 30, 135, 225, 315, 60])
        {
            Assert.True(pattern.Hits(struck), $"{struck}° is struck");
        }
    }

    [Fact]
    public void ItTurnsWithItsRotation()
    {
        var pattern = PulsePattern.For(ShardPhase.One, 45);

        Assert.False(pattern.Hits(45));
        Assert.True(pattern.Hits(0));
    }

    [Fact]
    public void LaterPhases_HaveFewerNarrowerGaps()
    {
        var early = PulsePattern.For(ShardPhase.One, 0);
        var late = PulsePattern.For(ShardPhase.Three, 0);

        Assert.True(late.Gaps < early.Gaps);
        Assert.True(late.GapDegrees < early.GapDegrees);
    }

    [Fact]
    public void TheStruckSectors_AreCentredBetweenTheGaps()
    {
        var pattern = PulsePattern.For(ShardPhase.Two, 10);

        foreach (var centre in pattern.DangerCentres()) Assert.True(pattern.Hits(centre));

        Assert.Equal(3, pattern.DangerCentres().Count());
        Assert.Equal(90, pattern.DangerDegrees, 6);
    }

    [Fact]
    public void TwoFifthsOfTheRingAreSafeAtFirst_AQuarterLater()
    {
        var pattern = PulsePattern.For(ShardPhase.One, 0);
        var safe = Enumerable.Range(0, 360).Count(a => !pattern.Hits(a));

        Assert.InRange(safe, 136, 144);

        var late = PulsePattern.For(ShardPhase.Three, 0);
        Assert.InRange(Enumerable.Range(0, 360).Count(a => !late.Hits(a)), 86, 94);
    }
}
