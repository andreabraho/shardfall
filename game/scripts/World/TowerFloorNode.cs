using System.Collections.Generic;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.World;

namespace Kiln.Game.World;

/// <summary>
/// One floor of the tower: the room, and the furniture its task needs (FR-7.11).
/// </summary>
/// <remarks>
/// The scene supplies the room and the markers — where the player lands, where the stair is,
/// where things may stand. The data supplies the task. This node is the join: on activation
/// it reads the floor definition and puts pylons on the marks, and it is the only place that
/// knows a Find floor's decoys are the same prop as a Break floor's seal.
/// <para>
/// Only the floor the player is on runs. The other eight are parked with their processing
/// off, which is what makes a nine-floor scene cost roughly what a one-floor scene costs.
/// </para>
/// </remarks>
public partial class TowerFloorNode : Node3D
{
    private readonly List<FloorPylon> _pylons = [];
    private readonly List<Combat.EnemyBrain> _sent = [];

    private TowerNode? _tower;
    private TowerFloor? _floor;
    private PackedScene? _enemyScene;
    private Node3D? _props;
    private double _waveIn;
    private bool _live;

    /// <summary>Which floor this is. Must be one the zone declares.</summary>
    [Export] public string FloorId { get; set; } = "";

    /// <summary>Where the player lands when they arrive on this floor.</summary>
    public Node3D Entry => GetNode<Node3D>("Entry");

    /// <summary>The way down, shut until the task is finished.</summary>
    public FloorStair? Stair => GetNodeOrNull<FloorStair>("Stair");

    public TowerFloor? Floor => _floor;

    public override void _Ready()
    {
        AddToGroup("tower_floors");

        _enemyScene = GD.Load<PackedScene>("res://scenes/enemy.tscn");

        SetProcess(false);
        Visible = false;
    }

    /// <summary>Called by the tower once, before anything is activated.</summary>
    public void Attach(TowerNode tower, TowerFloor floor)
    {
        _tower = tower;
        _floor = floor;
    }

    /// <summary>
    /// Opens the floor for business: dresses it, and starts its clock.
    /// </summary>
    public void Activate()
    {
        if (_floor is null) return;

        _live = true;
        Visible = true;
        SetProcess(true);

        Clear();
        Dress();

        // Staggered rather than immediate: arriving into a wave already on top of you reads
        // as an ambush the floor never announced.
        _waveIn = _floor.Task == FloorTask.Fight ? double.MaxValue : _floor.WaveSeconds;

        Stair?.Shut();
    }

    /// <summary>Parks the floor: everything it put in the world goes with it.</summary>
    public void Deactivate()
    {
        _live = false;
        Visible = false;
        SetProcess(false);

        Clear();
    }

    /// <summary>Called by the tower when the task is finished.</summary>
    public void Open() => Stair?.Open();

    public override void _Process(double delta)
    {
        if (!_live || _floor is null || _tower is null) return;

        Prune();

        if (_waveIn == double.MaxValue) return;

        _waveIn -= delta;

        if (_waveIn > 0) return;

        _waveIn = _floor.WaveSeconds;

        SendWave();
    }

    // ------------------------------------------------------------------ dressing

    /// <summary>
    /// Puts out whatever the floor's verb needs.
    /// </summary>
    /// <remarks>
    /// A boss floor gets a boss and nothing else, which is the requirement rather than a
    /// simplification (FR-7.13): adds would quietly turn the punctuation mark back into a
    /// sentence.
    /// </remarks>
    private void Dress()
    {
        _props = new Node3D { Name = "Props" };
        AddChild(_props);

        if (_floor!.Task == FloorTask.Fight)
        {
            SummonBoss();
            return;
        }

        // A hold floor has nothing to break. Its task is the clock, and a pylon standing in
        // the room would be a second way off the floor that finishes it early.
        if (_floor.Task == FloorTask.Hold) return;

        var marks = Marks();

        if (marks.Count == 0)
        {
            GD.PushError($"[tower] floor '{FloorId}' has no Marks for its {_floor.Task} task.");
            return;
        }

        // Real ones first, then the lookalikes, then shuffled across the marks — otherwise
        // the real one is always on the first mark and the floor is solved by habit.
        var wanted = _floor.Targets + _floor.Decoys;
        var chosen = Shuffle(marks, wanted);

        for (var i = 0; i < chosen.Count; i++)
        {
            Place(chosen[i], real: i < _floor.Targets, chants: _floor.Task == FloorTask.Find && i >= _floor.Targets);
        }
    }

