using Godot;
using Kiln.Core.Combat;
using Kiln.Core.Foundation;

namespace Kiln.Game.Player;

/// <summary>
/// Sets the player up and handles death and respawn (FR-3.9).
/// <para>
/// Stats are hard-coded for the Phase 2 slice. Levelling and gear replace this in Phases 3
/// and 4 — the numbers here exist so combat can be felt, not to be balanced.
/// </para>
/// </summary>
public partial class PlayerCharacter : Node
{
    private PlayerMotor _motor = null!;
    private Combat.Combatant _combatant = null!;
    private Vector3 _spawnPoint;

    [Export] public int StartingLevel { get; set; } = 10;

    /// <summary>Respawn delay. Kept short: a long death screen makes a hard game feel unfair.</summary>
    [Export] public double RespawnSeconds { get; set; } = 1.5;

    public override void _Ready()
    {
        _motor = GetParent<PlayerMotor>();
        _combatant = GetParent().GetNode<Combat.Combatant>("Combatant");

        _spawnPoint = _motor.GlobalPosition;
        _motor.AddToGroup("player");

        _combatant.IsPlayer = true;
        _combatant.Configure(
            new StatBlock
            {
                Level = StartingLevel,
                Class = CharacterClass.Warrior,
                Family = MonsterFamily.Human,
                Attributes = new Attributes(Str: 22, Dex: 14, Int: 8, Vit: 18),
                WeaponDamage = 28,
                ArmorValue = 34,
            },
            "$player.name");

        _combatant.Damaged += OnDamaged;
        _combatant.Died += OnDied;

        Debug.DebugOverlay.Register("hp", () => $"{_combatant.Health} ({_combatant.Health.Fraction:P0})");
    }

    private void OnDamaged(int amount, bool critical, bool evaded)
    {
        var at = _motor.GlobalPosition + (Vector3.Up * 2.0f);
        Combat.CombatFeedback.Number(at, amount, critical, evaded, onPlayer: true);

        if (evaded) return;

        _motor.GetNodeOrNull<Visual.VisualRoot>("VisualRoot")?.Flash();

        // Weight the feedback by how much the hit actually mattered. A scratch gets a flash
        // and nothing more; only a real blow freezes the frame or moves the camera.
        var severity = (float)(amount / System.Math.Max(1.0, _combatant.Stats.MaxHp));

        if (severity >= 0.05f || critical)
        {
            Combat.CombatFeedback.HitStop(critical ? 0.06 : 0.04);
        }

        // Pushed away from whatever hit us, so the kick carries information rather than
        // just being motion. Only the player being hit moves the camera — reacting to every
        // blow the player lands would be constant noise.
        Camera.CameraRig.Kick(LastHitDirection(), critical ? severity * 1.4f : severity);
    }

    /// <summary>
    /// Direction away from the nearest living enemy, as a stand-in for the true hit
    /// direction. Damage does not currently carry its source; when it does (needed anyway
    /// for threat and for directional blocking) this reads it from there instead.
    /// </summary>
    private Vector3 LastHitDirection()
    {
        Node3D? nearest = null;
        var best = float.MaxValue;

        foreach (var node in GetTree().GetNodesInGroup("combatants"))
        {
            if (node is not Combat.Combatant other || other == _combatant || !other.IsAlive) continue;

            var distance = _motor.GlobalPosition.DistanceSquaredTo(other.Body.GlobalPosition);
            if (distance >= best) continue;

            best = distance;
            nearest = other.Body;
        }

        return nearest is null
            ? Vector3.Zero
            : _motor.GlobalPosition - nearest.GlobalPosition;
    }

    private async void OnDied()
    {
        GD.Print("[combat] player died — respawning at spawn point");
        _motor.Stop();

        await ToSignal(GetTree().CreateTimer(RespawnSeconds), SceneTreeTimer.SignalName.Timeout);

        if (!IsInstanceValid(this)) return;

        _motor.GlobalPosition = _spawnPoint;
        _motor.MovementLocked = false;
        _motor.Stop();
        _combatant.Revive();

        // Respawning with an empty flask would just feed the next death.
        GetParent().GetNodeOrNull<HealthFlask>("HealthFlask")?.Refill();
    }
}
