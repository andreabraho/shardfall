using System.Collections.Generic;
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
        PlaceArrivingPlayer();
        Audit();

        // Every border is a checkpoint. Deferred so a tower has already put the player on their
        // floor, and the save records where they actually are.
        Callable.From(() => Saving.SaveService.Autosave($"Entered {ZoneId}")).CallDeferred();

        Quests.QuestTracker.Report(GetTree(), Kiln.Core.Foundation.ObjectiveType.Reach, ZoneId);
        Quests.QuestTracker.Touch();

        Debug.DebugOverlay.Register("zone", this, () =>
        {
            var zone = GameWorld.CurrentZone;

            if (zone is null) return "—";

            var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
            var where = player is not null && GameWorld.IsSafe(player.GlobalPosition)
                ? "  [color=#7fc98a]SAFE[/color]"
                : "";

            return $"{zone.Id} {zone.Band} · "
                + $"{GameWorld.LivingEnemies(GetTree())}/{GameWorld.PopulationCap} alive{where}";
        });
    }

    /// <summary>
    /// Moves the player to the end of the road they walked in on.
    /// </summary>
    /// <remarks>
    /// Nothing happens on a cold start, so the scene's own Player position stays the new-game
    /// spawn. A missing arrival is a warning rather than an error: the player still lands
    /// somewhere sensible, and failing to load the map over it would be a worse trade.
    /// </remarks>
    private void PlaceArrivingPlayer()
    {
        // A load puts the player exactly where they saved, which beats any arrival point.
        if (Saving.SaveService.TakePendingPosition() is { } saved)
        {
            ZoneTransition.TakeArrival();

            if (GetTree().GetFirstNodeInGroup("player") is Node3D loaded) loaded.GlobalPosition = saved;

            return;
        }

        var from = ZoneTransition.TakeArrival();

        if (from.Length == 0) return;

        if (GetTree().GetFirstNodeInGroup("player") is not Node3D player) return;

        foreach (var node in GetTree().GetNodesInGroup("zone_arrivals"))
        {
            if (node is not ZoneArrival arrival || arrival.FromZone != from) continue;

            player.GlobalPosition = arrival.GlobalPosition;

            GD.Print($"[gate] arrived in {ZoneId} from {from} — level {PlayerProfile.Progression.Level}, "
                + $"{PlayerProfile.HealthFraction:P0} health");

            return;
        }

        GD.PushWarning($"[gate] '{ZoneId}' has no arrival point from '{from}'; using the scene's start.");
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

        // An exit in the data with no gate on the ground is a road that stops being a road
        // halfway along it. A warning, not an error, because the graph is deliberately
        // designed ahead of the maps and this is the running count of what is left to build.
        var unbuilt = new List<string>();

        foreach (var exit in GameWorld.Graph[ZoneId]?.Exits ?? [])
        {
            unbuilt.Add(exit.To);
        }

        foreach (var node in GetTree().GetNodesInGroup("zone_gates"))
        {
            if (node is not ZoneGate gate) continue;

            if (!unbuilt.Remove(gate.ToZone))
            {
                GD.PushError($"[gate] '{ZoneId}' has a gate to '{gate.ToZone}', which is not one of its exits.");
            }
        }

        foreach (var missing in unbuilt)
        {
            GD.PushWarning($"[gate] '{ZoneId}' exits to '{missing}' but the scene places no gate for it.");
        }

        // Which shards a zone hosts is content; where they stand is level design. A scene
        // placing one that belongs elsewhere gives the world two of it, each with its own
        // cooldown and its own drops — and the arena had exactly that until this rule existed.
        var hosted = GameWorld.Graph[ZoneId] is null
            ? []
            : GameContent.Database.Zones[ZoneId].Shards.ToHashSet(System.StringComparer.Ordinal);

        foreach (var node in GetTree().GetNodesInGroup("shards"))
        {
            if (node is ShardNode shard && !hosted.Contains(shard.ShardId))
            {
                GD.PushError($"[shard] '{shard.ShardId}' stands in '{ZoneId}', which does not host it.");
            }
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

            // Measured against how far the camp's creatures can get from it, not against the
            // camp's own footprint. A creature standing at the edge of its field with a
            // fourteen-metre leash reaches twenty-two metres past the marker, and walks into
            // the village without ever having chased anybody there.
            var reach = def.Radius + LeashOf(def) + Kiln.Core.World.SafetyField.ClearanceMargin;
            var hit = GameWorld.Safety.Encroaches(
                field.GlobalPosition.X, field.GlobalPosition.Z, def.Radius, reach - def.Radius);

            if (hit is null) continue;

            GD.PushError(
                $"[safe] spawn field '{field.FieldId}' comes within {reach:0.#} m of safe "
                + $"region '{hit}' — its creatures can reach that far from the marker. "
                + "Move the camp out, or shorten their leash.");
        }
    }

    /// <summary>The longest leash among the creatures a field can produce.</summary>
    /// <remarks>
    /// The whole roster, not an average: the camp is only as close to the village as its
    /// furthest-roaming member allows, and a one-in-five spawn that wanders into the square
    /// is a bug the player meets once and never forgets.
    /// </remarks>
    private static double LeashOf(Kiln.Core.World.SpawnFieldDef def)
    {
        var longest = 0.0;

        foreach (var entry in def.Entries)
        {
            if (GameContent.Database.Enemies.TryGetValue(entry.EnemyId, out var enemy))
            {
                longest = System.Math.Max(longest, enemy.LeashRadius);
            }
        }

        return longest;
    }
}
