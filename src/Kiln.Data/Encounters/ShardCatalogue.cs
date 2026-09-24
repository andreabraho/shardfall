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
    private readonly IReadOnlySet<string>? _pool;

    /// <param name="pool">
    /// The creatures a wave may draw on — a map's own (see <see cref="ZonePool"/>). A role the
    /// pool has nobody for falls back to the whole game at the shard's level.
    /// </param>
    public ShardCatalogue(ContentDatabase content, IReadOnlySet<string>? pool = null)
    {
        _content = content;
        _pool = pool is { Count: > 0 } ? pool : null;

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
        // Never a boss: a wave of bruisers used to be able to draw Greymane or Gorthak, who
        // are bruisers too (REF-06).
        var anyone = _content.Enemies.Values.Where(e => !e.Boss);
        var sources = _pool is null ? [anyone] : new[] { anyone.Where(e => _pool.Contains(e.Id)), anyone };

        foreach (var source in sources)
        {
            foreach (var band in (int[])[4, 8, int.MaxValue])
            {
                var matches = source
                    .Where(e => e.Role == role && Math.Abs(e.Level - level) <= band)
                    .Select(e => e.Id)
                    .ToList();

                if (matches.Count > 0) return matches;
            }
        }

        return [];
    }

    /// <summary>
    /// The creatures that live on a map: every one its camps spawn and its tower floors send.
    /// A shard's waves come from here, so a stone in the orc valley raises orcs, not rats.
    /// </summary>
    public static IReadOnlySet<string> ZonePool(ContentDatabase content, string zoneId)
    {
        var pool = new HashSet<string>(StringComparer.Ordinal);

        if (!content.Zones.TryGetValue(zoneId, out var zone)) return pool;

        foreach (var field in zone.SpawnFields)
        {
            foreach (var entry in field.Entries) pool.Add(entry.Enemy);
        }

        foreach (var floor in zone.Floors)
        {
            foreach (var wave in floor.Waves) pool.Add(wave);
        }

        return pool;
    }

    /// <summary>The map's boss — the one its camps hold — or null when it has none.</summary>
    public static string? ZoneBoss(ContentDatabase content, string zoneId) =>
        content.Zones.TryGetValue(zoneId, out var zone)
            ? zone.SpawnFields
                .SelectMany(f => f.Entries)
                .Select(e => e.Enemy)
                .FirstOrDefault(id => content.Enemies.TryGetValue(id, out var def) && def.Boss)
            : null;

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
