using Kiln.Core.Encounters;
using Kiln.Core.Foundation;
using Kiln.Data.Definitions;
using Kiln.Data.Loading;

namespace Kiln.Data.Encounters;

/// <summary>
/// Projects shard content into the engine-free shapes the encounter state machine works with,
/// and answers "which enemies fill this role" for the wave composer.
/// </summary>
public sealed class ShardCatalogue : IEnemyRoster
{
    private readonly Dictionary<string, ShardTier> _tiers = [];
    private readonly ContentDatabase _content;

    public ShardCatalogue(ContentDatabase content)
    {
        _content = content;

        foreach (var (id, def) in content.Shards) _tiers[id] = ToTier(def);
    }

    public IReadOnlyDictionary<string, ShardTier> Tiers => _tiers;

    public ShardTier? Tier(string id) => _tiers.GetValueOrDefault(id);

    /// <summary>
    /// Enemies that fill a role near a level. The band widens if nothing matches, because an
    /// empty slot silently shrinks a wave — a shard fight quietly missing its shielder is
    /// much harder to notice than one that spawns a slightly off-level enemy.
    /// </summary>
    public IReadOnlyList<string> ByRole(EnemyRole role, int level)
    {
        foreach (var band in (int[])[4, 8, int.MaxValue])
        {
            var matches = _content.Enemies.Values
                .Where(e => e.Role == role && Math.Abs(e.Level - level) <= band)
                .Select(e => e.Id)
                .ToList();

            if (matches.Count > 0) return matches;
        }

        return [];
    }

    public static ShardTier ToTier(ShardDef def)
    {
        var waves = new List<ShardWave>();

        foreach (var wave in def.Waves)
        {
            if (!TryParsePhase(wave.Phase, out var phase)) continue;

            var slots = new List<WaveSlot>();

            foreach (var slot in wave.Slots)
            {
                if (!Enum.TryParse<EnemyRole>(slot.Role, ignoreCase: true, out var role)) continue;

                slots.Add(new WaveSlot(role, Math.Max(1, slot.Count), slot.Anchor));
            }

            waves.Add(new ShardWave(phase, slots));
        }

        var modifiers = new List<ShardModifier>();

        foreach (var name in def.Modifiers)
        {
            if (Enum.TryParse<ShardModifier>(name, ignoreCase: true, out var modifier)) modifiers.Add(modifier);
        }

        return new ShardTier(
            def.Id,
            def.Tier,
            def.Level,
            def.Hp,
            def.PulseInterval,
            def.PulseWindup,
            def.PulseRadius,
            def.PulseDamageCoef,
            def.ReclamationSeconds,
            def.ReclamationHeal,
            waves,
            modifiers,
            def.DropTable);
    }

    public static bool TryParsePhase(string text, out ShardPhase phase)
    {
        phase = text.ToLowerInvariant() switch
        {
            "one" or "1" => ShardPhase.One,
            "two" or "2" => ShardPhase.Two,
            "three" or "3" => ShardPhase.Three,
            _ => ShardPhase.Dormant,
        };

        return phase != ShardPhase.Dormant;
    }
}
