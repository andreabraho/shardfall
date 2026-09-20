using Kiln.Core.Combat;
using Xunit;

namespace Kiln.Tests.Combat;

public class AttackChainTests
{
    /// <summary>One attack per second, so a swing's cadence reads directly as seconds.</summary>
    private const double Rate = 1.0;

    private static double LapseOf(int step) => AttackChain.At(step).Cadence + AttackChain.Grace;

    [Fact]
    public void Swings_cycle_through_all_four_and_start_over()
    {
        var chain = new AttackChain();

        Assert.Equal(1, chain.Swing(Rate).Step);
        Assert.Equal(2, chain.Swing(Rate).Step);
        Assert.Equal(3, chain.Swing(Rate).Step);
        Assert.Equal(4, chain.Swing(Rate).Step);
        Assert.Equal(1, chain.Swing(Rate).Step);
    }

    [Fact]
    public void Only_the_fourth_swing_sweeps()
    {
        var chain = new AttackChain();

        Assert.False(chain.Swing(Rate).Sweeps);
        Assert.False(chain.Swing(Rate).Sweeps);
        Assert.False(chain.Swing(Rate).Sweeps);
        Assert.True(chain.Swing(Rate).Sweeps);
    }

    [Fact]
    public void Pausing_past_the_grace_starts_the_chain_over()
    {
        var chain = new AttackChain();

        chain.Swing(Rate);
        chain.Swing(Rate);
        Assert.Equal(3, chain.Step);

        chain.Tick(LapseOf(2) + 0.01);

        Assert.Equal(1, chain.Step);
        Assert.False(chain.Open);
    }

    [Fact]
    public void Pausing_within_the_grace_keeps_the_chain()
    {
        var chain = new AttackChain();

        chain.Swing(Rate);
        chain.Tick(LapseOf(1) - 0.01);

        Assert.Equal(2, chain.Step);
        Assert.True(chain.Open);
    }

    /// <summary>
    /// The fragility this replaced: with a fixed window, a slow attack rate meant the
    /// cooldown outlasted the window, the chain lapsed before the player was allowed to
    /// swing again, and the fourth swing could never be reached at all.
    /// </summary>
    [Fact]
    public void A_slow_attacker_can_still_finish_the_chain()
    {
        var chain = new AttackChain();
        const double slow = 0.4;

        for (var expected = 1; expected <= AttackChain.Length; expected++)
        {
            var swing = chain.Swing(slow);

            Assert.Equal(expected, swing.Step);

            // Wait out exactly the cooldown, as a player attacking as fast as allowed does.
            var cooldown = swing.Cadence / slow;

            for (var i = 0; i < 100; i++) chain.Tick(cooldown / 100.0);

            // The sweep ends the chain rather than leaving it open, so it is the three
            // before it that have to survive their own cooldown.
            if (expected == AttackChain.Length) break;

            Assert.True(chain.Open, $"the chain lapsed while waiting out swing {expected}");
            Assert.Equal(expected + 1, chain.Step);
        }
    }

    /// <summary>
    /// Ticking the window in pieces has to add up the same way, or the chain would survive
    /// indefinitely at a high frame rate and lapse early at a low one.
    /// </summary>
    [Fact]
    public void The_window_accumulates_across_ticks()
    {
        var chain = new AttackChain();

        chain.Swing(Rate);

        for (var i = 0; i < 100; i++) chain.Tick(LapseOf(1) / 50.0);

        Assert.Equal(1, chain.Step);
    }

    [Fact]
    public void A_fresh_chain_does_not_lapse_while_idle()
    {
        var chain = new AttackChain();

        chain.Tick(60.0);

        Assert.Equal(1, chain.Step);
        Assert.False(chain.Open);
    }

    /// <summary>
    /// Finishing has to be worth something and cost something. If the chain were both
    /// stronger and no slower, breaking off would simply be a mistake, and a choice that is
    /// always wrong is not a choice.
    /// </summary>
    [Fact]
    public void Finishing_the_chain_trades_speed_for_damage()
    {
        var damage = 0.0;
        var time = 0.0;

        for (var step = 1; step <= AttackChain.Length; step++)
        {
            damage += AttackChain.At(step).DamageCoef;
            time += AttackChain.At(step).Cadence;
        }

        Assert.True(damage > AttackChain.Length, "a full chain should out-damage four plain swings");
        Assert.True(time > AttackChain.Length, "a full chain should take longer than four plain swings");
    }

    [Fact]
    public void The_sweep_reaches_furthest_and_hits_hardest()
    {
        var sweep = AttackChain.At(AttackChain.Length);

        for (var step = 1; step < AttackChain.Length; step++)
        {
            Assert.True(sweep.Range > AttackChain.At(step).Range);
            Assert.True(sweep.DamageCoef > AttackChain.At(step).DamageCoef);
        }

        Assert.Equal(360, sweep.ArcDegrees);
    }

    [Fact]
    public void Reset_returns_to_the_opening_swing()
    {
        var chain = new AttackChain();

        chain.Swing(Rate);
        chain.Swing(Rate);
        chain.Reset();

        Assert.Equal(1, chain.Step);
        Assert.Equal(1, chain.Swing(Rate).Step);
    }
}
