using Kiln.Core.Combat;

namespace Kiln.Core.Encounters;

/// <summary>Something the encounter wants the world to do.</summary>
public enum ShardEventKind
{
    /// <summary>Seal the arena and begin. Carries no payload.</summary>
    Engaged,

    /// <summary>Spawn the wave for <see cref="ShardEvent.Phase"/>.</summary>
    SpawnWave,

    /// <summary>Begin the pulse wind-up. The decal appears now and lands after <see cref="ShardEvent.Duration"/>.</summary>
    PulseTelegraph,

    /// <summary>The pulse lands. Damage everything inside the radius.</summary>
    PulseStrike,

    /// <summary>The shard begins reclaiming itself. Show the cast bar.</summary>
    ReclamationStarted,

    /// <summary>The cast completed: the shard heals and a wave returns.</summary>
    ReclamationCompleted,

    /// <summary>The anchor died in time.</summary>
    ReclamationInterrupted,

    Broken,
}

public readonly record struct ShardEvent(ShardEventKind Kind, ShardPhase Phase = ShardPhase.Dormant, double Duration = 0);

/// <summary>What the world tells the encounter each tick.</summary>
/// <param name="AnchorAlive">Whether the add designated as the anchor is still standing.</param>
public readonly record struct ShardWorldState(int AddsAlive, bool AnchorAlive, bool PlayerInZone);

/// <summary>
/// The three-phase shard fight (SHD-02, doc 02 §3), as a pure state machine.
/// </summary>
/// <remarks>
/// Engine-free on purpose. This is the most intricate piece of pacing in the game — phase
/// thresholds, a pulse rhythm, a cast that can be interrupted by killing the right add — and
/// the only way to be confident in it is to run it thousands of times in a test rather than
/// by playing the same fight over and over.
/// <para>
/// It owns no health. The world owns the shard's health and reports it in; the encounter only
/// decides what should happen at a given fraction. That keeps damage, resistances and the
/// Warded modifier where they already live instead of duplicating a damage pipeline here.
/// </para>
/// </remarks>
public sealed class ShardEncounter
{
    private readonly List<ShardEvent> _events = [];
    private readonly HashSet<ShardPhase> _wavesSpawned = [];

    private double _untilPulse;
    private double _telegraphRemaining;
    private bool _telegraphing;
    private double _reclamationRemaining;

    public ShardEncounter(ShardTier tier, ShardModifier modifier, DifficultySettings difficulty)
    {
        Tier = tier;
        Modifier = modifier;
        Effects = ShardModifierEffects.For(modifier);
        Difficulty = difficulty;
        _untilPulse = tier.PulseInterval;
    }

    public ShardTier Tier { get; }

    public ShardModifier Modifier { get; }

    public ShardModifierEffects Effects { get; }

    public DifficultySettings Difficulty { get; }

    public ShardPhase Phase { get; private set; } = ShardPhase.Dormant;

    public bool IsActive => Phase is ShardPhase.One or ShardPhase.Two or ShardPhase.Three;

    public bool IsReclaiming => _reclamationRemaining > 0;

    /// <summary>Seconds left on the reclamation cast, for the bar the player is racing.</summary>
    public double ReclamationRemaining => Math.Max(0, _reclamationRemaining);

    public double ReclamationProgress =>
        Tier.ReclamationSeconds <= 0 ? 0 : 1.0 - (ReclamationRemaining / Tier.ReclamationSeconds);

    /// <summary>The pulse wind-up at the active difficulty. Validated against BAL-03.</summary>
    public double PulseWindup => Tier.PulseWindup * Difficulty.TelegraphScale;

    /// <summary>Rings the pulse draws. A second one from phase two onward (doc 02 §3).</summary>
    public int PulseRings => Phase >= ShardPhase.Two ? 2 : 1;

    /// <summary>
    /// Damage reduction the shard currently has. Warded makes clearing adds the prerequisite
    /// for hurting the shard at all, which is what turns it into a kill-order puzzle.
    /// </summary>
    public double DamageReduction(int addsAlive) => addsAlive > 0 ? Effects.WardedReduction : 0;

