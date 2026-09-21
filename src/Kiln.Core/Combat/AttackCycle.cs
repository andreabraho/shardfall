namespace Kiln.Core.Combat;

public enum AttackPhase
{
    /// <summary>Free to start the next attack once the cooldown allows.</summary>
    Ready,

    /// <summary>The blow is being drawn back. The character stands still; the hit lands at the end.</summary>
    Windup,

    /// <summary>The follow-through after the hit. Moving cancels its animation, not its timer.</summary>
    Recovery,
}

/// <summary>
/// The rhythm of the basic attack (REF-01): windup, hit, recovery, driven by a timer that comes
/// from attack speed and nothing else.
/// </summary>
/// <remarks>
/// Decided 2026-09-21, after the Metin2 model. The timer is the authority: an attack may start
/// only when the previous one's interval has run out, whatever the animation is doing. Walking
/// away during the recovery cuts the follow-through short — the character looks free sooner — but
/// the next blow still waits for the timer, so cancelling an animation is cosmetic and never a
/// way to attack faster.
/// <para>
/// The hit lands at the end of the windup, a fixed share of the interval, so a faster weapon
/// hits sooner as well as more often. During the windup the character is committed and does
/// not move.
/// </para>
/// </remarks>
public sealed class AttackCycle
{
    /// <summary>Share of the interval spent drawing the blow back before it lands.</summary>
    public const double WindupShare = 0.35;

    public const double MinWindup = 0.15;
    public const double MaxWindup = 0.45;

    private double _timer;

    public AttackPhase Phase { get; private set; } = AttackPhase.Ready;

    /// <summary>Seconds until the next attack may start. Runs regardless of animation.</summary>
    public double Cooldown { get; private set; }

    /// <summary>Seconds left in the current windup or recovery.</summary>
    public double PhaseRemaining => Phase == AttackPhase.Ready ? 0 : _timer;

    public bool CanStart => Phase != AttackPhase.Windup && Cooldown <= 0;

    /// <summary>Seconds between attacks at this rate.</summary>
    public static double IntervalFor(double attacksPerSecond) => 1.0 / Math.Max(0.1, attacksPerSecond);

    public static double WindupFor(double interval) => Math.Clamp(interval * WindupShare, MinWindup, MaxWindup);

    /// <summary>Starts an attack if the timer allows. Returns the windup length, or 0 when it may not start.</summary>
    public double TryStart(double attacksPerSecond)
    {
        if (!CanStart) return 0;

        var interval = IntervalFor(attacksPerSecond);

        Cooldown = interval;
        _timer = WindupFor(interval);
        Phase = AttackPhase.Windup;

        return _timer;
    }

    /// <summary>Advances time. Returns true on the tick the blow lands.</summary>
    public bool Tick(double delta)
    {
        Cooldown = Math.Max(0, Cooldown - delta);

        switch (Phase)
        {
            case AttackPhase.Windup:
                _timer -= delta;

                if (_timer > 0) return false;

                // The follow-through lasts until the next attack may start.
                Phase = AttackPhase.Recovery;
                _timer = Cooldown;

                return true;

            case AttackPhase.Recovery:
                _timer -= delta;

                if (_timer <= 0) Phase = AttackPhase.Ready;

                return false;

            default:
                return false;
        }
    }

    /// <summary>Cuts the follow-through short. Cosmetic: the cooldown is untouched.</summary>
    public void CancelRecovery()
    {
        if (Phase == AttackPhase.Recovery) Phase = AttackPhase.Ready;
    }

    /// <summary>Loses the blow being drawn (a stun, a death). The cooldown still runs.</summary>
    public void Interrupt()
    {
        if (Phase == AttackPhase.Windup) Phase = AttackPhase.Ready;
    }
}
