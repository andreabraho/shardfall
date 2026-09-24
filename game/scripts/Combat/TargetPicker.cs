using Godot;

namespace Kiln.Game.Combat;

/// <summary>
/// Which creature is under the cursor (REF-07): the one the player means to attack when they
/// click, whatever stands in front of it and however large it is.
/// </summary>
/// <remarks>
/// The click used to be a physics ray, and it stopped at the first thing it met. A crate,
/// a fence or a tree between the camera and the creature took the click, and the player
/// walked to the crate. The ray also only knew each creature by its body — a capsule the
/// size of a person whatever the model — so clicking the Spider Queen anywhere but the middle
/// of her back walked the Warrior underneath her instead of attacking.
/// <para>
/// Every living creature is tested instead as an upright cylinder the size of its model, and
/// scenery is not tested at all: a creature the player can see enough of to click is one
/// they mean to fight. The nearest along the line of sight wins.
/// </para>
/// </remarks>
public static class TargetPicker
{
    /// <summary>A little slack around every body, so a click on its edge still counts.</summary>
    private const float Slack = 0.25f;

    /// <summary>How tall and wide a shard counts as, in metres.</summary>
    private const float ShardRadius = 1.4f;

    private const float ShardHeight = 4.0f;

    /// <summary>The creature under <paramref name="screen"/>, or null, with where the line meets it.</summary>
    public static Combatant? Under(Viewport viewport, Camera3D camera, Vector2 screen, out Vector3 point)
    {
        var from = camera.ProjectRayOrigin(screen);
        var direction = camera.ProjectRayNormal(screen);

        Combatant? best = null;
        var nearest = float.MaxValue;

        foreach (var node in viewport.GetTree().GetNodesInGroup("enemies"))
        {
            if (node is not EnemyBrain brain || brain.IsDead || !brain.Self.IsAlive) continue;

            Consider(brain.GlobalPosition, brain.ClickRadius, brain.ClickHeight, brain.Self);
        }

        foreach (var node in viewport.GetTree().GetNodesInGroup("shards"))
        {
            if (node is not World.ShardNode { IsStanding: true } shard || !shard.Core.IsAlive) continue;

            Consider(shard.GlobalPosition, ShardRadius, ShardHeight, shard.Core);
        }

        point = best is null ? Vector3.Zero : from + (direction * nearest);
        return best;

        void Consider(Vector3 feet, float radius, float height, Combatant who)
        {
            var along = Through(from, direction, feet, radius + Slack, height);

            if (along < 0 || along >= nearest) return;

            nearest = along;
            best = who;
        }
    }

    /// <summary>
    /// How far along the line it passes closest to an upright cylinder standing at
    /// <paramref name="feet"/>, if it passes through it at all; -1 if it misses.
    /// </summary>
    public static float Through(Vector3 from, Vector3 direction, Vector3 feet, float radius, float height)
    {
        // The stretch of the line between the cylinder's floor and its top.
        float enter, leave;

        if (Mathf.Abs(direction.Y) < 1e-5f)
        {
            if (from.Y < feet.Y || from.Y > feet.Y + height) return -1;

            enter = 0;
            leave = 1000;
        }
        else
        {
            var a = (feet.Y - from.Y) / direction.Y;
            var b = (feet.Y + height - from.Y) / direction.Y;

            enter = Mathf.Max(0, Mathf.Min(a, b));
            leave = Mathf.Max(a, b);

            if (leave < enter) return -1;
        }

        // Over that stretch, the point nearest the cylinder's axis, seen from above.
        var start = from + (direction * enter);
        var step = new Vector2(direction.X, direction.Z);
        var toAxis = new Vector2(feet.X - start.X, feet.Z - start.Z);
        var reach = step.LengthSquared() < 1e-10f ? 0f : Mathf.Clamp(toAxis.Dot(step) / step.LengthSquared(), 0f, leave - enter);
        var nearest = new Vector2(start.X, start.Z) + (step * reach);

        return nearest.DistanceTo(new Vector2(feet.X, feet.Z)) <= radius ? enter + reach : -1;
    }
}
