using Shardfall.Core.Foundation;
using Xunit;

namespace Shardfall.Tests.Foundation;

/// <summary>
/// NFR-R.2. If these fail, the balance simulator's output is meaningless and every
/// tuning decision made from it is guesswork.
/// </summary>
public class DeterministicRngTests
{
    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var a = new DeterministicRng(12345);
        var b = new DeterministicRng(12345);

        for (var i = 0; i < 1000; i++)
        {
            Assert.Equal(a.NextUInt64(), b.NextUInt64());
        }
    }

    [Fact]
    public void DifferentSeeds_Diverge()
    {
        var a = new DeterministicRng(1);
        var b = new DeterministicRng(2);

        var same = 0;
        for (var i = 0; i < 100; i++)
        {
            if (a.NextUInt64() == b.NextUInt64()) same++;
        }

        Assert.True(same < 3, $"streams should diverge, but {same}/100 values matched");
    }

    [Fact]
    public void Fork_IsDeterministicButIndependent()
    {
        var loot1 = new DeterministicRng(777).Fork("loot");
        var loot2 = new DeterministicRng(777).Fork("loot");
        var damage = new DeterministicRng(777).Fork("damage");

        Assert.Equal(loot1.NextUInt64(), loot2.NextUInt64());
        Assert.NotEqual(loot1.NextUInt64(), damage.NextUInt64());
    }

    [Fact]
    public void NextDouble_StaysInUnitInterval()
    {
        var rng = new DeterministicRng(42);

        for (var i = 0; i < 10_000; i++)
        {
            var v = rng.NextDouble();
            Assert.InRange(v, 0.0, 0.9999999999);
        }
    }

    [Fact]
    public void NextInt_RespectsBounds()
    {
        var rng = new DeterministicRng(99);

        for (var i = 0; i < 10_000; i++)
        {
            Assert.InRange(rng.NextInt(5, 10), 5, 9);
        }
    }

    [Fact]
    public void NextInt_WithEmptyRange_ReturnsMin()
    {
        var rng = new DeterministicRng(1);
        Assert.Equal(7, rng.NextInt(7, 7));
        Assert.Equal(7, rng.NextInt(7, 3));
    }

    [Fact]
    public void Chance_ApproximatesProbability()
    {
        var rng = new DeterministicRng(2024);
        var hits = 0;
        const int trials = 100_000;

        for (var i = 0; i < trials; i++)
        {
            if (rng.Chance(0.25)) hits++;
        }

        Assert.InRange(hits / (double)trials, 0.24, 0.26);
    }

    [Fact]
    public void Chance_ZeroAndOne_AreAbsolute()
    {
        var rng = new DeterministicRng(5);

        for (var i = 0; i < 1000; i++)
        {
            Assert.False(rng.Chance(0.0));
            Assert.True(rng.Chance(1.0));
        }
    }

    [Fact]
    public void WeightedIndex_RespectsProportions()
    {
        var rng = new DeterministicRng(31337);
        double[] weights = [10, 30, 60];
        var counts = new int[3];
        const int trials = 100_000;

        for (var i = 0; i < trials; i++)
        {
            counts[rng.WeightedIndex(weights)]++;
        }

        Assert.InRange(counts[0] / (double)trials, 0.09, 0.11);
        Assert.InRange(counts[1] / (double)trials, 0.29, 0.31);
        Assert.InRange(counts[2] / (double)trials, 0.59, 0.61);
    }

    [Fact]
    public void WeightedIndex_SkipsZeroWeights()
    {
        var rng = new DeterministicRng(8);
        double[] weights = [0, 0, 1];

        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(2, rng.WeightedIndex(weights));
        }
    }

    [Fact]
    public void WeightedIndex_AllZero_ReturnsMinusOne()
    {
        var rng = new DeterministicRng(8);
        Assert.Equal(-1, rng.WeightedIndex([0, 0, 0]));
    }
}
