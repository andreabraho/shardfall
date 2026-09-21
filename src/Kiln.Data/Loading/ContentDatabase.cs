using Kiln.Data.Definitions;

namespace Kiln.Data.Loading;

/// <summary>
/// The loaded, validated content of the game. Built once at startup and treated as
/// immutable. Adding an item, enemy, skill or quest requires no code change (NFR-M.2) —
/// the only thing that ever changes is the JSON under <c>game/data/</c>.
/// </summary>
public sealed class ContentDatabase
{
    public required IReadOnlyDictionary<string, ItemDef> Items { get; init; }
    public required IReadOnlyDictionary<string, EnemyDef> Enemies { get; init; }
    public required IReadOnlyDictionary<string, SkillDef> Skills { get; init; }
    public required IReadOnlyDictionary<string, QuestDef> Quests { get; init; }
    public required IReadOnlyDictionary<string, DropTableDef> DropTables { get; init; }
    public required IReadOnlyDictionary<string, BonusPoolDef> BonusPools { get; init; }
    public required IReadOnlyDictionary<string, UpgradePathDef> UpgradePaths { get; init; }

    /// <summary>The art-swap boundary (ENG-07): logical visual id → what currently renders it.</summary>
    public required IReadOnlyDictionary<string, VisualDef> Visuals { get; init; }

    public required IReadOnlyDictionary<string, ShardDef> Shards { get; init; }

    /// <summary>The world graph (WLD-01). Shrines and spawn fields are nested inside these.</summary>
    public required IReadOnlyDictionary<string, ZoneDef> Zones { get; init; }

    /// <summary>The greybox kit (WLD-04): logical piece id → what currently renders it.</summary>
    public required IReadOnlyDictionary<string, KitPieceDef> KitPieces { get; init; }

    /// <summary>Villagers (QST-06). Where they stand is the scene's; what they say and sell is here.</summary>
    public IReadOnlyDictionary<string, NpcDef> Npcs { get; init; } = new Dictionary<string, NpcDef>();

    /// <summary>
    /// Language code → its table (UIX-06): <c>$content.keys</c> and, for languages other than
    /// English, English interface text → its translation. From <c>data/strings/&lt;code&gt;.json</c>.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Strings { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, string>>();

    /// <summary>The table for a language, or an empty one.</summary>
    public IReadOnlyDictionary<string, string> StringsFor(string language) =>
        Strings.TryGetValue(language, out var table) ? table : new Dictionary<string, string>();

    public int TotalDefinitions =>
        Items.Count + Enemies.Count + Skills.Count + Quests.Count +
        DropTables.Count + BonusPools.Count + UpgradePaths.Count + Visuals.Count + Shards.Count +
        Zones.Count + KitPieces.Count + Npcs.Count;

    /// <summary>All definitions, for rules that apply across every type (id format, uniqueness).</summary>
    public IEnumerable<IContentDef> All()
    {
        foreach (var d in Items.Values) yield return d;
        foreach (var d in Enemies.Values) yield return d;
        foreach (var d in Skills.Values) yield return d;
        foreach (var d in Quests.Values) yield return d;
        foreach (var d in DropTables.Values) yield return d;
        foreach (var d in BonusPools.Values) yield return d;
        foreach (var d in UpgradePaths.Values) yield return d;
        foreach (var d in Visuals.Values) yield return d;
        foreach (var d in Shards.Values) yield return d;
        foreach (var d in Zones.Values) yield return d;
        foreach (var d in KitPieces.Values) yield return d;
        foreach (var d in Npcs.Values) yield return d;
    }

    /// <summary>True when the id exists in any registry — used by cross-reference checks.</summary>
    public bool Exists(string id) =>
        Items.ContainsKey(id) || Enemies.ContainsKey(id) || Skills.ContainsKey(id) ||
        Quests.ContainsKey(id) || DropTables.ContainsKey(id) || BonusPools.ContainsKey(id) ||
        UpgradePaths.ContainsKey(id) || Visuals.ContainsKey(id) || Shards.ContainsKey(id) ||
        Zones.ContainsKey(id) || KitPieces.ContainsKey(id) || Npcs.ContainsKey(id);

    public static ContentDatabase Empty() => new()
    {
        Items = new Dictionary<string, ItemDef>(),
        Enemies = new Dictionary<string, EnemyDef>(),
        Skills = new Dictionary<string, SkillDef>(),
        Quests = new Dictionary<string, QuestDef>(),
        DropTables = new Dictionary<string, DropTableDef>(),
        BonusPools = new Dictionary<string, BonusPoolDef>(),
        UpgradePaths = new Dictionary<string, UpgradePathDef>(),
        Visuals = new Dictionary<string, VisualDef>(),
        Shards = new Dictionary<string, ShardDef>(),
        Zones = new Dictionary<string, ZoneDef>(),
        KitPieces = new Dictionary<string, KitPieceDef>(),
    };
}
