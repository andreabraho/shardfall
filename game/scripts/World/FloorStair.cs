using Godot;

namespace Kiln.Game.World;

/// <summary>
/// The way down off a floor (FR-7.11, FR-7.15). Shut until the floor's task is finished, and
/// unmistakable the moment it is.
/// </summary>
/// <remarks>
/// It opens loudly on purpose. The single worst failure a floor tower has is the player
/// finishing a task and not knowing they finished it — standing in a room that is now over,
/// looking for the thing that changed. So the stair is dark and inert while the floor runs,
/// and when it opens it lights, names itself and says so in the middle of the screen.
/// </remarks>
public partial class FloorStair : Area3D
{
    private static readonly Color Dark = new("3a3540");
    private static readonly Color Lit = new("ffd88a");

    private Visual.VisualRoot? _visual;
    private Combat.NamePlate? _plate;
    private bool _open;
    private double _cooldown;

    /// <summary>How wide the crossing is. Generous: a stair you can miss at a run is a bug.</summary>
    [Export] public float Radius { get; set; } = 3.0f;

    public override void _Ready()
    {
        AddToGroup("floor_stairs");

        CollisionLayer = 0;
        CollisionMask = Foundation.Layers.Player;
        Monitoring = true;

        AddChild(new CollisionShape3D
        {
            Name = "Crossing",
            Shape = new SphereShape3D { Radius = Radius },
            Position = new Vector3(0, 1.0f, 0),
        });

        _visual = new Visual.VisualRoot { Name = "VisualRoot", Position = new Vector3(0, 1.1f, 0) };
        AddChild(_visual);

        _plate = new Combat.NamePlate
        {
            Name = "NamePlate",
            Rank = Combat.NameRank.Boss,
            Offset = new Vector3(0, 3.4f, 0),
        };

        AddChild(_plate);

        BodyEntered += OnBodyEntered;

        Restyle();
    }

    public override void _Process(double delta)
    {
        if (_cooldown > 0) _cooldown -= delta;
    }

    /// <summary>Locks the stair. Called whenever a floor starts, including on a retry.</summary>
    public void Shut()
    {
        _open = false;

        // Arriving on a new floor puts the player next to that floor's own entry, which on a
        // tight room can overlap the stair they have not earned yet.
        _cooldown = 1.0;

        Restyle();
    }

    /// <summary>Opens the stair and makes sure the player cannot miss that it happened.</summary>
    public void Open()
    {
        if (_open) return;

        _open = true;

        Restyle();

        UI.WorldNotice.Show(GetTree(), "The floor is finished. The stair is open.");
    }

    private void Restyle()
    {
        var colour = _open ? Lit : Dark;

        _visual?.Apply("mesh_placeholder_monolith", "#" + colour.ToHtml(false), _open ? 1.1 : 0.7);

        if (_plate is null) return;

        _plate.Tint = colour;
        _plate.SetText(_open ? "Down" : "Sealed");
    }

    private void OnBodyEntered(Node3D body)
    {
        if (!_open || _cooldown > 0 || !body.IsInGroup("player")) return;

        // Found by group rather than by a reference set on each of nine stairs: one tower per
        // scene, and a stair that has lost its wiring would fail silently.
        if (GetTree().GetFirstNodeInGroup("tower") is TowerNode tower) tower.Descend();
    }
}
