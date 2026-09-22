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
/// interrupted by damage. How many charges it holds comes from the difficulty tier (doc 02 §1).
/// </para>
/// <para>
/// Refilling costs money (REF-02). Charges do not come back on their own, at a shrine or on
/// death: they are poured in from draughts bought at a merchant, one draught per charge. That
/// is what keeps yang worth earning after the gear you want is bought, and what makes going
/// back to the village a decision rather than a formality — the flask is the one thing in the
/// game you can genuinely run out of.
/// </para>
/// </summary>
public partial class HealthFlask : Node
{
    private Combatant _self = null!;
    private double _casting;
    private int _charges;

    /// <summary>Fraction of maximum health restored per charge.</summary>
    [Export] public double HealFraction { get; set; } = 0.45;

    /// <summary>
    /// Fraction of maximum mana restored per charge, alongside the healing.
    /// <para>
    /// Deliberately not a separate resource competing for the same charges. Making the player
    /// choose between healing and casting sounds like a decision, but health always wins when
    /// it is the thing keeping you alive, so the mana half would simply never be chosen. It
    /// rides along instead, which is what makes the flask the recovery button rather than the
    /// healing button.
    /// </para>
    /// </summary>
    [Export] public double ManaFraction { get; set; } = 0.35;

    /// <summary>Cast time. Long enough that drinking mid-telegraph is a real gamble.</summary>
    [Export] public double CastSeconds { get; set; } = 0.8;

    /// <summary>The material a charge is poured from. Bought at a merchant, one for one.</summary>
    public const string DraughtId = "mat_flask_draught";

    public int Charges => _charges;

    public int MaxCharges => GameSession.Difficulty.FlaskCharges;

    public bool IsCasting => _casting > 0;

    [Signal] public delegate void ChargesChangedEventHandler(int charges, int max);

    public override void _Ready()
    {
        _self = GetParent().GetNode<Combatant>("Combatant");

        // A new character starts with a full flask; a loaded one keeps what it had left, since
        // nothing refills it for free any more.
        _charges = PlayerProfile.FlaskCharges < 0
            ? MaxCharges
            : System.Math.Clamp(PlayerProfile.FlaskCharges, 0, MaxCharges);

        PlayerProfile.FlaskCharges = _charges;

        // Taking damage interrupts the drink — the cost of using it at the wrong moment.
        _self.Damaged += (_, _, evaded) =>
        {
            if (evaded || !IsCasting) return;

            _casting = 0;
            GD.Print("[flask] interrupted");
        };
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
        if (IsCasting || !_self.IsAlive || _self.Statuses.IsStunned) return;

        // Said out loud, because an empty flask is now a thing to go and fix rather than a
        // thing to wait out, and a silent key press reads as a broken button.
        if (_charges <= 0)
        {
            UI.WorldNotice.Show(GetTree(),
                Kiln.Core.Foundation.L10n.T("The flask is empty. Draughts to fill it are sold by merchants."));

            return;
        }

        // Now that it restores both, a charge is worth spending when either pool is short.
        if (_self.Health.IsFull && _self.Mana.IsFull) return;

        _charges--;
        _casting = CastSeconds;
        Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndFlask);

        Changed();
    }

    /// <summary>
    /// Pours one draught in. False when the flask is already full, so the draught is not
    /// quietly wasted.
    /// </summary>
    public bool AddCharge()
    {
        if (_charges >= MaxCharges) return false;

        _charges++;
        Changed();

        return true;
    }

    private void Changed()
    {
        PlayerProfile.FlaskCharges = _charges;

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
                _self.Mana.Add(_self.Stats.MaxMana * ManaFraction);
            }
        }
    }

    /// <summary>
    /// Fills the flask outright. Only for a new character and for the debug console — nothing
    /// in play refills for free.
    /// </summary>
    public void Refill()
    {
        _charges = MaxCharges;
        _casting = 0;
        Changed();
    }
}