    private void Place(Node3D mark, bool real, bool chants)
    {
        var pylon = new FloorPylon
        {
            Name = $"Pylon{_pylons.Count}",
            Real = real,
            Chants = chants,
            Hitpoints = _floor!.Task == FloorTask.Race ? 520 : 820,
            Label = LabelFor(_floor.Task),
        };

        // Every pylon on a Find floor must look the same at rest, so the real one is told
        // apart by behaviour and nothing else. Anything set here applies to all of them.
        _props!.AddChild(pylon);
        pylon.GlobalPosition = mark.GlobalPosition;
        pylon.Shattered += OnShattered;

        _pylons.Add(pylon);
    }

    private static string LabelFor(FloorTask task) => task switch
    {
        FloorTask.Carry => L10n.T("Keystone"),
        FloorTask.Find => L10n.T("Lantern"),
        FloorTask.Race => L10n.T("Prop"),
        _ => L10n.T("Seal"),
    };

    private void OnShattered(bool real)
    {
        if (!real)
        {
            // Nothing happens, and that is the point: a wrong lantern costs time, not health
            // and not the floor (FR-7.16).
            UI.WorldNotice.Show(GetTree(), L10n.T("Silent. Not this one."));
            return;
        }

        _tower?.Scored();
    }

    private void SummonBoss()
    {
        if (_enemyScene?.Instantiate() is not Combat.EnemyBrain boss || _floor is null) return;

        boss.EnemyId = _floor.Boss;
        boss.Name = $"Boss_{_floor.Boss}";

        _props!.AddChild(boss);
        // The middle of the room, not near the entry: the whole point of a boss floor is
        // walking into an empty room and seeing the one thing in it.
        boss.PlaceAt(GlobalPosition);

        boss.Self.Died += () => _tower?.Scored();

        _sent.Add(boss);
    }

    // ------------------------------------------------------------------ waves

    /// <summary>
    /// The floor's population. Pressure, never the task itself (FR-7.17).
    /// </summary>
    /// <remarks>
    /// Waves keep coming until the floor is finished, and finishing it is never "kill them
    /// all". A player who wants to leave a hold floor early cannot fight their way out of it,
    /// and a player on a race floor is right to ignore them entirely.
    /// </remarks>
    private void SendWave()
    {
        if (_floor is null || _floor.Waves.Count == 0 || _enemyScene is null) return;

        var headroom = GameWorld.Headroom(GetTree());
        var count = Mathf.Min(_floor.WaveSize, headroom);
        var spots = Spawns();

        for (var i = 0; i < count; i++)
        {
            if (_enemyScene.Instantiate() is not Combat.EnemyBrain enemy) continue;

            enemy.EnemyId = _floor.Waves[(int)(GD.Randi() % _floor.Waves.Count)];
            enemy.Name = $"{FloorId}_{enemy.EnemyId}_{Time.GetTicksMsec()}_{i}";

            _props!.AddChild(enemy);
            enemy.PlaceAt(spots.Count == 0
                ? Entry.GlobalPosition
                : spots[(int)(GD.Randi() % spots.Count)].GlobalPosition);

            _sent.Add(enemy);
        }
    }

    private void Prune()
    {
        for (var i = _sent.Count - 1; i >= 0; i--)
        {
            if (!IsInstanceValid(_sent[i]) || _sent[i].IsDead) _sent.RemoveAt(i);
        }
    }

    // ------------------------------------------------------------------ marks

    /// <summary>Where a pylon may stand.</summary>
    private List<Node3D> Marks() => Under("Marks");

    /// <summary>
    /// Where a wave walks in.
    /// </summary>
    /// <remarks>
    /// Kept apart from the pylon marks so a wave never lands on top of the thing it is meant
    /// to be defending — and so a floor can put its spawns at the walls while its targets sit
    /// in the open, which is the difference between pressure and an ambush.
    /// </remarks>
    private List<Node3D> Spawns()
    {
        var spawns = Under("Spawns");

        return spawns.Count > 0 ? spawns : Marks();
    }

    private List<Node3D> Under(string path)
    {
        var found = new List<Node3D>();

        if (GetNodeOrNull<Node3D>(path) is not { } parent) return found;

        foreach (var child in parent.GetChildren())
        {
            if (child is Node3D node) found.Add(node);
        }

        return found;
    }

    /// <summary>A shuffled subset, so nothing is ever in the same place twice.</summary>
    private static List<Node3D> Shuffle(List<Node3D> from, int take)
    {
        var pool = new List<Node3D>(from);
        var chosen = new List<Node3D>();

        while (chosen.Count < take && pool.Count > 0)
        {
            var index = (int)(GD.Randi() % (uint)pool.Count);

            chosen.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return chosen;
    }

    /// <summary>Takes everything this floor put in the world back out of it.</summary>
    private void Clear()
    {
        _pylons.Clear();
        _sent.Clear();

        if (_props is null) return;

        _props.QueueFree();
        _props = null;
    }
}
