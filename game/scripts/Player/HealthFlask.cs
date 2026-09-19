using Godot;
using Kiln.Game.Combat;
using Kiln.Game.Input;

namespace Kiln.Game.Player;

/// <summary>
/// The healing flask (CBT-11, FR-1.5): a small number of charges rather than a stack of
/// potions.
/// <para>
/// This is the single most important change to the original's resource model. While a
/// player can carry hundreds of potions, no encounter can threaten them, and every
/// difficulty lever in the design is void — so the flask is finite, has a cast time, and is
/// interrupted by damage. Charges come from the difficulty tier (doc 02 §1).
/// </para>
/// </summary>
public partial class HealthFlask : Node
{
    private Combatant _self = null!;
    private double _casting;
    private int _charges;

    /// <summary>Fraction of maximum health restored per charge.</summary>
    [Export] public double HealFraction { get; set; } = 0.45;

    /// <summary>Cast time. Long enough that drinking mid-telegraph is a real gamble.</summary>
    [Export] public double CastSeconds { get; set; } = 0.8;

    /// <summary>
    /// Seconds out of combat before charges refill.
    /// <para>
    /// A stand-in for shrines, which arrive in Phase 6 (WLD-02). Until there is somewhere to
    /// refill, an empty flask would simply end the test session.
    /// </para>
    /// </summary>
    [Export] public double RefillAfterSeconds { get; set; } = 8.0;

    private double _outOfCombat;

    public int Charges => _charges;

    public int MaxCharges => GameSession.Difficulty.FlaskCharges;

    public bool IsCasting => _casting > 0;

    [Signal] public delegate void ChargesChangedEventHandler(int charges, int max);

    public override void _Ready()
    {
        _self = GetParent().GetNode<Combatant>("Combatant");
        _charges = MaxCharges;

        // Taking damage interrupts the drink — the cost of using it at the wrong moment.
        _self.Damaged += (_, _, evaded) =>
        {
            ResetRefillTimer();

            if (evaded || !IsCasting) return;

            _casting = 0;
            GD.Print("[flask] interrupted");
        };

        Debug.DebugOverlay.Register("flask", () => $"{_charges}/{MaxCharges}{(IsCasting ? " drinking" : "")}");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // A panel has the player's attention; firing a skill behind it is never intended.
        if (UI.UiState.ModalOpen) return;

        if (!@event.IsActionPressed(GameActions.HealthFlask)) return;

        TryDrink();
        GetViewport().SetInputAsHandled();
    }

    private void TryDrink()
    {
        if (IsCasting || _charges <= 0 || !_self.IsAlive || _self.Statuses.IsStunned) return;
        if (_self.Health.IsFull) return;

        _charges--;
        _casting = CastSeconds;

        EmitSignal(SignalName.ChargesChanged, _charges, MaxCharges);
    }

    public override void _Process(double delta)
    {
        if (_casting > 0)
        {
            _casting -= delta;

            if (_casting <= 0 && _self.IsAlive)
            {
                _self.Heal((int)System.Math.Round(_self.Stats.MaxHp * HealFraction));
            }
        }

        TrackRefill(delta);
    }

    private void TrackRefill(double delta)
    {
        if (_charges >= MaxCharges)
        {
            _outOfCombat = 0;
            return;
        }

        // Any damage taken resets the timer; full health is the proxy for "the fight is over"
        // until shrines exist.
        _outOfCombat += delta;

        if (_outOfCombat < RefillAfterSeconds) return;

        _outOfCombat = 0;
        _charges = MaxCharges;
        EmitSignal(SignalName.ChargesChanged, _charges, MaxCharges);
        GD.Print("[flask] refilled");
    }

    public void ResetRefillTimer() => _outOfCombat = 0;

    /// <summary>Restores all charges. Used on respawn, and by shrines once they exist.</summary>
    public void Refill()
    {
        _charges = MaxCharges;
        _casting = 0;
        EmitSignal(SignalName.ChargesChanged, _charges, MaxCharges);
    }
}
