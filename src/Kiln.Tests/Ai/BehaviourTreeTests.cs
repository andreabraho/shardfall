using Kiln.Core.Ai;
using Xunit;

namespace Kiln.Tests.Ai;

public class BehaviourTreeTests
{
    /// <summary>A mutable context so tests can drive conditions and observe actions.</summary>
    private sealed class Ctx
    {
        public bool Flag { get; set; }
        public List<string> Log { get; } = [];
        public double Elapsed { get; set; }
    }

    private static BtNode<Ctx> Say(string what, BtStatus status = BtStatus.Success) =>
        new BtAction<Ctx>((c, _) =>
        {
            c.Log.Add(what);
            return status;
        });

    // -- Selector -----------------------------------------------------------

    [Fact]
    public void Selector_TakesTheFirstSucceedingChild()
    {
        var ctx = new Ctx();
        var tree = new BtSelector<Ctx>(Say("a", BtStatus.Failure), Say("b"), Say("c"));

        Assert.Equal(BtStatus.Success, tree.Tick(ctx, 0.1));
        Assert.Equal(["a", "b"], ctx.Log);
    }

    [Fact]
    public void Selector_FailsWhenEveryChildFails()
    {
        var ctx = new Ctx();
        var tree = new BtSelector<Ctx>(Say("a", BtStatus.Failure), Say("b", BtStatus.Failure));

        Assert.Equal(BtStatus.Failure, tree.Tick(ctx, 0.1));
    }

    [Fact]
    public void Selector_LetsAHigherPriorityBranchInterruptARunningOne()
    {
        // The reason a tree is used at all: an urgent branch must be able to take over
        // from a lower-priority action that is mid-flight.
        var ctx = new Ctx();
        var resets = 0;

        var urgent = new BtSequence<Ctx>(
            new BtCondition<Ctx>(c => c.Flag),
            Say("urgent"));

        var idle = new BtAction<Ctx>(
            (c, _) => { c.Log.Add("idle"); return BtStatus.Running; },
            _ => resets++);

        var tree = new BtSelector<Ctx>(urgent, idle);

        Assert.Equal(BtStatus.Running, tree.Tick(ctx, 0.1));

        ctx.Flag = true;
        Assert.Equal(BtStatus.Success, tree.Tick(ctx, 0.1));

        Assert.Contains("urgent", ctx.Log);
        Assert.Equal(1, resets);
    }

    // -- Sequence -----------------------------------------------------------

    [Fact]
    public void Sequence_RunsEveryChildInOrder()
    {
        var ctx = new Ctx();
        var tree = new BtSequence<Ctx>(Say("a"), Say("b"), Say("c"));

        Assert.Equal(BtStatus.Success, tree.Tick(ctx, 0.1));
        Assert.Equal(["a", "b", "c"], ctx.Log);
    }

    [Fact]
    public void Sequence_StopsAtTheFirstFailure()
    {
        var ctx = new Ctx();
        var tree = new BtSequence<Ctx>(Say("a"), Say("b", BtStatus.Failure), Say("c"));

        Assert.Equal(BtStatus.Failure, tree.Tick(ctx, 0.1));
        Assert.DoesNotContain("c", ctx.Log);
    }

    [Fact]
    public void Sequence_ResumesFromTheRunningChild()
    {
        var ctx = new Ctx();
        var ticks = 0;

        var tree = new BtSequence<Ctx>(
            Say("first"),
            new BtAction<Ctx>((_, _) => ++ticks < 3 ? BtStatus.Running : BtStatus.Success),
            Say("last"));

        Assert.Equal(BtStatus.Running, tree.Tick(ctx, 0.1));
        Assert.Equal(BtStatus.Running, tree.Tick(ctx, 0.1));
        Assert.Equal(BtStatus.Success, tree.Tick(ctx, 0.1));

        // "first" must run once, not once per tick, or entering a branch would repeat
        // its setup every frame.
        Assert.Single(ctx.Log, l => l == "first");
        Assert.Contains("last", ctx.Log);
    }

