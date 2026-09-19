using System.Text.Json;
using System.Text.Json.Serialization;
using Kiln.Data.Definitions;

namespace Kiln.Data.Loading;

/// <summary>
/// Loads every JSON file under the data root into a <see cref="ContentDatabase"/>.
/// Folder layout determines the definition type, so adding content is dropping a file in
/// the right folder — there is no registration step to forget.
/// </summary>
public static class ContentLoader
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false) },
    };

    /// <summary>Folder under the data root → the type it holds.</summary>
    private static readonly (string Folder, Type Type)[] Layout =
    [
        ("items", typeof(ItemDef)),
        ("enemies", typeof(EnemyDef)),
        ("skills", typeof(SkillDef)),
        ("quests", typeof(QuestDef)),
        ("shards", typeof(ShardDef)),
        ("zones", typeof(ZoneDef)),
    ];

    /// <summary>Files under <c>tables/</c> that hold a specific type.</summary>
    private static readonly (string File, Type Type)[] TableFiles =
    [
        ("tables/drop_tables.json", typeof(DropTableDef)),
        ("tables/bonus_pools.json", typeof(BonusPoolDef)),
        ("tables/upgrade_paths.json", typeof(UpgradePathDef)),
        ("tables/visuals.json", typeof(VisualDef)),
    ];

    public static ContentLoadResult LoadFromDirectory(string dataRoot)
    {
        if (!Directory.Exists(dataRoot))
        {
            return new ContentLoadResult(ContentDatabase.Empty(), [$"Data root not found: {dataRoot}"]);
        }

        return Load(new DiskContentFileSource(dataRoot));
    }

    public static ContentLoadResult Load(IContentFileSource source)
    {
        var ctx = new LoadContext();

        foreach (var (folder, type) in Layout)
        {
            if (!source.DirectoryExists(folder)) continue;

            foreach (var file in source.EnumerateJsonFiles(folder))
            {
                LoadFile(source, file, type, ctx);
            }
        }

        foreach (var (file, type) in TableFiles)
        {
            if (source.FileExists(file))
            {
                LoadFile(source, file, type, ctx);
            }
        }

        var db = new ContentDatabase
        {
            Items = ctx.Items,
            Enemies = ctx.Enemies,
            Skills = ctx.Skills,
            Quests = ctx.Quests,
            DropTables = ctx.DropTables,
            BonusPools = ctx.BonusPools,
            UpgradePaths = ctx.UpgradePaths,
            Visuals = ctx.Visuals,
            Shards = ctx.Shards,
            Zones = ctx.Zones,
        };

        return new ContentLoadResult(db, ctx.Errors);
    }

    private sealed class LoadContext
    {
        public readonly List<string> Errors = [];
        public readonly Dictionary<string, ItemDef> Items = new(StringComparer.Ordinal);
        public readonly Dictionary<string, EnemyDef> Enemies = new(StringComparer.Ordinal);
        public readonly Dictionary<string, SkillDef> Skills = new(StringComparer.Ordinal);
        public readonly Dictionary<string, QuestDef> Quests = new(StringComparer.Ordinal);
        public readonly Dictionary<string, DropTableDef> DropTables = new(StringComparer.Ordinal);
        public readonly Dictionary<string, BonusPoolDef> BonusPools = new(StringComparer.Ordinal);
        public readonly Dictionary<string, UpgradePathDef> UpgradePaths = new(StringComparer.Ordinal);
        public readonly Dictionary<string, VisualDef> Visuals = new(StringComparer.Ordinal);
        public readonly Dictionary<string, ShardDef> Shards = new(StringComparer.Ordinal);
        public readonly Dictionary<string, ZoneDef> Zones = new(StringComparer.Ordinal);
    }

    private static void LoadFile(IContentFileSource source, string file, Type type, LoadContext ctx)
    {
        string json;
        try
        {
            json = source.ReadAllText(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ctx.Errors.Add($"{file}: could not be read — {ex.Message}");
            return;
        }

        try
        {
            switch (type)
            {
                case var t when t == typeof(ItemDef):
                    Add(Deserialize<ItemDef>(json), file, ctx.Items, ctx.Errors);
                    break;
                case var t when t == typeof(EnemyDef):
                    Add(Deserialize<EnemyDef>(json), file, ctx.Enemies, ctx.Errors);
                    break;
                case var t when t == typeof(SkillDef):
                    Add(Deserialize<SkillDef>(json), file, ctx.Skills, ctx.Errors);
                    break;
                case var t when t == typeof(QuestDef):
                    Add(Deserialize<QuestDef>(json), file, ctx.Quests, ctx.Errors);
                    break;
                case var t when t == typeof(DropTableDef):
                    Add(Deserialize<DropTableDef>(json), file, ctx.DropTables, ctx.Errors);
                    break;
                case var t when t == typeof(BonusPoolDef):
                    Add(Deserialize<BonusPoolDef>(json), file, ctx.BonusPools, ctx.Errors);
                    break;
                case var t when t == typeof(UpgradePathDef):
                    Add(Deserialize<UpgradePathDef>(json), file, ctx.UpgradePaths, ctx.Errors);
                    break;
                case var t when t == typeof(ShardDef):
                    Add(Deserialize<ShardDef>(json), file, ctx.Shards, ctx.Errors);
                    break;
                case var t when t == typeof(VisualDef):
                    Add(Deserialize<VisualDef>(json), file, ctx.Visuals, ctx.Errors);
                    break;
                case var t when t == typeof(ZoneDef):
                    Add(Deserialize<ZoneDef>(json), file, ctx.Zones, ctx.Errors);
                    break;
            }
        }
        catch (JsonException ex)
        {
            // Must name the file and position, or a designer cannot act on it.
            ctx.Errors.Add($"{file}: invalid JSON — {ex.Message}");
        }
    }

    private static List<T> Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];

    private static void Add<T>(List<T> loaded, string file, Dictionary<string, T> target, List<string> errors)
        where T : ContentDefBase
    {
        foreach (var def in loaded)
        {
            def.SourceFile = file;

            if (string.IsNullOrEmpty(def.Id))
            {
                errors.Add($"{file}: a {typeof(T).Name} entry has no id.");
                continue;
            }

            if (!target.TryAdd(def.Id, def))
            {
                errors.Add($"{file}: duplicate id '{def.Id}' — already defined in {target[def.Id].SourceFile}.");
            }
        }
    }
}

public sealed record ContentLoadResult(ContentDatabase Database, IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0;
}
