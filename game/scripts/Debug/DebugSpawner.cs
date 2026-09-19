using System.Collections.Generic;
using Godot;
using Kiln.Game.Input;

namespace Kiln.Game.Debug;

/// <summary>
/// Refills the test arena with enemies so a system can be exercised for longer than one
/// clear takes.
/// </summary>
/// <remarks>
/// Purely a development tool, and one worth having: the drop tables, the upgrade ladder and
/// the socket bench all need dozens of kills to judge, and an arena that empties after ninety
/// seconds means every session is spent restarting rather than playing. Real zones get proper
/// spawn rules in Phase 6; this is the scaffolding that makes the systems testable now.
/// <para>
/// It remembers the arena's original placements at startup rather than inventing positions,
/// so a respawned wave stands where the scene author put it — packs stay packs, and the
/// archer stays at the range it was placed at.
/// </para>
/// </remarks>
public partial class DebugSpawner : Node
{
    private sealed record Placement(string EnemyId, Vector3 Position);

    private readonly List<Placement> _placements = [];
    private Node3D? _container;
    private PackedScene? _enemyScene;
    private double _sinceCheck;

    /// <summary>Seconds between refills. The arena repopulates on its own so testing never stalls.</summary>
    [Export] public double RespawnSeconds { get; set; } = 12.0;

    /// <summary>
    /// On by default: an empty arena is the problem this node exists to solve. F7 turns it off
    /// when a test needs the arena to stay cleared — checking leashing, or that a pack really
    /// is dead.
    /// </summary>
    [Export] public bool AutoRespawn { get; set; } = true;

    /// <summary>Enemies are only refilled while the player is at least this far from the spot.</summary>
    [Export] public float SafeDistance { get; set; } = 8f;

    public override void _Ready()
    {
        // Debug-only. A release build must never repopulate a zone behind the player's back.
        if (!OS.IsDebugBuild())
        {
            QueueFree();
            return;
        }

        _enemyScene = GD.Load<PackedScene>("res://scenes/enemy.tscn");

        CallDeferred(nameof(RecordPlacements));

        DebugOverlay.Register("spawner", () =>
            _container is null
                ? "no arena"
                : $"{LivingCount()}/{_placements.Count} alive · auto {(AutoRespawn ? "on" : "off")}");
    }

    /// <summary>Snapshots where the scene author put each enemy, before any of them die.</summary>
    private void RecordPlacements()
    {
        _container = GetTree().CurrentScene?.GetNodeOrNull<Node3D>("Enemies");

        if (_container is null)
        {
            GD.Print("[spawner] no Enemies node in this scene — spawning disabled");
            return;
        }

        foreach (var child in _container.GetChildren())
        {
            if (child is not Combat.EnemyBrain brain) continue;

            _placements.Add(new Placement(brain.EnemyId, brain.GlobalPosition));
        }

        GD.Print($"[spawner] recorded {_placements.Count} placements — F5 refills, F6 spawns one, F7 toggles auto");
    }

    private int LivingCount()
    {
        if (_container is null) return 0;

        var count = 0;

        foreach (var child in _container.GetChildren())
        {
            if (child is Combat.EnemyBrain { IsDead: false }) count++;
        }

        return count;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(GameActions.DebugSpawnWave))
        {
            var spawned = Refill(force: true);
            GD.Print($"[spawner] refilled {spawned} enemies");
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
            AutoRespawn = !AutoRespawn;
            GD.Print($"[spawner] auto-respawn {(AutoRespawn ? "on" : "off")}");
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (!AutoRespawn || _container is null) return;

        _sinceCheck += delta;

        if (_sinceCheck < RespawnSeconds) return;

        _sinceCheck = 0;
        Refill(force: false);
    }

    /// <summary>
    /// Puts back whatever is missing. Returns how many were spawned.
    /// </summary>
    /// <param name="force">
    /// Ignores the safe-distance rule. Pressing the key means "now", even if the player is
    /// standing on the spot; the timed refill stays polite and never materialises an enemy
    /// in the player's face.
    /// </param>
    private int Refill(bool force)
    {
        if (_container is null || _enemyScene is null) return 0;

        // The hard cap. Occupancy below is judged by position, and an enemy chasing the
        // player is nowhere near the spot it started from — without this, every pull would
        // free up a placement and the arena would fill without limit.
        var budget = _placements.Count - LivingCount();

        if (budget <= 0) return 0;

        var occupied = new List<Vector3>();

        foreach (var child in _container.GetChildren())
        {
            if (child is Combat.EnemyBrain { IsDead: false } brain) occupied.Add(brain.GlobalPosition);
        }

        var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
        var spawned = 0;

        foreach (var placement in _placements)
        {
            if (spawned >= budget) break;

            if (IsTaken(placement.Position, occupied)) continue;

            if (!force && player is not null
                && player.GlobalPosition.DistanceTo(placement.Position) < SafeDistance)
            {
                continue;
            }

            Spawn(placement.EnemyId, placement.Position);
            occupied.Add(placement.Position);
            spawned++;
        }

        return spawned;
    }

    private static bool IsTaken(Vector3 position, List<Vector3> occupied)
    {
        foreach (var taken in occupied)
        {
            if (taken.DistanceTo(position) < 2.5f) return true;
        }

        return false;
    }

    /// <summary>One enemy a few metres from the player, for testing a single fight.</summary>
    private void SpawnNearPlayer()
    {
        if (_container is null || _placements.Count == 0) return;

        if (GetTree().GetFirstNodeInGroup("player") is not Node3D player)
        {
            GD.Print("[spawner] no player to spawn next to");
            return;
        }

        var placement = _placements[(int)(GD.Randi() % (uint)_placements.Count)];
        var angle = GD.RandRange(0, Mathf.Tau);
        var offset = new Vector3((float)Mathf.Cos(angle), 0, (float)Mathf.Sin(angle)) * 6f;

        Spawn(placement.EnemyId, player.GlobalPosition + offset);
        GD.Print($"[spawner] spawned {placement.EnemyId}");
    }

    private void Spawn(string enemyId, Vector3 at)
    {
        if (_enemyScene?.Instantiate() is not Combat.EnemyBrain enemy) return;

        // Set before the node enters the tree: EnemyBrain reads its definition in _Ready.
        enemy.EnemyId = enemyId;
        enemy.Name = $"Spawned_{enemyId}_{Time.GetTicksMsec()}";

        _container!.AddChild(enemy);
        enemy.GlobalPosition = at;
    }
}
