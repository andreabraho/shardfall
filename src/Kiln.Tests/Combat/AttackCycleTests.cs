using Kiln.Core.Combat;
using Xunit;

namespace Kiln.Tests.Combat;

public class AttackCycleTests
{
    private static void Run(AttackCycle cycle, double seconds, ref int hits, double step = 0.01)
    {
        for (var t = 0.0; t < seconds - 1e-9; t += step)
        {
            if (cycle.Tick(step)) hits++;
        }
    }

    [Fact]
    public void TheHitLandsAtTheEndOfTheWindup()
    {
        var cycle = new AttackCycle();
        var windup = cycle.TryStart(attacksPerSecond: 1.0);
        var hits = 0;

        Assert.Equal(0.35, windup, 3);
        Run(cycle, 0.30, ref hits);
        Assert.Equal(0, hits);
        Run(cycle, 0.10, ref hits);
        Assert.Equal(1, hits);
        Assert.Equal(AttackPhase.Recovery, cycle.Phase);
    }

    [Fact]
    public void TheTimerGatesTheNextAttack()
    {
        var cycle = new AttackCycle();

        cycle.TryStart(1.0);
        var hits = 0;
        Run(cycle, 0.5, ref hits);

        Assert.False(cycle.CanStart);
        Assert.Equal(0, cycle.TryStart(1.0));

        Run(cycle, 0.51, ref hits);
        Assert.True(cycle.CanStart);
    }

    [Fact]
    public void CancellingTheFollowThroughDoesNotAttackSooner()
    {
        var cycle = new AttackCycle();

        cycle.TryStart(1.0);
        var hits = 0;
        Run(cycle, 0.4, ref hits);

        cycle.CancelRecovery();

        Assert.Equal(AttackPhase.Ready, cycle.Phase);
        Assert.False(cycle.CanStart);
        Assert.True(cycle.Cooldown > 0.5);
    }

    [Fact]
    public void AnInterruptedWindupLosesTheBlowButKeepsTheTimer()
    {
        var cycle = new AttackCycle();

        cycle.TryStart(1.0);
        cycle.Tick(0.1);
        cycle.Interrupt();

        var hits = 0;
        Run(cycle, 0.5, ref hits);

        Assert.Equal(0, hits);
        Assert.False(cycle.CanStart);
    }

    [Fact]
    public void FasterAttacksHitSoonerAndMoreOften()
    {
        Assert.True(AttackCycle.IntervalFor(1.5) < AttackCycle.IntervalFor(1.0));
        Assert.True(AttackCycle.WindupFor(AttackCycle.IntervalFor(1.5)) < AttackCycle.WindupFor(AttackCycle.IntervalFor(1.0)));

        var cycle = new AttackCycle();
        var hits = 0;

        for (var i = 0; i < 1000; i++)
        {
            cycle.TryStart(2.0);
            if (cycle.Tick(0.01)) hits++;
        }

        Assert.InRange(hits, 19, 21);
    }
}
