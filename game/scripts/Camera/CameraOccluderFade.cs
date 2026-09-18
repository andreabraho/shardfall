using System.Collections.Generic;
using Godot;
using Kiln.Game.Foundation;

namespace Kiln.Game.Camera;

/// <summary>
/// Fades geometry that stands between the camera and the player, instead of pulling the
/// camera in (MOV-05).
/// <para>
/// A SpringArm that collides with the world yanks the camera closer whenever a wall or
/// pillar is behind the character. In a semi-isometric click-to-move game that is worse
/// than the occlusion it solves: the screen-to-world mapping the player aims with keeps
/// changing, so clicks land somewhere other than intended. Keeping the distance constant
/// and making the obstruction see-through preserves both visibility and aim.
/// </para>
/// </summary>
public partial class CameraOccluderFade : Node
{
    /// <summary>20 Hz: occluders change slowly relative to the fade itself.</summary>
    private const double PollInterval = 0.05;

    /// <summary>Raycast passes, i.e. how many stacked occluders can fade at once.</summary>
    private const int MaxOccluders = 4;

    private sealed class Faded
    {
        public required StandardMaterial3D Material { get; init; }
        public float Alpha { get; set; } = 1f;
        public bool OccludingNow { get; set; }
    }

    private readonly Dictionary<MeshInstance3D, Faded> _tracked = [];
    private double _timer;
    private Camera3D? _camera;
    private Node3D? _target;

    [Export] public NodePath TargetPath { get; set; } = "";

    /// <summary>Opacity while occluding. Low enough to see through, high enough to read as solid.</summary>
    [Export] public float OccludedAlpha { get; set; } = 0.25f;

    [Export] public float FadeSpeed { get; set; } = 8f;

    /// <summary>Aimed at the chest, not the feet — feet are often hidden by the ground itself.</summary>
    [Export] public float TargetHeight { get; set; } = 1.1f;

    public override void _Ready()
    {
        if (!TargetPath.IsEmpty) _target = GetNodeOrNull<Node3D>(TargetPath);
    }

    public override void _Process(double delta)
    {
        _camera = GetViewport().GetCamera3D();

        _timer -= delta;
        if (_timer <= 0)
        {
            _timer = PollInterval;
            RefreshOccluders();
        }

        ApplyFade(delta);
    }

    private void RefreshOccluders()
    {
        foreach (var state in _tracked.Values)
        {
            state.OccludingNow = false;
        }

        if (_camera is null || _target is null) return;

        var from = _camera.GlobalPosition;
        var to = _target.GlobalPosition + (Vector3.Up * TargetHeight);

        var space = _camera.GetWorld3D().DirectSpaceState;
        var exclude = new Godot.Collections.Array<Rid>();

        // IntersectRay returns only the nearest hit, so walk the ray by excluding each
        // body we already found. Cheap at this poll rate and depth.
        for (var i = 0; i < MaxOccluders; i++)
        {
            var query = PhysicsRayQueryParameters3D.Create(from, to, Layers.World, exclude);
            query.CollideWithAreas = false;

            var hit = space.IntersectRay(query);
            if (hit.Count == 0) break;

            if (hit["collider"].AsGodotObject() is not CollisionObject3D body) break;

            exclude.Add(body.GetRid());
            MarkOccluding(body);
        }
    }

    private void MarkOccluding(Node body)
    {
        foreach (var child in body.GetChildren())
        {
            if (child is not MeshInstance3D mesh) continue;

            if (_tracked.TryGetValue(mesh, out var existing))
            {
                existing.OccludingNow = true;
                continue;
            }

            var source = mesh.GetActiveMaterial(0);
            if (source is null) continue;

            // Per-instance copy: the greybox blocks share one material resource, so
            // editing it directly would fade every pillar in the level at once.
            if (source.Duplicate() is not StandardMaterial3D material) continue;

            material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            mesh.MaterialOverride = material;

            _tracked[mesh] = new Faded { Material = material, OccludingNow = true };
        }
    }

    private void ApplyFade(double delta)
    {
        if (_tracked.Count == 0) return;

        var step = FadeSpeed * (float)delta;
        List<MeshInstance3D>? done = null;

        foreach (var (mesh, state) in _tracked)
        {
            if (!GodotObject.IsInstanceValid(mesh))
            {
                (done ??= []).Add(mesh);
                continue;
            }

            var target = state.OccludingNow ? OccludedAlpha : 1f;
            state.Alpha = Mathf.MoveToward(state.Alpha, target, step);

            state.Material.AlbedoColor = state.Material.AlbedoColor with { A = state.Alpha };

            // Fully opaque again: drop the override so it renders on the normal
            // opaque path rather than staying in the transparent queue forever.
            if (!state.OccludingNow && Mathf.IsEqualApprox(state.Alpha, 1f))
            {
                mesh.MaterialOverride = null;
                (done ??= []).Add(mesh);
            }
        }

        if (done is null) return;

        foreach (var mesh in done)
        {
            _tracked.Remove(mesh);
        }
    }
}
