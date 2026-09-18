using Sohan.Data.Definitions;

namespace Sohan.Data.Loading;

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

    public int TotalDefinitions =>
        Items.Count + Enemies.Count + Skills.Count + Quests.Count +
        DropTables.Count + BonusPools.Count + UpgradePaths.Count + Visuals.Count;

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
    }

    /// <summary>True when the id exists in any registry — used by cross-reference checks.</summary>
    public bool Exists(string id) =>
        Items.ContainsKey(id) || Enemies.ContainsKey(id) || Skills.ContainsKey(id) ||
        Quests.ContainsKey(id) || DropTables.ContainsKey(id) || BonusPools.ContainsKey(id) ||
        UpgradePaths.ContainsKey(id) || Visuals.ContainsKey(id);

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
    };
}
