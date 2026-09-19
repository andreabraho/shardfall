using Godot;
using Kiln.Core.World;
using Kiln.Data.World;

namespace Kiln.Game.World;

/// <summary>
/// The session's world: the zone graph, the shrines the player has found, and the one budget
/// every spawn field spends from.
/// </summary>
/// <remarks>
/// Built once after content loads. The population cap lives here rather than in the fields
/// because the thing it protects — frame time — is global: three overlapping camps each
/// politely under its own cap still add up to a slideshow.
/// </remarks>
public static class GameWorld
{
    /// <summary>
    /// The hard ceiling on enemies standing anywhere at once.
    /// <para>
    /// Sized so the densest legitimate moment — a five-creature camp, a neighbouring camp
    /// pulled by accident and a shard's phase-three wave — fits under it with room to spare,
    /// while a runaway spawn bug still cannot fill the level.
    /// </para>
    /// </summary>
    public const int PopulationCap = 36;

    private static WorldCatalogue? _catalogue;
    private static FastTravelNetwork? _travel;

    public static bool IsLoaded => _catalogue is not null;

    public static WorldCatalogue Catalogue =>
        _catalogue ?? throw new System.InvalidOperationException(
            "GameWorld.Load() must run after GameContent.Load().");

    public static ZoneGraph Graph => Catalogue.Graph;

    public static FastTravelNetwork Travel =>
        _travel ??= new FastTravelNetwork(Graph);

    /// <summary>
    /// The safe ground of the loaded scene (WLD-12), and the only authority on whether a
    /// point is inside a village.
    /// </summary>
    /// <remarks>
    /// Scene-scoped rather than world-scoped: the coordinates only mean anything inside the
    /// scene that supplied them, so each marker adds itself on entering the tree and takes
    /// itself out on leaving. Clearing it on a zone change instead would make safety depend on
    /// whether the zone was announced before or after its markers woke up — an ordering that
    /// is not ours to control.
    /// </remarks>
    public static SafetyField Safety { get; } = new();

    /// <summary>Whether this point is on safe ground. Works for any body, not just the player.</summary>
    public static bool IsSafe(Vector3 at) => Safety.IsSafe(at.X, at.Z);

    /// <summary>The zone the loaded scene declares. Empty until a <see cref="ZoneRoot"/> reports one.</summary>
    public static string CurrentZoneId { get; private set; } = "";

    public static Zone? CurrentZone => CurrentZoneId.Length > 0 ? Graph[CurrentZoneId] : null;

    public static void Load()
    {
        _catalogue = new WorldCatalogue(GameContent.Database);
        _travel = new FastTravelNetwork(_catalogue.Graph);
        CurrentZoneId = "";

        GD.Print($"[world] {_catalogue.Graph.Zones.Count} zones, "
            + $"{_catalogue.Graph.Shrines.Count} shrines, {_catalogue.Fields.Count} spawn fields");
    }

    public static void EnterZone(string zoneId)
    {
        CurrentZoneId = zoneId;

        if (CurrentZone is { } zone)
        {
            GD.Print($"[world] entered {zone.Id} — {zone.Kind}, level {zone.Band}");
        }
        else
        {
            GD.PushWarning($"[world] scene declares unknown zone '{zoneId}'.");
        }
    }

    /// <summary>How many more creatures the world will accept right now.</summary>
    public static int Headroom(SceneTree tree) =>
        Mathf.Max(0, PopulationCap - LivingEnemies(tree));

    public static int LivingEnemies(SceneTree tree)
    {
        var count = 0;

        foreach (var node in tree.GetNodesInGroup("enemies"))
        {
            if (node is Combat.EnemyBrain { IsDead: false }) count++;
        }

        return count;
    }
}
