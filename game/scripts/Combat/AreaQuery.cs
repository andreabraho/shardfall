using System.Collections.Generic;
using Godot;
using Kiln.Game.Foundation;

namespace Kiln.Game.Combat;

/// <summary>
/// Finds combatants inside a shape. Area attacks are a core mechanic (doc 01 §1) — hitting
/// a pack rather than one target at a time is what makes shard encounters and pulls work.
/// </summary>
public static class AreaQuery
{
    /// <summary>
    /// Enemies whose body centre lies within <paramref name="radius"/> of a point.
    /// </summary>
    public static List<Combatant> Sphere(Node3D context, Vector3 center, float radius, uint layers = Layers.Enemy)
    {
        var results = new List<Combatant>();

        var shape = new SphereShape3D { Radius = radius };
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = shape,
            Transform = new Transform3D(Basis.Identity, center),
            CollisionMask = layers,
            CollideWithAreas = false,
            CollideWithBodies = true,
            // A pull can easily overlap more than the default 32 results.
        };

        var hits = context.GetWorld3D().DirectSpaceState.IntersectShape(query, maxResults: 64);

        foreach (var hit in hits)
        {
            if (hit["collider"].AsGodotObject() is not Node3D body) continue;

            var combatant = body.GetNodeOrNull<Combatant>("Combatant");
            if (combatant is { IsAlive: true } && !results.Contains(combatant))
            {
                results.Add(combatant);
            }
        }

        return results;
    }

    /// <summary>
    /// Enemies inside a horizontal cone: within <paramref name="radius"/> of the origin and
    /// within <paramref name="angleDegrees"/> total spread around <paramref name="forward"/>.
    /// </summary>
    /// <remarks>
    /// Height is ignored on purpose. The game is played on near-flat ground from a fixed
    /// camera, and a true 3D cone would make hits fail for reasons the player cannot see.
    /// </remarks>
    public static List<Combatant> Cone(
        Node3D context,
        Vector3 origin,
        Vector3 forward,
        float radius,
        float angleDegrees,
        uint layers = Layers.Enemy)
    {
        var results = Sphere(context, origin, radius, layers);
        if (results.Count == 0) return results;

        var flatForward = (forward with { Y = 0 });
        if (flatForward.LengthSquared() < 0.0001f) return results;

        flatForward = flatForward.Normalized();
        var threshold = Mathf.Cos(Mathf.DegToRad(angleDegrees * 0.5f));

        for (var i = results.Count - 1; i >= 0; i--)
        {
            var toTarget = (results[i].Body.GlobalPosition - origin) with { Y = 0 };

            // A target directly on top of the origin counts as inside the cone rather than
            // being dropped by a degenerate normalise.
            if (toTarget.LengthSquared() < 0.01f) continue;

            if (toTarget.Normalized().Dot(flatForward) < threshold)
            {
                results.RemoveAt(i);
            }
        }

        return results;
    }
}
