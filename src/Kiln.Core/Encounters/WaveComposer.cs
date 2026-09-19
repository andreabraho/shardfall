using Kiln.Core.Foundation;

namespace Kiln.Core.Encounters;

/// <summary>Which enemies exist for a given role and level. Implemented over the content database.</summary>
public interface IEnemyRoster
{
    /// <summary>Enemy ids that fill <paramref name="role"/> and are appropriate at <paramref name="level"/>.</summary>
    IReadOnlyList<string> ByRole(EnemyRole role, int level);
}

/// <summary>One enemy the composer decided to spawn.</summary>
public readonly record struct ComposedEnemy(string EnemyId, EnemyRole Role, bool IsAnchor);

/// <summary>
/// Turns a wave's role slots into concrete enemies (SHD-03).
/// </summary>
/// <remarks>
/// Waves are authored as roles rather than as enemy ids so a tier describes a <em>shape</em> —
/// "two bruisers, an archer and a shielder" — and any zone can dress that shape in its own
/// creatures. Without this, every shard would need its own enemy list and adding a zone would
/// mean re-authoring every tier that appears in it.
/// </remarks>
public static class WaveComposer
{
    public static List<ComposedEnemy> Compose(ShardWave wave, IEnemyRoster roster, int level, DeterministicRng rng)
    {
        var composed = new List<ComposedEnemy>(wave.TotalCount);

        foreach (var slot in wave.Slots)
        {
            var candidates = roster.ByRole(slot.Role, level);

            if (candidates.Count == 0) continue;

            for (var i = 0; i < slot.Count; i++)
            {
                var id = candidates[rng.NextInt(0, candidates.Count)];

                // Only the first enemy of an anchor slot is the anchor. A wave with two
                // anchors would mean killing either one interrupts the cast, which halves the
                // pressure the phase-three beat exists to create.
                composed.Add(new ComposedEnemy(id, slot.Role, slot.IsAnchor && i == 0));
            }
        }

        return composed;
    }

    /// <summary>True when the wave names exactly one anchor, as the phase-three beat requires.</summary>
    public static bool HasSingleAnchor(ShardWave wave)
    {
        var anchors = 0;

        foreach (var slot in wave.Slots)
        {
            if (slot.IsAnchor) anchors += Math.Min(slot.Count, 1);
        }

        return anchors == 1;
    }
}
