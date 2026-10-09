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
    private static readonly SphereShape3D _shape = new();

    private static readonly PhysicsShapeQueryParameters3D _query = new()
    {
        Shape = _shape,
        CollideWithAreas = false,
        CollideWithBodies = true,
    };

    private static readonly Variant ColliderKey = "collider";

    private static readonly NodePath CombatantPath = "Combatant";

    /// <summary>
    /// Enemies whose body centre lies within <paramref name="radius"/> of a point.
    /// </summary>
    public static List<Combatant> Sphere(Node3D context, Vector3 center, float radius, uint layers = Layers.Enemy)
    {
        var results = new List<Combatant>();

        // One shape and one set of parameters for every query, rather than a new pair each time
        // (2026-10-09). Every creature asks this several times a second to keep out of its
        // neighbours, and each new shape was a physics-server resource made, used once and left
        // for the collector. Queries run on the main thread, one at a time, so sharing is safe.
        _shape.Radius = radius;
        _query.Transform = new Transform3D(Basis.Identity, center);
        _query.CollisionMask = layers;

        // A pull can easily overlap more than the default 32 results.
        var hits = context.GetWorld3D().DirectSpaceState.IntersectShape(_query, maxResults: 64);

        // The answer is let go of as soon as it is read. Each result is an engine collection with a
        // finalizer, and left to the collector every one of them outlived the cheap collection.
        using var answer = (Godot.Collections.Array)hits;

        foreach (var hit in hits)
        {
            using var result = hit;

            if (result[ColliderKey].AsGodotObject() is not Node3D body) continue;

            var combatant = body.GetNodeOrNull<Combatant>(CombatantPath);
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

    /// <summary>
    /// Enemies in a band <paramref name="width"/> wide running <paramref name="length"/> ahead
    /// of the origin along <paramref name="forward"/> (REF-21, the Blade Wave). Height is
    /// ignored, as for the cone.
    /// </summary>
    public static List<Combatant> Line(
        Node3D context,
        Vector3 origin,
        Vector3 forward,
        float length,
        float width,
        uint layers = Layers.Enemy)
    {
        var flatForward = forward with { Y = 0 };

        if (flatForward.LengthSquared() < 0.0001f) return [];

        flatForward = flatForward.Normalized();

        // Everything near enough to be in it, then only what is ahead and within half the width
        // of the middle line.
        var middle = origin + (flatForward * (length * 0.5f));
        var reach = Mathf.Sqrt((length * length * 0.25f) + (width * width * 0.25f));
        var results = Sphere(context, middle, reach, layers);

        for (var i = results.Count - 1; i >= 0; i--)
        {
            var offset = (results[i].Body.GlobalPosition - origin) with { Y = 0 };
            var along = offset.Dot(flatForward);
            var across = (offset - (flatForward * along)).Length();

            if (along < -0.5f || along > length || across > width * 0.5f) results.RemoveAt(i);
        }

        return results;
    }
}
