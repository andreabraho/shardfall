using Kiln.Core.Foundation;

namespace Kiln.Core.World;

/// <summary>One kind of creature a field can produce, and how often relative to the others.</summary>
public readonly record struct SpawnEntry(string EnemyId, double Weight = 1.0);

/// <summary>
/// The tuning of one spawn field (WLD-03) — a camp, a patrol stretch, a nest.
/// </summary>
/// <param name="Id">Content id; the scene node names this to inherit its tuning.</param>
/// <param name="Entries">What it produces, by weight.</param>
/// <param name="Count">How many should be standing when the player is here.</param>
/// <param name="RespawnSeconds">How long after a death its replacement walks in.</param>
/// <param name="ActivationRadius">Beyond this the field stops putting creatures in the world.</param>
/// <param name="Radius">How far from the field's centre they scatter.</param>
public sealed record SpawnFieldDef(
    string Id,
    IReadOnlyList<SpawnEntry> Entries,
    int Count,
    double RespawnSeconds,
    double ActivationRadius,
    double Radius);

/// <summary>What the world tells a field each tick.</summary>
/// <param name="PlayerDistance">Metres from the field's centre to the player.</param>
/// <param name="Alive">How many of this field's creatures are currently standing.</param>
/// <param name="GlobalHeadroom">How many more creatures the world will accept right now.</param>
public readonly record struct SpawnFieldState(double PlayerDistance, int Alive, int GlobalHeadroom);

/// <summary>
/// The population of a single spawn field, as a pure state machine (WLD-03).
/// </summary>
/// <remarks>
/// Three decisions are worth stating, because each of them is a bug in the obvious version.
/// <para>
/// <b>Timers run even when the player is far away.</b> The naive version only ticks nearby
/// fields, which means walking back to a camp you cleared five minutes ago finds it still
/// empty. A timer is a float; ticking every field in the game costs nothing. What is expensive
/// is putting creatures in the world, and that is what the activation radius actually gates.
/// </para>
/// <para>
/// <b>Deaths are inferred from the living count, not reported.</b> The field never subscribes
/// to anything. If four creatures die it sees four missing and starts four timers, so the camp
/// comes back as a camp rather than trickling in one at a time behind the player — a stream of
/// single arrivals is the thing that makes farming feel like housework.
/// </para>
/// <para>
/// <b>Deactivation has hysteresis.</b> Standing exactly on the activation radius would
/// otherwise spawn and despawn the camp every frame.
/// </para>
/// </remarks>
public sealed class SpawnField
{
    /// <summary>The field goes quiet only past this multiple of its activation radius.</summary>
    public const double DeactivationHysteresis = 1.25;

    private readonly List<double> _timers = [];
    private readonly DeterministicRng _rng;
    private readonly List<string> _spawns = [];

    private int _ready;

    public SpawnField(SpawnFieldDef def, DeterministicRng rng)
    {
        Def = def;
        _rng = rng;

        // A field the player has never seen is already populated. Arriving at a camp and
        // watching it assemble itself would announce that the world is a machine.
        _ready = def.Count;
    }

    public SpawnFieldDef Def { get; }

    /// <summary>True while the field is close enough to the player to put creatures in the world.</summary>
    public bool Active { get; private set; }

    /// <summary>Creatures owed to the field and waiting only on the world to accept them.</summary>
    public int Ready => _ready;

    /// <summary>Creatures still counting down their respawn.</summary>
    public int Pending => _timers.Count;

    /// <summary>
    /// Set on the tick the field goes quiet. The caller removes its creatures; they will be
    /// owed again immediately, so walking back in finds the camp intact.
    /// </summary>
    public bool ShouldClear { get; private set; }

    /// <summary>Advances the field and returns the enemy ids to place this tick.</summary>
    public IReadOnlyList<string> Tick(double delta, SpawnFieldState state)
    {
        _spawns.Clear();
        ShouldClear = false;

        UpdateActivation(state.PlayerDistance);
        TickTimers(delta);
        OweMissing(state.Alive);

        if (!Active)
        {
            // Everything it is owed keeps accruing while it sleeps, capped at its population
            // so a field left alone overnight does not empty a hundred creatures onto the
            // player the moment they walk back over the ridge.
            _ready = Math.Min(_ready, Def.Count);

            return _spawns;
        }

        var budget = Math.Min(_ready, Math.Max(0, state.GlobalHeadroom));

        for (var i = 0; i < budget; i++) _spawns.Add(Pick());

        _ready -= budget;

        return _spawns;
    }

    /// <summary>Which creature the next slot produces. Weighted, off the field's own stream.</summary>
    private string Pick()
    {
        if (Def.Entries.Count == 0) return string.Empty;
        if (Def.Entries.Count == 1) return Def.Entries[0].EnemyId;

        var weights = new double[Def.Entries.Count];

        for (var i = 0; i < Def.Entries.Count; i++) weights[i] = Def.Entries[i].Weight;

        return Def.Entries[_rng.WeightedIndex(weights)].EnemyId;
    }

    private void UpdateActivation(double distance)
    {
        if (Active)
        {
            if (distance <= Def.ActivationRadius * DeactivationHysteresis) return;

            Active = false;
            ShouldClear = true;

            return;
        }

        Active = distance <= Def.ActivationRadius;
    }

    private void TickTimers(double delta)
    {
        for (var i = _timers.Count - 1; i >= 0; i--)
        {
            _timers[i] -= delta;

            if (_timers[i] > 0) continue;

            _timers.RemoveAt(i);
            _ready++;
        }
    }

    /// <summary>
    /// Starts a timer for every creature the field is short of. Missing is measured against
    /// what is alive plus what is already owed, so a slot is never counted twice.
    /// </summary>
    private void OweMissing(int alive)
    {
        var missing = Def.Count - alive - _timers.Count - _ready;

        for (var i = 0; i < missing; i++) _timers.Add(Def.RespawnSeconds);
    }
}
