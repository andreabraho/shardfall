using Godot;
using Kiln.Game.Foundation;

namespace Kiln.Game.World;

/// <summary>
/// Changes the cursor to say what a click would do (MOV-04): move, attack, interact, or
/// nothing. In a click-to-move game the cursor is the primary affordance — it is how the
/// player knows an enemy is clickable before committing.
/// </summary>
/// <remarks>
/// Uses Godot's built-in cursor shapes, so this works with no art (decision Q2). Swapping in
/// custom cursors later is a call to <c>Input.SetCustomMouseCursor</c> and nothing else.
/// </remarks>
public partial class CursorManager : Node
{
    /// <summary>20 Hz. A raycast per frame is wasted work for something the eye cannot track.</summary>
    private const double PollInterval = 0.05;

    private double _timer;
    private Camera3D? _camera;
    private Godot.Input.CursorShape _current = Godot.Input.CursorShape.Arrow;

    public override void _Process(double delta)
    {
        _timer -= delta;
        if (_timer > 0) return;
        _timer = PollInterval;

        _camera = GetViewport().GetCamera3D();
        if (_camera is null) return;

        Apply(ShapeUnderCursor());
    }

    private Godot.Input.CursorShape ShapeUnderCursor()
    {
        var mouse = GetViewport().GetMousePosition();
        var from = _camera!.ProjectRayOrigin(mouse);
        var to = from + (_camera.ProjectRayNormal(mouse) * 1000f);

        var query = PhysicsRayQueryParameters3D.Create(from, to, Layers.ClickTargets);
        query.CollideWithAreas = false;

        var hit = GetViewport().World3D.DirectSpaceState.IntersectRay(query);

        // Pointing at the sky: nothing would happen.
        if (hit.Count == 0) return Godot.Input.CursorShape.Forbidden;

        if (hit["collider"].AsGodotObject() is not CollisionObject3D body)
        {
            return Godot.Input.CursorShape.Arrow;
        }

        var layer = body.CollisionLayer;

        if ((layer & Layers.Enemy) != 0) return Godot.Input.CursorShape.Cross;
        if ((layer & Layers.Interactable) != 0) return Godot.Input.CursorShape.PointingHand;

        return Godot.Input.CursorShape.Arrow;
    }

    private void Apply(Godot.Input.CursorShape shape)
    {
        if (shape == _current) return;

        _current = shape;
        Godot.Input.SetDefaultCursorShape(shape);
    }
}
