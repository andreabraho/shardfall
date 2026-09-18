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

        if (!evaded) Combat.CombatFeedback.HitStop(0.05);
    }

    private async void OnDied()
    {
        GD.Print("[combat] player died — respawning at spawn point");
        _motor.Stop();

        await ToSignal(GetTree().CreateTimer(RespawnSeconds), SceneTreeTimer.SignalName.Timeout);

        if (!IsInstanceValid(this)) return;

        _motor.GlobalPosition = _spawnPoint;
        _motor.Stop();
        _combatant.Revive();
    }
}
