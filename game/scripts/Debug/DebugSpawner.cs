using Godot;
using Kiln.Game.Input;

namespace Kiln.Game.Debug;

/// <summary>
/// Shortcuts for exercising the spawn fields (WLD-03) without waiting on their timers.
/// </summary>
/// <remarks>
/// The arena used to be refilled by this node from a snapshot of its scene placements, which
/// was scaffolding for testing items before zones existed. The fields now own population, so
/// what is left is the two things a tester still cannot do by playing: empty the zone on
/// demand, and stop it refilling while something is being checked.
/// </remarks>
public partial class DebugSpawner : Node
{
    public override void _Ready()
    {
        // Debug-only. A release build must never let a key empty a zone.
        if (!OS.IsDebugBuild())
        {
            QueueFree();
            return;
        }

        GD.Print("[spawner] F5 clears every enemy · F6 spawns one next to you · F7 freezes the fields");

        DebugOverlay.Register("spawner", () =>
            $"{World.GameWorld.LivingEnemies(GetTree())}/{World.GameWorld.PopulationCap} alive"
            + (Frozen ? " · fields frozen" : ""));
    }

    /// <summary>
    /// Stops every field putting anything new in the world. For checking leashing, or that a
    /// camp really is dead, without a respawn arriving mid-observation.
    /// </summary>
    public static bool Frozen { get; private set; }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(GameActions.DebugSpawnWave))
        {
            GD.Print($"[spawner] cleared {ClearAll()} enemies");
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed(GameActions.DebugSpawnOne))
        {
            SpawnNearPlayer();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed(GameActions.DebugToggleRespawn))
        {
            Frozen = !Frozen;
            GD.Print($"[spawner] fields {(Frozen ? "frozen" : "running")}");
            GetViewport().SetInputAsHandled();
        }
    }

    private int ClearAll()
    {
        var count = 0;

        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is not Combat.EnemyBrain { IsDead: false } brain) continue;

            brain.QueueFree();
            count++;
        }

        return count;
    }

    /// <summary>One enemy from the nearest field's roster, a few metres away, for a single fight.</summary>
    private void SpawnNearPlayer()
    {
        if (GetTree().GetFirstNodeInGroup("player") is not Node3D player)
        {
            GD.Print("[spawner] no player to spawn next to");
            return;
        }

        var field = Nearest(player.GlobalPosition);

        if (field is null)
        {
            GD.Print("[spawner] no spawn field in this scene");
            return;
        }

        var def = World.GameWorld.Catalogue.Field(field.FieldId);

        if (def is null || def.Entries.Count == 0) return;

        if (GD.Load<PackedScene>("res://scenes/enemy.tscn").Instantiate() is not Combat.EnemyBrain enemy) return;

        var angle = GD.RandRange(0, Mathf.Tau);

        enemy.EnemyId = def.Entries[(int)(GD.Randi() % (uint)def.Entries.Count)].EnemyId;
        enemy.Name = $"Debug_{enemy.EnemyId}_{Time.GetTicksMsec()}";

        GetTree().CurrentScene.AddChild(enemy);
        enemy.GlobalPosition = player.GlobalPosition
            + (new Vector3((float)Mathf.Cos(angle), 0, (float)Mathf.Sin(angle)) * 6f);

        GD.Print($"[spawner] spawned {enemy.EnemyId}");
    }

    private World.SpawnFieldNode? Nearest(Vector3 to)
    {
        World.SpawnFieldNode? best = null;
        var distance = float.MaxValue;

        foreach (var node in GetTree().GetNodesInGroup("spawn_fields"))
        {
            if (node is not World.SpawnFieldNode field) continue;

            var d = field.GlobalPosition.DistanceTo(to);

            if (d >= distance) continue;

            distance = d;
            best = field;
        }

        return best;
    }
}
