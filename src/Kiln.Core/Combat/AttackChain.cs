namespace Kiln.Core.Combat;

/// <summary>
/// One swing of the basic-attack chain.
/// </summary>
/// <param name="Step">Position in the chain, from 1 to <see cref="AttackChain.Length"/>.</param>
/// <param name="DamageCoef">Weapon coefficient, relative to a plain swing.</param>
/// <param name="ArcDegrees">Total spread the swing covers, centred on the aim.</param>
/// <param name="Range">Reach in metres.</param>
/// <param name="Cadence">Multiplier on the interval before the next swing is allowed.</param>
/// <param name="Lunge">How far the character throws itself into the swing, in metres.</param>
/// <param name="Sweeps">Whether this swing throws what it hits.</param>
public sealed record ChainSwing(
    int Step,
    double DamageCoef,
    double ArcDegrees,
    double Range,
    double Cadence,
    double Lunge,
    bool Sweeps);

/// <summary>
/// The four-swing basic attack (CBT-17): keep attacking and the swings escalate, ending in
/// one that clears the ground around you.
/// </summary>
/// <remarks>
/// The basic attack is pressed more than everything else in the game combined, so it is the
/// one place where repetition is guaranteed. A single swing repeated forever is the version
/// of that which teaches nothing; a chain turns the same button into a rhythm with a payoff
/// at the end of it, and gives the player a reason to decide between finishing the chain and
/// breaking off to reposition.
/// <para>
/// The escalation is deliberately not a damage race. Across a full chain the swings deal
/// about sixteen per cent more than four plain ones, but they also take about eleven per
/// cent longer, so finishing is worth roughly four per cent in damage. What it is actually
/// worth is the fourth swing: a wide, slow, committed sweep that throws a crowd off you.
/// Chains that simply do more damage per second make stopping a mistake; this one makes
/// stopping a choice.
/// </para>
/// <para>
/// Engine-free on purpose (NFR-M.1). Which swing comes next, and when the chain has lapsed,
/// is arithmetic that should be testable without a scene tree.
/// </para>
/// </remarks>
public sealed class AttackChain
{
    public const int Length = 4;

    /// <summary>
    /// Seconds of grace after the next swing becomes available, before the chain lapses.
    /// </summary>
    /// <remarks>
    /// Measured from when the swing is ready rather than from when the last one landed, so
    /// the window survives attack speed changing under it. A fixed window looks reasonable
    /// at one attack per second and becomes impossible below it: the cooldown outlasts the
    /// window, the chain lapses before the player is allowed to swing again, and the fourth
    /// swing simply cannot be reached. Slow weapons would have quietly had no finisher.
    /// <para>
    /// Generous enough to survive a step sideways between blows. Breaking the chain should
    /// be something the player chose, not something a moment of repositioning did to them.
    /// </para>
    /// </remarks>
    public const double Grace = 0.6;

    private static readonly ChainSwing[] Swings =
    [
        // A jab: quick and narrow. The chain has to open with its cheapest swing, because
        // this is the one thrown at a single enemy that is about to die anyway.
        new(1, DamageCoef: 0.90, ArcDegrees: 70, Range: 2.6, Cadence: 0.85, Lunge: 0.30, Sweeps: false),

        // Wider and squarer, the first swing that catches a second enemy standing close.
        new(2, DamageCoef: 1.00, ArcDegrees: 100, Range: 2.9, Cadence: 0.95, Lunge: 0.35, Sweeps: false),

        // A committed cut: longer reach, more weight, and the tell that the sweep is next.
        new(3, DamageCoef: 1.15, ArcDegrees: 110, Range: 3.2, Cadence: 1.05, Lunge: 0.45, Sweeps: false),

        // The sweep. All the way around, the longest reach, and slow enough to be a
        // decision: a finisher that cost nothing to throw would just be the fourth swing.
        new(4, DamageCoef: 1.60, ArcDegrees: 360, Range: 3.8, Cadence: 1.60, Lunge: 0.15, Sweeps: true),
    ];

    private int _next;
    private double _since;
    private double _lapse;

    /// <summary>The swing that would be thrown now, from 1 to <see cref="Length"/>.</summary>
    public int Step => _next + 1;

    /// <summary>Seconds since the last swing.</summary>
    public double Since => _since;

    /// <summary>True while another swing would continue the chain rather than restart it.</summary>
    public bool Open => _next > 0 && _since <= _lapse;

    /// <summary>Advances the lapse timer, dropping the chain once the window has passed.</summary>
    public void Tick(double delta)
    {
        if (_next == 0) return;

        _since += delta;

        if (_since > _lapse) Reset();
    }

    /// <summary>
    /// Takes the next swing and moves the chain along.
    /// </summary>
    /// <param name="attacksPerSecond">
    /// The character's current attack rate, which together with the swing's cadence sets how
    /// long the chain will wait for the next one.
    /// </param>
    public ChainSwing Swing(double attacksPerSecond)
    {
        var swing = Swings[_next];

        _next = (_next + 1) % Length;
        _since = 0;
        _lapse = (swing.Cadence / System.Math.Max(0.1, attacksPerSecond)) + Grace;

        return swing;
    }

    /// <summary>Drops the chain back to its first swing.</summary>
    public void Reset()
    {
        _next = 0;
        _since = 0;
        _lapse = 0;
    }

    /// <summary>The swing at a position, for anything that needs to look ahead.</summary>
    public static ChainSwing At(int step) => Swings[(step - 1) % Length];
}
