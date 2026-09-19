using Kiln.Core.Foundation;

namespace Kiln.Core.Encounters;

/// <summary>The three fighting phases, plus the states either side of them.</summary>
public enum ShardPhase
{
    /// <summary>Not yet engaged. The corruption zone is visible but nothing is happening.</summary>
    Dormant,

    /// <summary>100–66% shard health. Trash only, single-ring pulse.</summary>
    One,

    /// <summary>66–33%. An elite joins, and the pulse gains a second ring.</summary>
    Two,

    /// <summary>33–0%. The shard tries to reclaim itself; the anchor add must die or it heals.</summary>
    Three,

    Broken,
}

/// <summary>
/// The modifier a node rolls each time it respawns (SHD-08, doc 02 §3).
/// </summary>
/// <remarks>
/// The point is that farming a node is varied rather than identical. Each modifier changes
/// what the fight asks of the player, not how large its numbers are: Warded changes the kill
/// order, Frenzied changes the spacing, Venomous changes whether standing in the pulse is
/// survivable at all, and Twin changes the shape of the arena.
/// </remarks>
public enum ShardModifier
{
    None,
    Frenzied,
    Warded,
    Venomous,
    Twin,
}

/// <summary>What a modifier actually changes.</summary>
public sealed record ShardModifierEffects(
    double AddSpeedMultiplier,
    double AddAttackSpeedMultiplier,

    /// <summary>Damage reduction on the shard while any add is alive. Warded's kill-order pressure.</summary>
    double WardedReduction,

    /// <summary>Whether the radial pulse leaves poison behind.</summary>
    bool PoisonPulse,

    /// <summary>Extra shards that must also be broken.</summary>
    int ExtraShards)
{
    public static ShardModifierEffects For(ShardModifier modifier) => modifier switch
    {
        ShardModifier.Frenzied => new(1.25, 1.35, 0, false, 0),
        ShardModifier.Warded => new(1.0, 1.0, 0.60, false, 0),
        ShardModifier.Venomous => new(1.0, 1.0, 0, true, 0),
        ShardModifier.Twin => new(1.0, 1.0, 0, false, 1),
        _ => new(1.0, 1.0, 0, false, 0),
    };
}

/// <summary>One enemy slot in a wave.</summary>
/// <param name="IsAnchor">
/// The add whose death interrupts the reclamation cast. Exactly one slot in the phase-three
/// wave carries this.
/// </param>
public sealed record WaveSlot(EnemyRole Role, int Count, bool IsAnchor = false);

/// <summary>The enemies a phase brings with it.</summary>
public sealed record ShardWave(ShardPhase Phase, IReadOnlyList<WaveSlot> Slots)
{
    public int TotalCount
    {
        get
        {
            var total = 0;

            foreach (var slot in Slots) total += slot.Count;

            return total;
        }
    }
}

/// <summary>
/// A shard tier: everything about how a fight of this rank behaves.
/// </summary>
/// <remarks>
/// Tier changes wave composition, pulse pattern and loot — not just the size of the numbers
/// (doc 02 §3). A tier-5 shard that is a tier-1 shard with more health teaches the player
/// nothing new and is the reason the original's stones all felt the same.
/// </remarks>
public sealed record ShardTier(
    string Id,
    int Tier,
    int Level,
    double MaxHp,

    /// <summary>Seconds between pulses, measured from the end of the previous one.</summary>
    double PulseInterval,

    /// <summary>Authored at the Disciple baseline, scaled per difficulty like every telegraph.</summary>
    double PulseWindup,

    double PulseRadius,
    double PulseDamageCoef,

    /// <summary>Seconds the reclamation cast takes before it lands.</summary>
    double ReclamationSeconds,

    /// <summary>Health the shard regains if the cast completes.</summary>
    double ReclamationHealFraction,

    IReadOnlyList<ShardWave> Waves,
    IReadOnlyList<ShardModifier> ModifierPool,
    string? DropTable)
{
    /// <summary>Health fraction at which phase two begins.</summary>
    public const double PhaseTwoAt = 0.66;

    /// <summary>Health fraction at which phase three begins.</summary>
    public const double PhaseThreeAt = 0.33;

    public static ShardPhase PhaseFor(double healthFraction) => healthFraction switch
    {
        <= 0 => ShardPhase.Broken,
        <= PhaseThreeAt => ShardPhase.Three,
        <= PhaseTwoAt => ShardPhase.Two,
        _ => ShardPhase.One,
    };

    public ShardWave? WaveFor(ShardPhase phase)
    {
        foreach (var wave in Waves)
        {
            if (wave.Phase == phase) return wave;
        }

        return null;
    }

    /// <summary>
    /// Rolls the modifier for a respawn. A node with an empty pool is always plain, which is
    /// what the earliest tiers use — a first shard fight should teach the base rhythm before
    /// it starts varying.
    /// </summary>
    public ShardModifier RollModifier(DeterministicRng rng) =>
        ModifierPool.Count == 0 ? ShardModifier.None : ModifierPool[rng.NextInt(0, ModifierPool.Count)];
}
