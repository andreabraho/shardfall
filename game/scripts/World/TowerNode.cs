using System.Collections.Generic;
using System.Linq;
using Godot;
using Kiln.Core.World;

namespace Kiln.Game.World;

/// <summary>
/// The dungeon as a stack of floors (FR-7.11): which floor the player is on, what it is
/// asking of them, and when the way down opens.
/// </summary>
/// <remarks>
/// The decisions live in <see cref="TowerRun"/>, which has no engine in it and is tested
/// without one. This node is the join between that and the scene: it hands the run its
/// floors, tells it when a target went down, and turns what it says back into rooms being
/// switched on and off.
/// <para>
/// A tower is one scene, not nine. The floors are stacked in world space and only the
/// occupied one processes, which keeps the whole dungeon inside one navigation bake and one
/// load — and means descending is a teleport and a fade rather than a scene change the player
/// waits through nine times.
/// </para>
/// </remarks>
public partial class TowerNode : Node3D
{
    private readonly Dictionary<string, TowerFloorNode> _rooms = new(System.StringComparer.Ordinal);

    private TowerRun? _run;
    private TowerFloorNode? _here;

    /// <summary>The run in progress, or null before the zone has loaded.</summary>
    public TowerRun? Run => _run;

    /// <summary>Raised whenever the floor, its progress or its clock changes. The HUD listens.</summary>
    [Signal] public delegate void FloorChangedEventHandler();

    public override void _Ready()
    {
        AddToGroup("tower");

        if (!GameWorld.IsLoaded)
        {
            GD.PushError("[tower] loaded before GameWorld.Load() ran.");
            return;
        }

        // Deferred, all of it: a node is ready before its parent, so at this point the zone
        // root has not entered its zone yet and GameWorld still names the map the player just
        // left. Asking for "the current zone" here reads the previous scene.
        CallDeferred(nameof(Begin));
    }

    /// <summary>Reads the zone, matches its floors to the rooms, and opens the first one.</summary>
    private void Begin()
    {
        var zone = GameWorld.Graph[GameWorld.CurrentZoneId];

        if (zone is null || !zone.IsTower)
        {
            GD.PushError($"[tower] '{GameWorld.CurrentZoneId}' declares no floors.");
            return;
        }

        Index(zone);

        // Resuming where the player left off (FR-7.20). A tower re-entered from the top after
        // eight floors is a tower nobody re-enters.
        _run = new TowerRun(zone.Floors, PlayerProfile.DepthIn(zone.Id));

        Arrive();
    }

    /// <summary>Matches each floor in the data to the room in the scene that lays it out.</summary>
    private void Index(Zone zone)
    {
        foreach (var node in GetChildren())
        {
            if (node is TowerFloorNode room) _rooms[room.FloorId] = room;
        }

        foreach (var floor in zone.Floors)
        {
            if (_rooms.TryGetValue(floor.Id, out var room))
            {
                room.Attach(this, floor);
                continue;
            }

            GD.PushError($"[tower] '{zone.Id}' declares floor '{floor.Id}' but the scene lays out no room for it.");
        }

        foreach (var id in _rooms.Keys)
        {
            if (zone.Floors.Any(f => f.Id == id)) continue;

            GD.PushError($"[tower] the scene lays out room '{id}', which '{zone.Id}' does not declare.");
        }
    }

    public override void _Process(double delta)
    {
        if (_run is null) return;

        var before = _run.Phase;

        _run.Tick(delta);

        if (_run.JustFailed)
        {
            UI.WorldNotice.Show(GetTree(), "Out of time. The floor resets.");
            _here?.Activate();
        }

        if (_run.Phase != before && _run.Phase == FloorPhase.Open) Finish();

        // Every frame: the HUD shows a clock, and a clock that updates on events does not
        // move.
        EmitSignal(SignalName.FloorChanged);
    }

