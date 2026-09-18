using Kiln.Core.Foundation;
using Kiln.Core.Items;
using Kiln.Data.Definitions;
using Kiln.Data.Loading;

namespace Kiln.Data.Items;

/// <summary>
/// Projects loaded content into the engine-free shapes the item simulation works with.
/// <para>
/// This is the only place that knows both vocabularies. Core never learns what JSON is, and
/// the JSON never learns what a <see cref="StatModifiers"/> field is called — which is what
/// lets the whole item system be tested without a content tree, and lets content be reshaped
/// without touching simulation code.
/// </para>
/// </summary>
public sealed class ItemCatalogue : IItemSpecs, IBonusPools
{
    /// <summary>Keys in <c>base_stats</c> that describe the item frame rather than a modifier.</summary>
    private const string WeaponDamageMin = "weapon_damage_min";
    private const string WeaponDamageMax = "weapon_damage_max";
    private const string ArmorValueKey = "armor_value";

    private readonly Dictionary<string, ItemSpec> _specs = [];
    private readonly Dictionary<string, BonusPool> _pools = [];
    private readonly Dictionary<string, UpgradeLadder> _ladders = [];

    public ItemCatalogue(ContentDatabase content)
    {
        foreach (var (id, def) in content.Items) _specs[id] = ToSpec(def);
        foreach (var (id, def) in content.BonusPools) _pools[id] = ToPool(def);
        foreach (var (id, def) in content.UpgradePaths) _ladders[id] = ToLadder(def);
    }

    public bool TryGet(string id, out ItemSpec spec) => _specs.TryGetValue(id, out spec!);

    // Explicit: an overload differing only by the type of an out parameter is legal but
    // ambiguous at every call site that uses var, which is every call site.
    bool IBonusPools.TryGet(string id, out BonusPool pool) => _pools.TryGetValue(id, out pool!);

    public BonusPool? Pool(string? id) => id is not null && _pools.TryGetValue(id, out var pool) ? pool : null;

    /// <summary>The ladder an item upgrades along, or null when it does not upgrade.</summary>
    public UpgradeLadder? LadderFor(ItemInstance item) =>
        TryGet(item.DefId, out var spec) && spec.UpgradePathId is { } path && _ladders.TryGetValue(path, out var ladder)
            ? ladder
            : null;

    public IReadOnlyCollection<ItemSpec> Specs => _specs.Values;

    // ------------------------------------------------------------------ mapping

    public static ItemSpec ToSpec(ItemDef def)
    {
        var grants = new List<BonusLine>();

        foreach (var (key, value) in def.BaseStats)
        {
            if (key is WeaponDamageMin or WeaponDamageMax or ArmorValueKey) continue;

            // Anything else in base_stats is a modifier the item grants outright: a ring's
            // flat stats, or what a socket stone contributes. Unknown keys are dropped here
            // and reported by the content validator, which is where a designer will see them.
            if (BonusStat.TryParse(key, out var stat, out _))
            {
                grants.Add(new BonusLine(key, stat, value));
            }
        }

        var lines = def.BonusLineCount;

        return new ItemSpec
        {
            Id = def.Id,
            Slot = def.Slot,
            Rarity = def.Rarity,
            LevelReq = def.LevelReq,
            Width = def.GridSize.Length > 0 ? Math.Max(1, def.GridSize[0]) : 1,
            Height = def.GridSize.Length > 1 ? Math.Max(1, def.GridSize[1]) : 1,
            MaxStack = def.Slot is null ? Math.Max(1, def.MaxStack) : 1,
            SellValue = def.SellValue,
            WeaponDamageMin = Stat(def, WeaponDamageMin),
            WeaponDamageMax = Stat(def, WeaponDamageMax),
            ArmorValue = Stat(def, ArmorValueKey),
            Sockets = def.Sockets,
            MinBonusLines = lines.Length > 0 ? lines[0] : 0,
            MaxBonusLines = lines.Length > 1 ? lines[1] : lines.Length > 0 ? lines[0] : 0,
            BonusPoolId = def.BonusPool,
            UpgradePathId = def.UpgradePath,
            ClassRestriction = [.. def.ClassRestriction.Select(ParseClass).Where(c => c is not null).Select(c => c!.Value)],
            Grants = grants,
        };
    }

    private static double Stat(ItemDef def, string key) => def.BaseStats.GetValueOrDefault(key, 0);

    private static CharacterClass? ParseClass(string name) =>
        Enum.TryParse<CharacterClass>(name, ignoreCase: true, out var cls) && Enum.IsDefined(cls) ? cls : null;

    public static BonusPool ToPool(BonusPoolDef def)
    {
        var lines = new List<BonusPoolEntry>();

        foreach (var line in def.Lines)
        {
            if (!BonusStat.TryParse(line.Stat, out var stat, out _)) continue;

            var min = line.Range.Length > 0 ? line.Range[0] : 0;
            var max = line.Range.Length > 1 ? line.Range[1] : min;

            lines.Add(new BonusPoolEntry(line.Id, stat, min, max, line.Weight));
        }

        return new BonusPool(def.Id, lines);
    }

    public static UpgradeLadder ToLadder(UpgradePathDef def) =>
        new(def.Id, [.. def.Steps.Select(s => new UpgradeStep(s.To, s.Yang, s.Mats, s.Chance, s.Pity))]);
}
