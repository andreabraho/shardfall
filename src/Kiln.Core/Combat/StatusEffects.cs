namespace Kiln.Core.Combat;

/// <summary>Status effects (FR-3.4).</summary>
public enum StatusKind
{
    /// <summary>Damage over time, stacks.</summary>
    Poison,

    /// <summary>Damage over time, stacks, higher per-tick than poison but shorter.</summary>
    Bleed,

    /// <summary>Cannot act. Never stacks — stun-locking a player is unacceptable.</summary>
    Stun,

    /// <summary>Reduced move speed.</summary>
    Slow,

    /// <summary>Reduced damage dealt.</summary>
    Weaken,

    /// <summary>Increased damage taken. Applied by Striker companions and some skills.</summary>
    Vulnerability,
}

/// <summary>How repeated applications of the same status combine.</summary>
public enum StackRule
{
    /// <summary>Add a stack up to a cap, and refresh the duration.</summary>
    Stack,

    /// <summary>Keep the stronger magnitude and refresh the duration.</summary>
    Strongest,

    /// <summary>Refresh the duration only.</summary>
    Refresh,
}

public sealed class StatusEffect
{
    public required StatusKind Kind { get; init; }

    /// <summary>Meaning depends on the kind: damage per tick, or a fraction for slow/weaken.</summary>
    public double Magnitude { get; set; }

    public double Duration { get; set; }
    public double Remaining { get; set; }
    public int Stacks { get; set; } = 1;
    public int MaxStacks { get; init; } = 1;

    /// <summary>Seconds between damage ticks. Zero for non-damaging statuses.</summary>
    public double TickInterval { get; init; }

    public double TickTimer { get; set; }

    public bool Expired => Remaining <= 0;

    /// <summary>Total per-tick damage across stacks.</summary>
    public double DamagePerTick => Magnitude * Stacks;
}

/// <summary>Damage produced by statuses during a tick, reported back to the caller to apply.</summary>
public readonly record struct StatusTickResult(double Damage, IReadOnlyList<StatusKind> Expired);

/// <summary>
/// The statuses currently on one entity.
/// <para>
/// Stacking rules live here rather than at each call site, because inconsistent stacking is
/// how "why did that kill me" bugs happen — particularly stun, which never stacks so a
/// player can never be chain-locked out of control.
/// </para>
/// </summary>
public sealed class StatusEffectSet
{
    private static readonly Dictionary<StatusKind, StackRule> Rules = new()
    {
        [StatusKind.Poison] = StackRule.Stack,
        [StatusKind.Bleed] = StackRule.Stack,
        [StatusKind.Stun] = StackRule.Refresh,
        [StatusKind.Slow] = StackRule.Strongest,
        [StatusKind.Weaken] = StackRule.Strongest,
        [StatusKind.Vulnerability] = StackRule.Strongest,
    };

    /// <summary>Tolerance for float drift when scheduling damage-over-time ticks.</summary>
    private const double TickEpsilon = 1e-6;

    private readonly Dictionary<StatusKind, StatusEffect> _effects = [];

    public IReadOnlyCollection<StatusEffect> Active => _effects.Values;

    public int Count => _effects.Count;

    public bool Has(StatusKind kind) => _effects.ContainsKey(kind);

    public StatusEffect? Get(StatusKind kind) => _effects.GetValueOrDefault(kind);

    public bool IsStunned => Has(StatusKind.Stun);

    /// <summary>Move speed multiplier from slows. 1.0 when unaffected.</summary>
    public double MoveSpeedMultiplier =>
        Has(StatusKind.Slow) ? Math.Max(0.2, 1 - _effects[StatusKind.Slow].Magnitude) : 1.0;

    /// <summary>Outgoing damage multiplier from weaken.</summary>
    public double DamageDealtMultiplier =>
        Has(StatusKind.Weaken) ? Math.Max(0.1, 1 - _effects[StatusKind.Weaken].Magnitude) : 1.0;

    /// <summary>Incoming damage multiplier from vulnerability.</summary>
    public double DamageTakenMultiplier =>
        Has(StatusKind.Vulnerability) ? 1 + _effects[StatusKind.Vulnerability].Magnitude : 1.0;

