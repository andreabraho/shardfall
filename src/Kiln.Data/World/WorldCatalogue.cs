using Kiln.Core.World;
using Kiln.Data.Definitions;
using Kiln.Data.Loading;
using CoreSpawnField = Kiln.Core.World.SpawnFieldDef;
using DataSpawnField = Kiln.Data.Definitions.SpawnFieldDef;

namespace Kiln.Data.World;

/// <summary>
/// Projects zone content into the engine-free world model: the graph, its shrines and the
/// tuning of every spawn field.
/// </summary>
public sealed class WorldCatalogue
{
    private readonly Dictionary<string, CoreSpawnField> _fields = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _fieldZone = new(StringComparer.Ordinal);

    public WorldCatalogue(ContentDatabase content)
    {
        var zones = new List<Zone>();
        var shrines = new List<Shrine>();
        var safe = new List<SafeRegion>();

        foreach (var def in content.Zones.Values.OrderBy(z => z.Id, StringComparer.Ordinal))
        {
            zones.Add(ToZone(def));

            foreach (var shrine in def.Shrines)
            {
                shrines.Add(new Shrine(shrine.Id, shrine.Name, def.Id, shrine.FastTravel));
            }

            foreach (var region in def.SafeRegions)
            {
                safe.Add(new SafeRegion(region.Id, region.Name, def.Id, region.Radius));
            }

            foreach (var field in def.SpawnFields)
            {
                _fields[field.Id] = ToField(field);
                _fieldZone[field.Id] = def.Id;
            }
        }

        Graph = new ZoneGraph(zones, shrines, safe);
    }

    public ZoneGraph Graph { get; }

    public IReadOnlyDictionary<string, CoreSpawnField> Fields => _fields;

    public CoreSpawnField? Field(string id) => _fields.GetValueOrDefault(id);

    /// <summary>Which zone a field belongs to. Used to reject a scene placing a foreign field.</summary>
    public string? ZoneOf(string fieldId) => _fieldZone.GetValueOrDefault(fieldId);

    public static ZoneKind KindOf(string kind) => kind switch
    {
        "hub" => ZoneKind.Hub,
        "dungeon" => ZoneKind.Dungeon,
        _ => ZoneKind.Wilds,
    };

    private static Zone ToZone(ZoneDef def)
    {
        var band = def.LevelBand.Length > 1
            ? new LevelBand(def.LevelBand[0], def.LevelBand[1])
            : new LevelBand(1, 1);

        var exits = def.Exits
            .Select(e => new ZoneExit(e.To, e.RequiredLevel, e.RequiredQuest))
            .ToList();

        return new Zone(
            def.Id,
            def.Name,
            KindOf(def.Kind),
            band,
            def.Scene,
            exits,
            def.Shrines.Select(s => s.Id).ToList())
        {
            Floors = def.Floors.Select(ToFloor).ToList(),
        };
    }

    /// <summary>Depth comes from the list's order — see <see cref="FloorDef"/>.</summary>
    private static TowerFloor ToFloor(FloorDef def, int position) => new(
        def.Id,
        position + 1,
        def.Name,
        TaskOf(def.Task),
        def.Targets,
        def.Decoys,
        def.Seconds,
        def.Boss,
        def.Shrine,
        def.Bench,
        def.Refuge)
    {
        Waves = def.Waves,
        WaveSize = def.WaveSize,
        WaveSeconds = def.WaveSeconds,
    };

    /// <summary>
    /// The verb, by name.
    /// </summary>
    /// <remarks>
    /// An unknown verb falls back to Break rather than throwing, because the validator rejects
    /// it by name with a line number and that is a far better error than a stack trace from
    /// inside the loader. The fallback exists so the validator gets to run at all.
    /// </remarks>
    public static FloorTask TaskOf(string task) => task switch
    {
        "hold" => FloorTask.Hold,
        "find" => FloorTask.Find,
        "carry" => FloorTask.Carry,
        "race" => FloorTask.Race,
        "fight" => FloorTask.Fight,
        _ => FloorTask.Break,
    };

    private static CoreSpawnField ToField(DataSpawnField def) => new(
        def.Id,
        def.Entries.Select(e => new SpawnEntry(e.Enemy, e.Weight)).ToList(),
        def.Count,
        def.RespawnSeconds,
        def.ActivationRadius,
        def.Radius);
}
