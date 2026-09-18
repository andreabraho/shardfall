namespace Sohan.Core.Foundation;

/// <summary>
/// xoshiro256** seeded through SplitMix64.
/// <para>
/// Required by NFR-R.2: combat maths must be identical given the same seed, otherwise the
/// balance simulator (roadmap PRG-09 / ITM-12) cannot reproduce a result and its output is
/// worthless. Never use <see cref="System.Random"/> for anything that affects simulation.
/// </para>
/// <para>
/// Not thread-safe by design — each system owns a forked stream via <see cref="Fork"/>, so
/// e.g. adding a loot roll can never shift the sequence the damage pipeline sees.
/// </para>
/// </summary>
public sealed class DeterministicRng
{
    private ulong _s0, _s1, _s2, _s3;

    public DeterministicRng(ulong seed)
    {
        Seed = seed;
        var sm = seed;
        _s0 = SplitMix64(ref sm);
        _s1 = SplitMix64(ref sm);
        _s2 = SplitMix64(ref sm);
        _s3 = SplitMix64(ref sm);
    }

    public ulong Seed { get; }

    /// <summary>
    /// A new independent stream derived from this seed and a label. Use one per subsystem
    /// ("loot", "damage", "shard_modifier") so streams stay decoupled.
    /// </summary>
    public DeterministicRng Fork(string label)
    {
        ulong h = 1469598103934665603UL; // FNV-1a offset basis
        foreach (var c in label)
        {
            h ^= c;
            h *= 1099511628211UL;
        }

        return new DeterministicRng(Seed ^ h);
    }

    private static ulong SplitMix64(ref ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        var z = x;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

    public ulong NextUInt64()
    {
        var result = Rotl(_s1 * 5, 7) * 9;
        var t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = Rotl(_s3, 45);

        return result;
    }

    /// <summary>Uniform in [0,1).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / 9007199254740992.0);

    /// <summary>Uniform in [min,max).</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
        {
            return minInclusive;
        }

        var range = (ulong)(maxExclusive - minInclusive);
        return minInclusive + (int)(NextUInt64() % range);
    }

    /// <summary>Uniform in [min,max] inclusive — the form most data ranges use.</summary>
    public int NextIntInclusive(int minInclusive, int maxInclusive) =>
        NextInt(minInclusive, maxInclusive == int.MaxValue ? maxInclusive : maxInclusive + 1);

    /// <summary>Uniform in [min,max).</summary>
    public double NextDouble(double minInclusive, double maxExclusive) =>
        minInclusive + (NextDouble() * (maxExclusive - minInclusive));

    /// <summary>True with probability <paramref name="probability"/>.</summary>
    public bool Chance(double probability) => probability > 0 && NextDouble() < probability;

    /// <summary>
    /// Picks an index from weights proportionally. Returns -1 when every weight is zero,
    /// which callers must handle rather than silently defaulting to entry 0.
    /// </summary>
    public int WeightedIndex(IReadOnlyList<double> weights)
    {
        double total = 0;
        for (var i = 0; i < weights.Count; i++)
        {
            if (weights[i] > 0) total += weights[i];
        }

        if (total <= 0) return -1;

        var roll = NextDouble() * total;
        for (var i = 0; i < weights.Count; i++)
        {
            if (weights[i] <= 0) continue;
            roll -= weights[i];
            if (roll < 0) return i;
        }

        // Floating point drift: fall back to the last positive weight.
        for (var i = weights.Count - 1; i >= 0; i--)
        {
            if (weights[i] > 0) return i;
        }

        return -1;
    }
}
