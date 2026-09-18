using Kiln.Core.Combat;
using Xunit;

namespace Kiln.Tests.Combat;

public class PoolTests
{
    [Fact]
    public void Remove_ClampsAtZero_AndReportsActualAmount()
    {
        var pool = new Pool(100);

        Assert.Equal(60, pool.Remove(60));
        Assert.Equal(40, pool.Remove(999));
        Assert.True(pool.IsEmpty);
    }

    [Fact]
    public void Add_ClampsAtMax()
    {
        var pool = new Pool(100, 90);

        Assert.Equal(10, pool.Add(50));
        Assert.True(pool.IsFull);
    }

    [Fact]
    public void TrySpend_IsAllOrNothing()
    {
        var pool = new Pool(100, 30);

        Assert.False(pool.TrySpend(50));
        Assert.Equal(30, pool.Current);

        Assert.True(pool.TrySpend(30));
        Assert.Equal(0, pool.Current);
    }

    [Fact]
    public void RaisingMax_KeepsTheSameFraction()
    {
        var pool = new Pool(100, 50);
        pool.Max = 200;

        Assert.Equal(0.5, pool.Fraction, 6);
    }

    [Fact]
    public void LoweringMax_CannotKill()
    {
        // A +MaxHP buff expiring must never be lethal on its own.
        var pool = new Pool(200, 100);
        pool.Max = 100;

        Assert.False(pool.IsEmpty);
        Assert.Equal(0.5, pool.Fraction, 6);
    }
}

public class StatusEffectTests
{
    [Fact]
    public void Poison_TicksDamageOverTime()
    {
        var set = new StatusEffectSet();
        set.Apply(StatusEffectSet.Poison(damagePerTick: 10, duration: 3));

        double total = 0;
        for (var i = 0; i < 30; i++)
        {
            total += set.Tick(0.1).Damage;
        }

        Assert.Equal(30, total, 3);
    }

    [Fact]
    public void Poison_StacksUpToItsCap()
    {
        var set = new StatusEffectSet();

        for (var i = 0; i < 10; i++)
        {
            set.Apply(StatusEffectSet.Poison(damagePerTick: 5, duration: 10, maxStacks: 3));
        }

        Assert.Equal(3, set.Get(StatusKind.Poison)!.Stacks);
        Assert.Equal(15, set.Get(StatusKind.Poison)!.DamagePerTick, 3);
    }

    [Fact]
    public void Stun_NeverStacks()
    {
        // Chain-stunning a player out of control is unacceptable, so stun only refreshes.
        var set = new StatusEffectSet();

        set.Apply(StatusEffectSet.Stun(1.0));
        set.Apply(StatusEffectSet.Stun(1.0));
        set.Apply(StatusEffectSet.Stun(1.0));

        Assert.Equal(1, set.Get(StatusKind.Stun)!.Stacks);
        Assert.True(set.IsStunned);
    }

    [Fact]
    public void Stun_Expires()
    {
        var set = new StatusEffectSet();
        set.Apply(StatusEffectSet.Stun(1.0));

        set.Tick(1.1);

        Assert.False(set.IsStunned);
        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void Slow_KeepsTheStrongest()
    {
        var set = new StatusEffectSet();

        set.Apply(StatusEffectSet.Slow(0.30));
        set.Apply(StatusEffectSet.Slow(0.10));

        Assert.Equal(0.70, set.MoveSpeedMultiplier, 3);
    }

    [Fact]
    public void Slow_CannotFullyImmobilise()
    {
        var set = new StatusEffectSet();
        set.Apply(StatusEffectSet.Slow(0.99));

        Assert.True(set.MoveSpeedMultiplier >= 0.2);
    }

    [Fact]
    public void Weaken_And_Vulnerability_ScaleDamage()
    {
        var set = new StatusEffectSet();

        set.Apply(StatusEffectSet.Weaken(0.25));
        set.Apply(StatusEffectSet.Vulnerability(0.30));

        Assert.Equal(0.75, set.DamageDealtMultiplier, 3);
        Assert.Equal(1.30, set.DamageTakenMultiplier, 3);
    }

    [Fact]
    public void Tick_ReportsExpiredEffects()
    {
        var set = new StatusEffectSet();
        set.Apply(StatusEffectSet.Slow(0.2, duration: 0.5));

        var result = set.Tick(0.6);

        Assert.Contains(StatusKind.Slow, result.Expired);
        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void CleanseOne_RemovesASingleHarmfulStatus()
    {
        var set = new StatusEffectSet();
        set.Apply(StatusEffectSet.Poison(5));
        set.Apply(StatusEffectSet.Stun());

        Assert.True(set.CleanseOne());
        Assert.Equal(1, set.Count);
        Assert.False(set.IsStunned); // stun is cleansed first, being the most punishing
    }

    [Fact]
    public void EmptySet_IsCheapAndInert()
    {
        var set = new StatusEffectSet();
        var result = set.Tick(1.0);

        Assert.Equal(0, result.Damage);
        Assert.Empty(result.Expired);
        Assert.Equal(1.0, set.MoveSpeedMultiplier, 6);
    }

    [Fact]
    public void LongTick_DoesNotLoseDamage()
    {
        // A frame spike must not silently swallow damage-over-time ticks.
        var set = new StatusEffectSet();
        set.Apply(StatusEffectSet.Poison(damagePerTick: 10, duration: 5));

        var damage = set.Tick(3.0).Damage;

        Assert.Equal(30, damage, 3);
    }
}
