using Kiln.Core.World;
using Xunit;

namespace Kiln.Tests.World;

/// <summary>
/// Safe ground (WLD-12). Every one of these is a way a village stops being safe without
/// anything looking broken.
/// </summary>
public class SafetyFieldTests
{
    private static SafetyField Village(double radius = 20)
    {
        var field = new SafetyField();
        field.Register("saf_village", 0, 0, radius);

        return field;
    }

    [Fact]
    public void InsideIsSafeAndOutsideIsNot()
    {
        var field = Village();

        Assert.True(field.IsSafe(0, 0));
        Assert.True(field.IsSafe(19.9, 0));
        Assert.False(field.IsSafe(20.1, 0));
        Assert.Equal("saf_village", field.RegionAt(10, 10));
    }

    [Fact]
    public void ACampThatMerelyDoesNotOverlapIsStillTooClose()
    {
        // The failure this exists to catch: a camp whose edge stops a metre short of the
        // boundary. Nothing overlaps, and creatures still stand within aggro range of someone
        // standing safely inside — a village that visibly does not quite work.
        var field = Village();

        Assert.Equal("saf_village", field.Encroaches(29, 0, radius: 8));
        Assert.Null(field.Encroaches(35, 0, radius: 8));
    }

    [Fact]
    public void ClearanceIsCountedFromTheCampEdgeNotItsCentre()
    {
        var field = Village();
        var justClear = 20 + 8 + SafetyField.ClearanceMargin + 0.1;

        Assert.Null(field.Encroaches(justClear, 0, radius: 8));
        Assert.NotNull(field.Encroaches(justClear - 0.3, 0, radius: 8));
    }

    [Fact]
    public void RegisteringTheSameRegionTwiceMovesItRatherThanDuplicatingIt()
    {
        // A scene reload that stacked two copies of the village would leave the old one
        // behind as invisible safe ground out in the field.
        var field = Village();
        field.Register("saf_village", 100, 0, 20);

        Assert.Equal(1, field.Count);
        Assert.False(field.IsSafe(0, 0));
        Assert.True(field.IsSafe(100, 0));
    }

    [Fact]
    public void AFieldWithNoRegionsIsSafeNowhere()
    {
        var field = new SafetyField();

        Assert.False(field.IsSafe(0, 0));
        Assert.Null(field.Encroaches(0, 0, radius: 50));
        Assert.Equal(double.MaxValue, field.DistanceToSafety(0, 0));
    }

    [Fact]
    public void DistanceIsNegativeInsideSoTheHudCanShowHowFarTheGateIs()
    {
        var field = Village();

        Assert.Equal(-20, field.DistanceToSafety(0, 0), 3);
        Assert.Equal(10, field.DistanceToSafety(30, 0), 3);
    }

    [Fact]
    public void TwoVillagesBothAnswerForThemselves()
    {
        var field = Village();
        field.Register("saf_second", 200, 0, 30);

        Assert.Equal("saf_village", field.RegionAt(0, 0));
        Assert.Equal("saf_second", field.RegionAt(200, 0));
        Assert.False(field.IsSafe(100, 0));
    }

    [Fact]
    public void ClearanceIsMeasuredAgainstHowFarCreaturesRoamNotTheCampSize()
    {
        // The bug this exists for, reported from play: reed hoppers standing in the village.
        // Nothing chased them there. Their camp sat 41 m out with a radius of 8 and a leash of
        // 14, so one spawned on the near edge could stand 19 m from the village centre — inside
        // safe ground of radius 22 — while never leaving its tether. Measured against the camp's
        // own footprint the placement looked fine, which is exactly why it shipped.
        var field = Village(22);
        const double campRadius = 8;
        const double leash = 14;

        Assert.Null(field.Encroaches(41, 0, campRadius));

        var reach = campRadius + leash + SafetyField.ClearanceMargin;
        Assert.Equal("saf_village", field.Encroaches(41, 0, campRadius, reach - campRadius));
        Assert.Null(field.Encroaches(49, 0, campRadius, reach - campRadius));
    }
}
