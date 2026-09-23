using System.Collections.Generic;
using System.Linq;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.World;
using Kiln.Game.Combat;
using Kiln.Game.Visual;

namespace Kiln.Game.World;

/// <summary>
/// One floor of the tower: the room, and the furniture its task needs (FR-7.11).
/// </summary>
/// <remarks>
/// The scene supplies the room and the markers — where the player lands, where the stair is,
/// where things may stand. The data supplies the task. This node is the join: on activation
/// it reads the floor definition and puts its targets on the marks.
/// <para>
/// The Demon Tower pass (2026-09-23) made every floor a fight rather than a walk: everything a
/// floor sends hunts the player from wherever it lands; a pack is kept alive on top of the
/// waves; a lantern lets out a crowd when it breaks, real or not; a keystone sits behind a
/// barrier until the monsters guarding it are dead; the stones are real stones, fought like
/// the ones in the field; and a boss calls help as it weakens and can enrage.
/// </para>
/// <para>
/// Only the floor the player is on runs. The other eight are parked with their processing
/// off, which is what makes a nine-floor scene cost roughly what a one-floor scene costs.
/// </para>
/// </remarks>
public partial class TowerFloorNode : Node3D
{
    /// <summary>How close the player comes to a sealed keystone before its guard rises.</summary>
    private const float GuardWakeRadius = 13f;

    private readonly List<FloorPylon> _pylons = [];
    private readonly List<ShardNode> _stones = [];
    private readonly List<EnemyBrain> _sent = [];
    private readonly List<EnemyBrain> _pack = [];
    private readonly List<EnemyBrain> _burst = [];
    private readonly Dictionary<FloorPylon, List<EnemyBrain>> _guards = [];
    private readonly HashSet<float> _bossCalls = [];

    private TowerNode? _tower;
    private TowerFloor? _floor;
    private PackedScene? _enemyScene;
    private Node3D? _props;
    private EnemyBrain? _boss;
    private double _waveIn;
    private double _packIn;
    private bool _live;
    private bool _foundReal;
    private bool _enraged;

    /// <summary>Which floor this is. Must be one the zone declares.</summary>
    [Export] public string FloorId { get; set; } = "";

    /// <summary>Where the player lands when they arrive on this floor.</summary>
    public Node3D Entry => GetNode<Node3D>("Entry");

    /// <summary>The way on, shut until the task is finished.</summary>
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

    /// <summary>Opens the floor for business: dresses it, and starts its clock.</summary>
    public void Activate()
    {
        if (_floor is null) return;

        _live = true;
        Visible = true;
        SetProcess(true);

        Clear();
        Dress();

        // The first wave comes a few seconds in rather than on arrival, so landing is not an
        // ambush; the pack is there from the start, because a quiet room is the complaint.
        _waveIn = _floor.Waves.Count == 0 ? double.MaxValue : Mathf.Min(_floor.WaveSeconds, 6.0);
        _packIn = 1.5;

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

        // Nothing more comes once the way on is open: the floor is won, and a player who has
        // just beaten it should be able to walk to the stair.
        var open = _tower.Run?.Phase == FloorPhase.Open;

        if (!open)
        {
            Waves(delta);
            KeepPack(delta);
        }

        WakeGuards();
        CheckLanterns();
    }

    // ------------------------------------------------------------------ dressing