    // -- Decorators ---------------------------------------------------------

    [Fact]
    public void Cooldown_BlocksTheChildAfterSuccess()
    {
        var ctx = new Ctx();
        var tree = new BtCooldown<Ctx>(1.0, Say("cast"));

        Assert.Equal(BtStatus.Success, tree.Tick(ctx, 0.1));
        Assert.Equal(BtStatus.Failure, tree.Tick(ctx, 0.1));
        Assert.Single(ctx.Log);

        tree.Tick(ctx, 1.0); // burn the cooldown
        Assert.Equal(BtStatus.Success, tree.Tick(ctx, 0.1));
        Assert.Equal(2, ctx.Log.Count);
    }

    [Fact]
    public void Cooldown_DoesNotStartOnFailure()
    {
        // A branch that could not run must stay available, or one bad frame locks the
        // ability out for its whole cooldown.
        var ctx = new Ctx();
        var tree = new BtCooldown<Ctx>(5.0, Say("x", BtStatus.Failure));

        Assert.Equal(BtStatus.Failure, tree.Tick(ctx, 0.1));
        Assert.Equal(BtStatus.Failure, tree.Tick(ctx, 0.1));
        Assert.Equal(2, ctx.Log.Count);
    }

    [Fact]
    public void Cooldown_DoesNotStartWhileRunning()
    {
        var ctx = new Ctx();
        var tree = new BtCooldown<Ctx>(5.0, Say("x", BtStatus.Running));

        Assert.Equal(BtStatus.Running, tree.Tick(ctx, 0.1));
        Assert.Equal(BtStatus.Running, tree.Tick(ctx, 0.1));
        Assert.Equal(2, ctx.Log.Count);
    }

    [Fact]
    public void Cooldown_CanBeCleared()
    {
        var ctx = new Ctx();
        var tree = new BtCooldown<Ctx>(10.0, Say("x"));

        tree.Tick(ctx, 0.1);
        Assert.Equal(BtStatus.Failure, tree.Tick(ctx, 0.1));

        tree.Clear();
        Assert.Equal(BtStatus.Success, tree.Tick(ctx, 0.1));
    }

    [Fact]
    public void Inverter_SwapsSuccessAndFailure()
    {
        var ctx = new Ctx();

        Assert.Equal(BtStatus.Failure, new BtInverter<Ctx>(Say("a")).Tick(ctx, 0.1));
        Assert.Equal(BtStatus.Success, new BtInverter<Ctx>(Say("b", BtStatus.Failure)).Tick(ctx, 0.1));
        Assert.Equal(BtStatus.Running, new BtInverter<Ctx>(Say("c", BtStatus.Running)).Tick(ctx, 0.1));
    }

    [Fact]
    public void Optional_SwallowsFailure()
    {
        var ctx = new Ctx();
        var tree = new BtSequence<Ctx>(
            new BtOptional<Ctx>(Say("maybe", BtStatus.Failure)),
            Say("after"));

        Assert.Equal(BtStatus.Success, tree.Tick(ctx, 0.1));
        Assert.Contains("after", ctx.Log);
    }

    [Fact]
    public void Condition_GatesABranch()
    {
        var ctx = new Ctx();
        var tree = new BtSequence<Ctx>(new BtCondition<Ctx>(c => c.Flag), Say("gated"));

        Assert.Equal(BtStatus.Failure, tree.Tick(ctx, 0.1));
        Assert.Empty(ctx.Log);

        ctx.Flag = true;
        Assert.Equal(BtStatus.Success, tree.Tick(ctx, 0.1));
        Assert.Contains("gated", ctx.Log);
    }

    [Fact]
    public void Delta_ReachesLeaves()
    {
        var ctx = new Ctx();
        var tree = new BtAction<Ctx>((c, d) => { c.Elapsed += d; return BtStatus.Running; });

        tree.Tick(ctx, 0.25);
        tree.Tick(ctx, 0.25);

        Assert.Equal(0.5, ctx.Elapsed, 6);
    }
}
