using System.Collections.Generic;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.World;

namespace Kiln.Game.World;

/// <summary>
/// One camp in the world (WLD-03): a point in the scene, with its tuning taken from data.
/// </summary>
/// <remarks>
/// The node owns the placement and the engine work; <see cref="SpawnField"/> owns every
/// decision about how many and when, which is why that part is unit-tested and this part is
/// not. The split also means a designer retunes a camp by editing JSON and dragging a gizmo,
/// never by touching code.
/// <para>
/// Each field has its own RNG stream, forked from the session seed by field id, so the camp
/// on the ridge produces the same creatures in the same order every run and a report of "the
/// wrong thing spawned here" is reproducible.
/// </para>
/// </remarks>
[Tool]
public partial class SpawnFieldNode : Node3D
{
    private readonly List<Combat.EnemyBrain> _mine = [];

    private SpawnField? _field;
    private PackedScene? _enemyScene;
    private Node3D? _container;
    private Node3D? _player;
    private string _fieldId = "";

    [Export]
    public string FieldId
    {
        get => _fieldId;
        set { _fieldId = value; if (Engine.IsEditorHint() && IsNodeReady()) DrawFootprint(); }
    }

    /// <summary>The field's tuning, or null when it failed to resolve. Read by the scene audit.</summary>
    public Kiln.Core.World.SpawnFieldDef? Def => _field?.Def;

    public override void _Ready()
    {
        if (Engine.IsEditorHint())
        {
            DrawFootprint();
            return;
        }

        if (!GameWorld.IsLoaded)
        {
            GD.PushError($"[spawn] '{FieldId}' loaded before GameWorld.Load() ran.");
            return;
        }

        var def = GameWorld.Catalogue.Field(FieldId);

        if (def is null)
        {
            GD.PushError($"[spawn] no spawn field '{FieldId}' in content — this node will do nothing.");
            return;
        }

        // Whether this camp belongs in this scene is checked by ZoneRoot.Audit, not here: at
        // this point the current zone is still the previous scene's, because a node is ready
        // before its parent is.
        AddToGroup("spawn_fields");

        _field = new SpawnField(def, new DeterministicRng(GameSession.Seed).Fork($"spawn:{FieldId}"));
        _enemyScene = GD.Load<PackedScene>("res://scenes/enemy.tscn");
    }

    public override void _Process(double delta)
    {
        if (_field is null || Debug.DebugSpawner.Frozen) return;

        Prune();

        _container ??= GetTree().CurrentScene?.GetNodeOrNull<Node3D>("Enemies") ?? GetTree().CurrentScene as Node3D;
        _player ??= GetTree().GetFirstNodeInGroup("player") as Node3D;

        var distance = _player is null
            ? float.MaxValue
            : _player.GlobalPosition.DistanceTo(GlobalPosition);

        var state = new SpawnFieldState(distance, _mine.Count, GameWorld.Headroom(GetTree()));
        var spawns = _field.Tick(delta, state);

        if (_field.ShouldClear)
        {
            Clear();
            return;
        }

        foreach (var enemyId in spawns) Spawn(enemyId);

        if (spawns.Count > 0 && !_reported)
        {
            _reported = true;
            GD.Print($"[spawn] {FieldId} populated — {spawns.Count} of {_field.Def.Count}");
        }
    }

    private bool _reported;

    /// <summary>Drops the dead and anything the world removed from under us.</summary>
    private void Prune()
    {
        for (var i = _mine.Count - 1; i >= 0; i--)
        {
            var brain = _mine[i];

            if (!IsInstanceValid(brain) || brain.IsDead) _mine.RemoveAt(i);
        }
    }

    /// <summary>
    /// Takes the field's creatures back out of the world. Anything currently fighting is left
    /// alone — the player has walked far enough for this to be rare, and despawning something
    /// mid-swing is the one way this system can be noticed.
    /// </summary>
    private void Clear()
    {
        for (var i = _mine.Count - 1; i >= 0; i--)
        {
            var brain = _mine[i];

            if (!IsInstanceValid(brain)) { _mine.RemoveAt(i); continue; }
            if (brain.HasLivingTarget) continue;

            _mine.RemoveAt(i);
            brain.QueueFree();
        }
    }

    private void Spawn(string enemyId)
    {
        if (_enemyScene?.Instantiate() is not Combat.EnemyBrain enemy) return;

        // Set before the node enters the tree: EnemyBrain reads its definition in _Ready.
        enemy.EnemyId = enemyId;
        enemy.Name = $"{FieldId}_{enemyId}_{Time.GetTicksMsec()}_{_mine.Count}";

        (_container ?? this).AddChild(enemy);
        enemy.GlobalPosition = Scatter();

        _mine.Add(enemy);
    }

    /// <summary>
    /// Shows the camp's footprint and the ring inside which it must not meet safe ground.
    /// </summary>
    /// <remarks>
    /// Two rings rather than one: the inner is where creatures stand, the outer is the
    /// distance the scene audit enforces against a village. Placing a camp against a
    /// boundary you cannot see means finding out at the next run, which is the loop this is
    /// meant to close.
    /// </remarks>
    private void DrawFootprint()
    {
        if (!GameContent.IsLoaded && !GameContent.EnsureLoadedForEditor()) return;

        var radius = FootprintRadius();

        EditorRing.Show(this, radius, new Color(0.85f, 0.35f, 0.3f, 0.5f));

        // Freed before the replacement is named, or Godot renames the new one around it.
        if (GetNodeOrNull<Node>("Clearance") is { } stale)
        {
            RemoveChild(stale);
            stale.QueueFree();
        }

        if (radius > 0)
        {
            var clearance = new Node3D { Name = "Clearance" };
            AddChild(clearance);
            EditorRing.Show(clearance, radius + SafetyField.ClearanceMargin,
                new Color(0.85f, 0.35f, 0.3f, 0.22f), thickness: 0.12f);
        }
    }

    private double FootprintRadius()
    {
        foreach (var zone in GameContent.Database.Zones.Values)
        {
            foreach (var field in zone.SpawnFields)
            {
                if (field.Id == _fieldId) return field.Radius;
            }
        }

        return 0;
    }

    /// <summary>
    /// A point inside the field's radius, biased outward so a camp reads as a ring of
    /// creatures rather than a pile on the marker.
    /// </summary>
    private Vector3 Scatter()
    {
        var radius = (float)(_field?.Def.Radius ?? 6.0);
        var angle = GD.RandRange(0, Mathf.Tau);
        var distance = radius * Mathf.Sqrt((float)GD.RandRange(0.25, 1.0));

        return GlobalPosition + new Vector3(
            (float)Mathf.Cos(angle) * distance,
            0,
            (float)Mathf.Sin(angle) * distance);
    }
}
