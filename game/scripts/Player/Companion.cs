using Godot;
using Kiln.Data.Definitions;
using Kiln.Game.Foundation;

namespace Kiln.Game.Player;

/// <summary>
/// A companion (REF-23): a small creature that walks beside the player. Looks only — it
/// neither fights nor is fought, and nothing can click it.
/// </summary>
/// <remarks>
/// A body with no collision layer of its own, so no attack, click or creature finds it, and
/// nothing it touches is pushed; it only collides with the ground and the walls, to stand and
/// be stopped. It heads for a spot behind the player's shoulder, walks when it is a little way
/// off, runs to catch up, and is simply put back beside the player when it falls far behind —
/// round a wall it could not find its way past, or after the player travelled.
/// </remarks>
public partial class Companion : CharacterBody3D
{
    /// <summary>Where it likes to be: behind and to the left of the player.</summary>
    private static readonly Vector3 Spot = new(-1.1f, 0, 1.2f);

    /// <summary>Closer than this to its spot, it stands.</summary>
    private const float Near = 0.5f;

    /// <summary>Further than this from the player, it is put back beside them.</summary>
    private const float Lost = 14f;

    private const float Gravity = 20f;

    private Node3D? _player;
    private Visual.VisualRoot _visual = null!;

    /// <summary>The cosmetic it is.</summary>
    public string CosmeticId { get; private set; } = "";

    public static Companion Make(CosmeticDef look)
    {
        var companion = new Companion { Name = "Companion", CosmeticId = look.Id };

        companion.CollisionLayer = 0;
        companion.CollisionMask = Layers.World;

        companion.AddChild(new CollisionShape3D
        {
            Shape = new SphereShape3D { Radius = 0.2f },
            Position = new Vector3(0, 0.2f, 0),
        });

        companion._visual = new Visual.VisualRoot { Name = "VisualRoot" };
        companion.AddChild(companion._visual);
        companion._look = look;

        return companion;
    }

    private CosmeticDef _look = null!;

    public override void _Ready()
    {
        AddToGroup("companions");
        _visual.Apply(_look.Visual, null, _look.Scale);
        _player = GetTree().GetFirstNodeInGroup("player") as Node3D;

        if (_player is not null) GlobalPosition = Beside(_player);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_player is null || !IsInstanceValid(_player))
        {
            _player = GetTree().GetFirstNodeInGroup("player") as Node3D;
            return;
        }

        var goal = Beside(_player);
        var to = (goal - GlobalPosition) with { Y = 0 };
        var far = to.Length();

        if (GlobalPosition.DistanceTo(_player.GlobalPosition) > Lost)
        {
            GlobalPosition = goal;
            Velocity = Vector3.Zero;
            return;
        }

        var playerSpeed = _player is CharacterBody3D body ? (body.Velocity with { Y = 0 }).Length() : 0f;

        // Walks when a little off, runs to keep up when the player runs or it has fallen behind.
        var speed = far < Near ? 0f : Mathf.Max(far > 3f ? playerSpeed * 1.15f + 1.5f : 2.2f, 2.2f);
        var flat = far < Near ? Vector3.Zero : to.Normalized() * Mathf.Min(speed, far / (float)delta);

        var fall = IsOnFloor() ? 0f : Velocity.Y - (Gravity * (float)delta);
        Velocity = flat with { Y = fall };
        MoveAndSlide();

        // Faces where it goes; standing, where the player looks.
        // The model turns, not the body, as the player's and every creature's does.
        var target = flat.LengthSquared() > 0.01f ? Mathf.Atan2(-flat.X, -flat.Z) : Yaw(_player);

        _visual.Rotation = _visual.Rotation with { Y = Mathf.LerpAngle(_visual.Rotation.Y, target, Mathf.Min(1f, 10f * (float)delta)) };
    }

    /// <summary>Where the player faces: their model turns, their body does not.</summary>
    private static float Yaw(Node3D player) => player.GetNodeOrNull<Node3D>("VisualRoot")?.Rotation.Y ?? 0f;

    /// <summary>Its spot by the player, turned with the way the player faces.</summary>
    private static Vector3 Beside(Node3D player) => player.GlobalPosition + Spot.Rotated(Vector3.Up, Yaw(player));
}
