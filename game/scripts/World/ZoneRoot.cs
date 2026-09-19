using System.Linq;
using Godot;

namespace Kiln.Game.World;

/// <summary>
/// Sits on a zone scene's root and says which zone this is (WLD-01).
/// </summary>
/// <remarks>
/// The link between a scene and its content is one exported string, deliberately. Everything
/// else about a zone — its band, its exits, its shrines, what its camps are made of — is data
/// the validator can check; the scene only supplies the coordinates data cannot hold.
/// </remarks>
public partial class ZoneRoot : Node3D
{
    [Export] public string ZoneId { get; set; } = "";

    /// <remarks>
    /// Runs after every child, since Godot readies children first. That is what makes the
    /// audit below possible: by this point each spawn field and village marker in the scene
    /// has registered itself, so the scene and the data are both known at the same moment.
    /// </remarks>
    public override void _Ready()
    {
        if (!GameWorld.IsLoaded)
        {
            GD.PushError("[world] a zone scene loaded before GameWorld.Load() ran.");
            return;
        }

        GameWorld.EnterZone(ZoneId);
        Audit();

        Debug.DebugOverlay.Register("zone", () =>
        {
            var zone = GameWorld.CurrentZone;

            if (zone is null) return "—";

            var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
            var where = player is not null && GameWorld.IsSafe(player.GlobalPosition) ? " · SAFE" : "";

            return $"{zone.Id} {zone.Band} · "
                + $"{GameWorld.LivingEnemies(GetTree())}/{GameWorld.PopulationCap} alive{where}";
        });
    }

    /// <summary>
    /// Checks the laid-out scene against the promises the data makes about it.
    /// </summary>
    /// <remarks>
    /// The content validator cannot do this: it never sees a coordinate. A camp dragged onto
    /// the edge of the village is valid content in an invalid place, and the only moment the
    /// two halves are both known is here, once every child has registered itself.
    /// </remarks>
    private void Audit()
    {
        var declared = GameWorld.Graph.SafeRegionsIn(ZoneId).Select(r => r.Id).ToHashSet();

        foreach (var node in GetTree().GetNodesInGroup("safe_zones"))
        {
            if (node is not SafeZoneNode marker) continue;

            declared.Remove(marker.RegionId);

            var home = GameWorld.Graph.SafeRegion(marker.RegionId)?.Zone;

            if (home is not null && home != ZoneId)
            {
                GD.PushWarning($"[safe] '{marker.RegionId}' belongs to '{home}' but stands in '{ZoneId}'.");
            }
        }

        // A village the data promises and the scene never places is the quiet version of this
        // going wrong: the map says sanctuary, the ground does not.
        foreach (var missing in declared)
        {
            GD.PushError($"[safe] '{ZoneId}' declares safe region '{missing}' but the scene places no marker for it.");
        }

        foreach (var node in GetTree().GetNodesInGroup("spawn_fields"))
        {
            if (node is not SpawnFieldNode field || field.Def is not { } def) continue;

            var owner = GameWorld.Catalogue.ZoneOf(field.FieldId);

            // Checked here rather than in the field's own _Ready, where the current zone is
            // still the previous scene's and the comparison was quietly always false.
            if (owner is not null && owner != ZoneId)
            {
                GD.PushWarning($"[spawn] '{field.FieldId}' belongs to '{owner}' but stands in '{ZoneId}'.");
            }

            var hit = GameWorld.Safety.Encroaches(
                field.GlobalPosition.X, field.GlobalPosition.Z, def.Radius);

            if (hit is null) continue;

            GD.PushError(
                $"[safe] spawn field '{field.FieldId}' comes within "
                + $"{Kiln.Core.World.SafetyField.SpawnClearance} m of safe region '{hit}'. "
                + "Move the camp: creatures would aggro someone standing safely inside.");
        }
    }
}