    /// <summary>
    /// Advances the fight. Returns what the world should act on — nothing is mutated outside
    /// this object, so a caller can ignore an event without corrupting the state machine.
    /// </summary>
    public IReadOnlyList<ShardEvent> Tick(double delta, double healthFraction, ShardWorldState world)
    {
        _events.Clear();

        if (Phase == ShardPhase.Broken) return _events;

        if (Phase == ShardPhase.Dormant)
        {
            if (!world.PlayerInZone) return _events;

            Phase = ShardPhase.One;
            _events.Add(new ShardEvent(ShardEventKind.Engaged));
            SpawnWaveFor(ShardPhase.One);

            return _events;
        }

        AdvancePhase(healthFraction);

        if (Phase == ShardPhase.Broken) return _events;

        TickPulse(delta);
        TickReclamation(delta, world);

        return _events;
    }

    private void AdvancePhase(double healthFraction)
    {
        var next = ShardTier.PhaseFor(healthFraction);

        // Phases only ever move forward. Healing from a completed reclamation can lift the
        // shard back above a threshold, and re-running phase two's entrance there would spawn
        // its wave a second time — the fight would get harder every time the player let a
        // cast land, on top of the heal they already paid for.
        if (next <= Phase) return;

        Phase = next;

        if (Phase == ShardPhase.Broken)
        {
            _events.Add(new ShardEvent(ShardEventKind.Broken));
            _telegraphing = false;
            _reclamationRemaining = 0;

            return;
        }

        SpawnWaveFor(Phase);

        if (Phase == ShardPhase.Three) BeginReclamation();
    }

    private void SpawnWaveFor(ShardPhase phase)
    {
        if (Tier.WaveFor(phase) is null || !_wavesSpawned.Add(phase)) return;

        _events.Add(new ShardEvent(ShardEventKind.SpawnWave, phase));
    }

    private void TickPulse(double delta)
    {
        if (_telegraphing)
        {
            _telegraphRemaining -= delta;

            if (_telegraphRemaining > 0) return;

            _telegraphing = false;
            _untilPulse = Tier.PulseInterval;
            _events.Add(new ShardEvent(ShardEventKind.PulseStrike, Phase, Tier.PulseRadius));

            return;
        }

        _untilPulse -= delta;

        if (_untilPulse > 0) return;

        _telegraphing = true;
        _telegraphRemaining = PulseWindup;
        _events.Add(new ShardEvent(ShardEventKind.PulseTelegraph, Phase, PulseWindup));
    }

    private void BeginReclamation()
    {
        _reclamationRemaining = Tier.ReclamationSeconds;
        _events.Add(new ShardEvent(ShardEventKind.ReclamationStarted, ShardPhase.Three, Tier.ReclamationSeconds));
    }

    private void TickReclamation(double delta, ShardWorldState world)
    {
        if (_reclamationRemaining <= 0) return;

        // Killing the anchor is the interrupt. This is the beat that replaces racing other
        // players for the stone: the pressure is a deadline you can act on, not a competitor.
        if (!world.AnchorAlive)
        {
            _reclamationRemaining = 0;
            _events.Add(new ShardEvent(ShardEventKind.ReclamationInterrupted, ShardPhase.Three));

            return;
        }

        _reclamationRemaining -= delta;

        if (_reclamationRemaining > 0) return;

        _reclamationRemaining = 0;
        _events.Add(new ShardEvent(
            ShardEventKind.ReclamationCompleted, ShardPhase.Three, Tier.ReclamationHealFraction));

        // It tries again. Letting one cast land must not end the mechanic, or the optimal
        // play would be to eat the heal once and then ignore the anchor entirely.
        _wavesSpawned.Remove(ShardPhase.Three);
        SpawnWaveFor(ShardPhase.Three);
        BeginReclamation();
    }

    /// <summary>The player left the zone: everything resets so a shard cannot be whittled down over many visits.</summary>
    public void Reset()
    {
        Phase = ShardPhase.Dormant;
        _wavesSpawned.Clear();
        _telegraphing = false;
        _telegraphRemaining = 0;
        _reclamationRemaining = 0;
        _untilPulse = Tier.PulseInterval;
        _events.Clear();
    }
}