    public void Apply(StatusEffect effect)
    {
        effect.Remaining = effect.Duration;

        if (!_effects.TryGetValue(effect.Kind, out var existing))
        {
            _effects[effect.Kind] = effect;
            return;
        }

        switch (Rules.GetValueOrDefault(effect.Kind, StackRule.Refresh))
        {
            case StackRule.Stack:
                existing.Stacks = Math.Min(existing.Stacks + 1, Math.Max(1, existing.MaxStacks));
                existing.Magnitude = Math.Max(existing.Magnitude, effect.Magnitude);
                existing.Remaining = Math.Max(existing.Remaining, effect.Duration);
                break;

            case StackRule.Strongest:
                if (effect.Magnitude >= existing.Magnitude)
                {
                    existing.Magnitude = effect.Magnitude;
                }

                existing.Remaining = Math.Max(existing.Remaining, effect.Duration);
                break;

            case StackRule.Refresh:
            default:
                existing.Remaining = Math.Max(existing.Remaining, effect.Duration);
                break;
        }
    }

    public bool Remove(StatusKind kind) => _effects.Remove(kind);

    /// <summary>Removes one harmful status. Used by the Shaman's cleanse (doc 02 §2.2).</summary>
    public bool CleanseOne()
    {
        foreach (var kind in (StatusKind[])[StatusKind.Stun, StatusKind.Poison, StatusKind.Bleed, StatusKind.Slow, StatusKind.Weaken])
        {
            if (_effects.Remove(kind)) return true;
        }

        return false;
    }

    public void Clear() => _effects.Clear();

    /// <summary>
    /// Advances time. Returns damage accumulated this tick and which statuses expired, so
    /// the caller decides how to apply it — the set never reaches into a health pool itself.
    /// </summary>
    public StatusTickResult Tick(double delta)
    {
        if (_effects.Count == 0) return new StatusTickResult(0, []);

        double damage = 0;
        List<StatusKind>? expired = null;

        foreach (var effect in _effects.Values)
        {
            effect.Remaining -= delta;

            if (effect.TickInterval > 0)
            {
                effect.TickTimer -= delta;

                // Epsilon, not zero: summing frame deltas leaves a residue of ~1e-16, so a
                // timer that should be exactly 0 lands a hair above it and the tick slips to
                // the next frame. Over a full duration that silently loses whole ticks —
                // poison quietly dealing 2/3 of its listed damage.
                while (effect.TickTimer <= TickEpsilon)
                {
                    damage += effect.DamagePerTick;
                    effect.TickTimer += effect.TickInterval;
                }
            }

            if (effect.Expired)
            {
                (expired ??= []).Add(effect.Kind);
            }
        }

        if (expired is not null)
        {
            foreach (var kind in expired)
            {
                _effects.Remove(kind);
            }
        }

        return new StatusTickResult(damage, (IReadOnlyList<StatusKind>?)expired ?? []);
    }

    // -- Factories, so call sites cannot invent inconsistent durations ------------------

    public static StatusEffect Poison(double damagePerTick, double duration = 6, int maxStacks = 5) =>
        new() { Kind = StatusKind.Poison, Magnitude = damagePerTick, Duration = duration, MaxStacks = maxStacks, TickInterval = 1.0, TickTimer = 1.0 };

    public static StatusEffect Bleed(double damagePerTick, double duration = 4, int maxStacks = 3) =>
        new() { Kind = StatusKind.Bleed, Magnitude = damagePerTick, Duration = duration, MaxStacks = maxStacks, TickInterval = 0.5, TickTimer = 0.5 };

    public static StatusEffect Stun(double duration = 1.2) =>
        new() { Kind = StatusKind.Stun, Magnitude = 1, Duration = duration };

    public static StatusEffect Slow(double fraction, double duration = 3) =>
        new() { Kind = StatusKind.Slow, Magnitude = fraction, Duration = duration };

    public static StatusEffect Weaken(double fraction, double duration = 5) =>
        new() { Kind = StatusKind.Weaken, Magnitude = fraction, Duration = duration };

    public static StatusEffect Vulnerability(double fraction, double duration = 4) =>
        new() { Kind = StatusKind.Vulnerability, Magnitude = fraction, Duration = duration };
}