    /// <summary>Reported by a floor when one of its real targets went down.</summary>
    public void Scored()
    {
        if (_run is null || _run.Phase != FloorPhase.Running) return;

        _run.ScoreTarget();

        if (_run.Phase == FloorPhase.Open)
        {
            Finish();
            return;
        }

        // Said out loud, because on a Carry or Race floor the only feedback otherwise is a
        // number in the corner the player is much too busy to read.
        UI.WorldNotice.Show(GetTree(), $"{_run.Scored} of {_run.Floor.Targets}.");
        EmitSignal(SignalName.FloorChanged);
    }

    /// <summary>Takes the player down one floor, or back out at the bottom. Called by the stair.</summary>
    public void Descend()
    {
        if (_run is null) return;

        // The bottom of the tower leads back to the mouth, which is where the way out of the
        // dungeon is. The deepest floor reached is already recorded, so walking back up costs
        // nothing: re-entering later still resumes at the bottom.
        if (_run.Finished)
        {
            var zone = GameWorld.Graph[GameWorld.CurrentZoneId];

            if (zone is null) return;

            _run = new TowerRun(zone.Floors);
            Arrive();

            return;
        }

        if (!_run.Descend()) return;

        Checkpoint();
        Arrive();
    }

    /// <summary>Switches the world over to the current floor and puts the player on it.</summary>
    private void Arrive()
    {
        if (_run is null) return;

        _here?.Deactivate();

        if (!_rooms.TryGetValue(_run.Floor.Id, out var room))
        {
            GD.PushError($"[tower] no room for floor '{_run.Floor.Id}'.");
            return;
        }

        _here = room;
        room.Activate();

        if (GetTree().GetFirstNodeInGroup("player") is Node3D player)
        {
            player.GlobalPosition = room.Entry.GlobalPosition;
        }

        GD.Print($"[tower] floor {_run.Depth}/{_run.FloorCount} — {_run.Floor.Id} ({_run.Floor.Task})");

        UI.WorldNotice.Show(GetTree(),
            $"{Items.GameItems.Localise(_run.Floor.Name)}  ·  {Brief(_run.Floor)}");

        EmitSignal(SignalName.FloorChanged);
    }

    /// <summary>
    /// The task in one line, in the imperative.
    /// </summary>
    /// <remarks>
    /// Written out per verb rather than assembled from the numbers, because "break 1 target"
    /// is not a sentence anybody reads. The player has to know what to do within a second of
    /// landing, and the one place that is guaranteed to be read is the line that appears when
    /// they arrive.
    /// </remarks>
    public static string Brief(TowerFloor floor) => floor.Task switch
    {
        FloorTask.Break => "Break the seal.",
        FloorTask.Hold => $"Hold for {floor.Seconds:F0} seconds.",
        FloorTask.Find => "One lantern is not breathing with the others. Break that one.",
        FloorTask.Carry => $"Break all {floor.Targets} keystones.",
        FloorTask.Race => $"Break {floor.Targets}. The clock starts with the first.",
        FloorTask.Fight => "Kill what is waiting.",
        _ => "",
    };

    private void Finish()
    {
        if (_run is null) return;

        _here?.Open();

        if (_run.Finished)
        {
            UI.WorldNotice.Show(GetTree(), "The tower is finished.");
            GD.Print("[tower] cleared");
        }

        EmitSignal(SignalName.FloorChanged);
    }

    /// <summary>
    /// Records how far down the player got, but only on a floor with a shrine (FR-7.20).
    /// </summary>
    /// <remarks>
    /// Checkpoint granularity rather than every floor, and the reason is the way back out. A
    /// run resumed onto a floor with no shrine is a player dropped in front of a boss with no
    /// rest point and no way to leave except through it. Every shrine floor is somewhere they
    /// can stand, heal and travel out from, so resuming is never a trap.
    /// </remarks>
    private void Checkpoint()
    {
        if (_run is null || _run.Floor.Shrine.Length == 0) return;

        PlayerProfile.ReachedDepth(GameWorld.CurrentZoneId, _run.Depth);
    }
}