    /// <summary>Puts out whatever the floor's verb needs.</summary>
    private void Dress()
    {
        _props = new Node3D { Name = "Props" };
        AddChild(_props);

        if (_floor!.Task == FloorTask.Fight)
        {
            SummonBoss();
            return;
        }

        // A hold floor has nothing to break. Its task is the clock.
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
            if (_floor.Stone.Length > 0) PlaceStone(chosen[i]);
            else Place(chosen[i], real: i < _floor.Targets);
        }
    }

    private void Place(Node3D mark, bool real)
    {
        var pylon = new FloorPylon
        {
            Name = $"Pylon{_pylons.Count}",
            Real = real,
            Hitpoints = _floor!.Task == FloorTask.Race ? 520 : 820,
            Label = LabelFor(_floor.Task),
            VisualId = VisualFor(_floor.Task),
            Shielded = _floor.Task == FloorTask.Carry && _floor.Guard > 0,
        };

        // Every lantern looks the same, the true one included: which one it is, is luck.
        _props!.AddChild(pylon);
        pylon.GlobalPosition = mark.GlobalPosition;
        pylon.Shattered += r => OnShattered(pylon, r);

        _pylons.Add(pylon);
    }

    /// <summary>A real stone, fought as one in the field: waves as it breaks, hunting adds.</summary>
    private void PlaceStone(Node3D mark)
    {
        var look = GameContent.Database.Shards.TryGetValue(_floor!.Stone, out var def) ? def.Visual : "mesh_placeholder_monolith";
        var height = GameContent.Database.Visuals.TryGetValue(look, out var visual) ? (float)visual.Height : 3.2f;

        var stone = new ShardNode
        {
            Name = $"Stone{_stones.Count}",
            ShardId = _floor.Stone,
            Persistent = false,
            HuntingAdds = true,
            Abandons = false,
        };

        stone.AddChild(new VisualRoot { Name = "VisualRoot", VisualId = look, Position = new Vector3(0, height / 2, 0) });

        _props!.AddChild(stone);
        stone.GlobalPosition = mark.GlobalPosition;
        stone.Broken += () => _tower?.Scored();

        _stones.Add(stone);
    }

    private static string LabelFor(FloorTask task) => task switch
    {
        FloorTask.Carry => L10n.T("Keystone"),
        FloorTask.Find => L10n.T("Lantern"),
        FloorTask.Race => L10n.T("Prop"),
        _ => L10n.T("Seal"),
    };

    private static string VisualFor(FloorTask task) => task switch
    {
        FloorTask.Carry => "mesh_tower_keystone",
        FloorTask.Find => "mesh_tower_lantern",
        _ => "mesh_placeholder_monolith",
    };

    private void OnShattered(FloorPylon pylon, bool real)
    {
        // A lantern lets out what was in it, whichever lantern it was.
        if (_floor!.Task == FloorTask.Find)
        {
            Summon(_floor.Burst, pylon.GlobalPosition, 3.5f, _burst);

            if (!real)
            {
                UI.WorldNotice.Show(GetTree(), L10n.T("Not this one — and it woke them."));
                return;
            }

            _foundReal = true;
            UI.WorldNotice.Show(GetTree(), L10n.T("That was the one. Kill everything the lanterns let out."));
            return;
        }

        if (real) _tower?.Scored();
    }

    /// <summary>
    /// The find floor is done when the true lantern is broken and nothing it or the others let
    /// out is still standing.
    /// </summary>
    private void CheckLanterns()
    {
        if (!_foundReal || _burst.Count > 0) return;

        _foundReal = false;
        _tower?.Scored();
    }

    /// <summary>A sealed keystone's guard rises when the player comes near it, once.</summary>
    private void WakeGuards()
    {
        if (_floor is null || _floor.Guard <= 0) return;
        if (GetTree().GetFirstNodeInGroup("player") is not Node3D player) return;

        foreach (var pylon in _pylons)
        {
            if (!pylon.Shielded || !IsInstanceValid(pylon)) continue;

            if (!_guards.TryGetValue(pylon, out var guard))
            {
                if (pylon.GlobalPosition.DistanceTo(player.GlobalPosition) > GuardWakeRadius) continue;

                guard = [];
                _guards[pylon] = guard;
                Summon(_floor.Guard, pylon.GlobalPosition, 4.5f, guard);
                UI.WorldNotice.Show(GetTree(), L10n.T("The keystone's guard wakes. Kill them to break the seal."));
                continue;
            }

            guard.RemoveAll(e => !IsInstanceValid(e) || e.IsDead);

            if (guard.Count > 0) continue;

            pylon.Lower();
            UI.WorldNotice.Show(GetTree(), L10n.T("The keystone is open."));
        }
    }

    // ------------------------------------------------------------------ the boss

    private void SummonBoss()
    {
        if (_enemyScene?.Instantiate() is not EnemyBrain boss || _floor is null) return;

        boss.EnemyId = _floor.Boss;
        boss.Name = $"Boss_{_floor.Boss}";

        _props!.AddChild(boss);
        // The middle of the room, not near the entry: walking in and seeing it is the point.
        boss.PlaceAt(GlobalPosition);

        // A boss comes for the player however far they out-level it, and is never deleted in one
        // blow: the trivial-level rule (FR-2.7) is for clearing a field, not for skipping the
        // fight a floor exists for.
        boss.Self.IsEncounter = true;
        boss.Hunt();

        boss.Self.Died += () => _tower?.Scored();
        boss.Self.HealthChanged += OnBossHealth;

        _boss = boss;
        _sent.Add(boss);
    }

    /// <summary>At three quarters, half and a quarter the boss calls for help; at half it may enrage.</summary>
    private void OnBossHealth(float fraction)
    {
        if (_floor is null || _boss is null || _boss.IsDead) return;

        foreach (var mark in new[] { 0.75f, 0.5f, 0.25f })
        {
            if (fraction > mark || !_bossCalls.Add(mark)) continue;

            if (_floor.BossAdds > 0)
            {
                Summon(_floor.BossAdds, _boss.GlobalPosition, 6f, _sent);
                UI.WorldNotice.Show(GetTree(), L10n.F("{0} calls the tower to its aid!", _boss.Self.DisplayName));
                Camera.CameraRig.Kick(Vector3.Up, 0.8f);
            }
        }

        if (!_floor.Enrage || _enraged || fraction > 0.5) return;

        _enraged = true;
        _boss.MoveSpeed *= 1.3f;
        _boss.RecoverSeconds *= 0.6;

        _boss.AddChild(new OmniLight3D
        {
            Name = "Rage",
            Position = new Vector3(0, 2.5f, 0),
            LightColor = new Color(1f, 0.25f, 0.1f),
            LightEnergy = 3f,
            OmniRange = 9f,
        });

        AoeVisual.Circle(_boss.GlobalPosition, 6f, hostile: true);
        Camera.CameraRig.Kick(Vector3.Up, 1.4f);
        UI.WorldNotice.Show(GetTree(), L10n.F("{0} is enraged!", _boss.Self.DisplayName));
    }

    // ------------------------------------------------------------------ waves and the pack

    /// <summary>The timed waves: pressure, never the task itself (FR-7.17).</summary>
    private void Waves(double delta)
    {
        if (_waveIn == double.MaxValue || _floor is null) return;

        _waveIn -= delta;

        if (_waveIn > 0) return;

        _waveIn = _floor.WaveSeconds;

        var spots = Spawns();

        for (var i = 0; i < _floor.WaveSize; i++)
        {
            var at = spots.Count == 0 ? Entry.GlobalPosition : spots[(int)(GD.Randi() % spots.Count)].GlobalPosition;

            Summon(1, at, 1.5f, _sent);
        }
    }

    /// <summary>Keeps the pack at strength: whenever one dies, another comes in from the walls.</summary>
    private void KeepPack(double delta)
    {
        if (_floor is null || _floor.Pack <= 0) return;

        _packIn -= delta;

        if (_packIn > 0) return;

        _packIn = 1.2;
        _pack.RemoveAll(e => !IsInstanceValid(e) || e.IsDead);

        if (_pack.Count >= _floor.Pack) return;

        var spots = Spawns();
        var at = spots.Count == 0 ? Entry.GlobalPosition : spots[(int)(GD.Randi() % spots.Count)].GlobalPosition;

        // Two at a time at most, so a pack wiped out comes back as a stream rather than a wall.
        Summon(Mathf.Min(2, _floor.Pack - _pack.Count), at, 1.5f, _pack);
    }

    /// <summary>
    /// Puts <paramref name="count"/> of the floor's creatures in a ring about a point, every
    /// one of them hunting the player.
    /// </summary>
    private void Summon(int count, Vector3 around, float radius, List<EnemyBrain> into)
    {
        if (_floor is null || _floor.Waves.Count == 0 || _enemyScene is null || _props is null) return;

        count = Mathf.Min(count, GameWorld.Headroom(GetTree()));

        for (var i = 0; i < count; i++)
        {
            if (_enemyScene.Instantiate() is not EnemyBrain enemy) continue;

            enemy.EnemyId = _floor.Waves[(int)(GD.Randi() % _floor.Waves.Count)];
            enemy.Name = $"{FloorId}_{enemy.EnemyId}_{Time.GetTicksMsec()}_{i}";

            var angle = Mathf.Tau * i / Mathf.Max(1, count) + (float)GD.RandRange(0, 0.6);

            _props.AddChild(enemy);
            enemy.PlaceAt(around + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius);
            enemy.Hunt();

            into.Add(enemy);
            if (!ReferenceEquals(into, _sent)) _sent.Add(enemy);
        }
    }

    private void Prune()
    {
        _sent.RemoveAll(e => !IsInstanceValid(e) || e.IsDead);
        _burst.RemoveAll(e => !IsInstanceValid(e) || e.IsDead);
    }

    // ------------------------------------------------------------------ marks

    /// <summary>Where a target may stand.</summary>
    private List<Node3D> Marks() => Under("Marks");

    /// <summary>Where a wave walks in. Kept apart from the marks, at the walls.</summary>
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
        _stones.Clear();
        _sent.Clear();
        _pack.Clear();
        _burst.Clear();
        _guards.Clear();
        _bossCalls.Clear();
        _boss = null;
        _foundReal = false;
        _enraged = false;

        if (_props is null) return;

        _props.QueueFree();
        _props = null;
    }
}
