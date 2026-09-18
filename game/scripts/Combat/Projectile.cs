using Godot;
using Kiln.Game.Foundation;

namespace Kiln.Game.Combat;

/// <summary>
/// A travelling shot. Deliberately slow enough to see and to step out of — the Archer's
/// role is to pressure the player into closing distance, not to land unavoidable damage
/// from off screen.
/// </summary>
public partial class Projectile : Node3D
{
    private Vector3 _velocity;
    private double _life;
    private Combatant? _source;
    private double _coefficient = 1.0;

    [Export] public float Speed { get; set; } = 14f;

    /// <summary>Hit radius. Generous, because the shot is dodged by leaving the line, not by pixels.</summary>
    [Export] public float HitRadius { get; set; } = 0.7f;

    [Export] public double MaxLifetime { get; set; } = 3.0;

    [Export] public uint TargetLayers { get; set; } = Layers.Player;

    public static Projectile Spawn(
        Node parent,
        Combatant source,
        Vector3 from,
        Vector3 toward,
        double coefficient,
        uint targetLayers)
    {
        var projectile = new Projectile
        {
            Name = "Projectile",
            TargetLayers = targetLayers,
        };

        parent.AddChild(projectile);
        projectile.Launch(source, from, toward, coefficient);
        return projectile;
    }

    private void Launch(Combatant source, Vector3 from, Vector3 toward, double coefficient)
    {
        _source = source;
        _coefficient = coefficient;

        GlobalPosition = from;

        var direction = (toward - from) with { Y = 0 };
        _velocity = direction.LengthSquared() > 0.0001f
            ? direction.Normalized() * Speed
            : Vector3.Forward * Speed;

        var mesh = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.18f, Height = 0.36f, RadialSegments = 10, Rings = 5 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.82f, 0.4f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                EmissionEnabled = true,
                Emission = new Color(1f, 0.7f, 0.3f),
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };

        AddChild(mesh);
    }

    public override void _PhysicsProcess(double delta)
    {
        _life += delta;

        if (_life > MaxLifetime || _source is null || !IsInstanceValid(_source))
        {
            QueueFree();
            return;
        }

        GlobalPosition += _velocity * (float)delta;

        foreach (var hit in AreaQuery.Sphere(this, GlobalPosition, HitRadius, TargetLayers))
        {
            hit.TakeAttack(_source, skillCoef: _coefficient);
            QueueFree();
            return;
        }

        // Stopped by geometry, so pillars and walls are real cover.
        var query = PhysicsRayQueryParameters3D.Create(
            GlobalPosition - (_velocity.Normalized() * 0.3f),
            GlobalPosition,
            Layers.World);

        if (GetWorld3D().DirectSpaceState.IntersectRay(query).Count > 0)
        {
            QueueFree();
        }
    }
}
