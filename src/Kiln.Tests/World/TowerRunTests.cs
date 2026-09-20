using Kiln.Core.World;
using Xunit;

namespace Kiln.Tests.World;

/// <summary>
/// A run through a floor tower (FR-7.11). Written against the two things that are easy to get
/// subtly wrong: which clock starts when, and the difference between a floor that finished and
/// a floor whose clock merely ran out.
/// </summary>
public class TowerRunTests
{
    private static TowerFloor Floor(FloorTask task, int index = 1, int targets = 1, double seconds = 0) =>
        new($"flr_{index:00}", index, $"$floor.{index}", task, Targets: targets, Seconds: seconds);

    private static TowerRun Run(params TowerFloor[] floors) => new(floors);

    [Fact]
    public void AFloorStartsSealedBehindItsTask()
    {
        var run = Run(Floor(FloorTask.Break));

        Assert.Equal(FloorPhase.Running, run.Phase);
        Assert.False(run.Finished);
    }

    [Fact]
    public void BreakingTheOneThingOpensTheWayDown()
    {
        var run = Run(Floor(FloorTask.Break), Floor(FloorTask.Hold, 2, seconds: 30));

        run.ScoreTarget();

        Assert.Equal(FloorPhase.Open, run.Phase);
    }

    [Fact]
    public void PartialProgressDoesNotOpenTheFloor()
    {
        var run = Run(Floor(FloorTask.Carry, targets: 3));

        run.ScoreTarget();
        run.ScoreTarget();

        Assert.Equal(FloorPhase.Running, run.Phase);
        Assert.Equal(2, run.Scored);
    }

    [Fact]
    public void AHoldClockRunsFromArrival()
    {
        var run = Run(Floor(FloorTask.Hold, seconds: 10));

        Assert.True(run.ClockRunning);

        run.Tick(9.0);
        Assert.Equal(FloorPhase.Running, run.Phase);

        run.Tick(1.5);
        Assert.Equal(FloorPhase.Open, run.Phase);
    }

    /// <summary>
    /// The one that makes the verb work. A race whose clock starts when the player walks in is
    /// a test of how fast they read an unfamiliar room, which is not the skill being asked for.
    /// </summary>
    [Fact]
    public void ARaceClockWaitsForTheFirstTarget()
    {
        var run = Run(Floor(FloorTask.Race, targets: 3, seconds: 20));

        Assert.False(run.ClockRunning);

        run.Tick(60.0);

        Assert.Equal(FloorPhase.Running, run.Phase);
        Assert.False(run.JustFailed);

        run.ScoreTarget();

        Assert.True(run.ClockRunning);
    }

    [Fact]
    public void ALapsedRaceResetsTheFloorRatherThanEndingTheRun()
    {
        var run = Run(Floor(FloorTask.Race, targets: 3, seconds: 20));

        run.ScoreTarget();
        run.Tick(25.0);

        Assert.True(run.JustFailed);
        Assert.Equal(FloorPhase.Running, run.Phase);
        Assert.Equal(0, run.Scored);
        Assert.False(run.ClockRunning);
    }

    [Fact]
    public void FailureIsReportedOnceAndNotAgainOnTheNextTick()
    {
        var run = Run(Floor(FloorTask.Race, targets: 2, seconds: 5));

        run.ScoreTarget();
        run.Tick(6.0);

        Assert.True(run.JustFailed);

        run.Tick(0.1);

        Assert.False(run.JustFailed);
    }

    [Fact]
    public void ARaceBeatenOnTimeOpensTheFloor()
    {
        var run = Run(Floor(FloorTask.Race, targets: 2, seconds: 20));

        run.ScoreTarget();
        run.Tick(5.0);
        run.ScoreTarget();

        Assert.Equal(FloorPhase.Open, run.Phase);

        // And the clock stops, so a slow walk to the stairs cannot fail a floor already won.
        run.Tick(60.0);

        Assert.Equal(FloorPhase.Open, run.Phase);
        Assert.False(run.JustFailed);
    }

    [Fact]
    public void DescendingIsRefusedWhileTheFloorIsStillRunning()
    {
        var run = Run(Floor(FloorTask.Break), Floor(FloorTask.Fight, 2));

        Assert.False(run.Descend());
        Assert.Equal(1, run.Depth);

        run.ScoreTarget();

        Assert.True(run.Descend());
        Assert.Equal(2, run.Depth);
        Assert.Equal(FloorPhase.Running, run.Phase);
    }

    [Fact]
    public void TheLastFloorIsTheEndOfTheTower()
    {
        var run = Run(Floor(FloorTask.Break), Floor(FloorTask.Fight, 2));

        run.ScoreTarget();
        run.Descend();
        run.ScoreTarget();

        Assert.True(run.Finished);
        Assert.False(run.Descend());
    }

    /// <summary>
    /// Re-entering the dungeon puts the player back where they left off (FR-7.20), which means
    /// a run can legitimately start somewhere other than floor one.
    /// </summary>
    [Fact]
    public void ARunCanResumePartWayDown()
    {
        var run = new TowerRun([Floor(FloorTask.Break), Floor(FloorTask.Hold, 2, seconds: 30), Floor(FloorTask.Fight, 3)], startAt: 3);

        Assert.Equal(3, run.Depth);
        Assert.Equal(FloorTask.Fight, run.Floor.Task);
    }

    [Fact]
    public void ResumingBeyondTheTowerLandsOnItsLastFloor()
    {
        var run = new TowerRun([Floor(FloorTask.Break), Floor(FloorTask.Fight, 2)], startAt: 99);

        Assert.Equal(2, run.Depth);
    }

    [Fact]
    public void ScoringAfterTheFloorIsOpenChangesNothing()
    {
        var run = Run(Floor(FloorTask.Break), Floor(FloorTask.Fight, 2));

        run.ScoreTarget();
        run.ScoreTarget();

        Assert.Equal(1, run.Scored);
    }

    /// <summary>The clock the HUD shows counts down, for both verbs, because that is the question.</summary>
    [Fact]
    public void RemainingCountsDown()
    {
        var run = Run(Floor(FloorTask.Hold, seconds: 30));

        Assert.Equal(30, run.Remaining, 3);

        run.Tick(12.0);

        Assert.Equal(18, run.Remaining, 3);
    }
}
