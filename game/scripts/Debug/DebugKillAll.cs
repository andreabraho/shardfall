using Godot;
using Kiln.Game.Combat;
using Kiln.Game.Input;

namespace Kiln.Game.Debug;

/// <summary>
/// Kills every creature in the scene (F2, 2026-10-08, at your call: to climb a tower fast and test
/// what lies further up), and every stone and floor target, sealed or not.
/// </summary>
/// <remarks>
/// Each one dies as it would to a blow, so the kills count: experience, drops, a hold floor's
/// clock, a key floor's key. A boss's ward is lifted for it. A floor target still sealed — a
/// door whose guard sleeps, the statue waiting for its key — is unsealed and broken all the same.
/// <para>Debug builds only, as the other debug keys.</para>
/// </remarks>
public partial class DebugKillAll : Node
{
    public override void _Ready()
    {
        if (!OS.IsDebugBuild())
        {
            QueueFree();
            return;
        }

        GD.Print("[debug] F2 kills every creature, stone and floor target in the scene");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Shift+F2 is the skill points' (DebugSkills).
        if (!@event.IsActionPressed(GameActions.DebugKillAll) || @event is InputEventKey { ShiftPressed: true }) return;

        GetViewport().SetInputAsHandled();

        var count = 0;

        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is EnemyBrain { IsDead: false } enemy && Kill(enemy.Self)) count++;
        }

        foreach (var node in GetTree().GetNodesInGroup("shards"))
        {
            if (node is World.ShardNode shard && Kill(shard.Core)) count++;
        }

        foreach (var node in GetTree().GetNodesInGroup("floor_pylons"))
        {
            if (node is not World.FloorPylon pylon || !pylon.IsVisibleInTree()) continue;

            // A sealed one is unsealed first: its guard may be asleep across the floor.
            pylon.Lower();

            if (pylon.GetNodeOrNull<Combatant>("Combatant") is { } core && Kill(core)) count++;
        }

        var notice = $"Debug: {count} killed.";

        GD.Print($"[debug] {notice}");
        UI.WorldNotice.Show(GetTree(), notice);
    }

    private static bool Kill(Combatant target)
    {
        if (!IsInstanceValid(target) || !target.IsAlive) return false;

        target.Untouchable = false;
        target.CheatDeath = false;
        target.Barrier = 0;
        target.ApplyDamage(int.MaxValue / 2);

        return true;
    }
}
