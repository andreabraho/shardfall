namespace Kiln.Core.World;

/// <summary>What a floor asks of the player before it lets them down to the next one (FR-7.12).</summary>
/// <remarks>
/// Six verbs, and the requirement that matters is that a tower uses at least five of them and
/// never twice in a row. Nine floors that all ask for the same thing are one floor nine times,
/// and no amount of decoration on the other floors fixes that.
/// <para>
/// There is deliberately no "clear the floor" verb. It is a chore that exists to consume
/// subscription time, and we do not charge any (doc 02 §10.1, FR-7.17). Leaving it out of the
/// enum is cheaper than a rule forbidding it.
/// </para>
/// </remarks>
public enum FloorTask
{
    /// <summary>Destroy the one named thing on the floor.</summary>
    Break,

    /// <summary>Stay alive for a duration while the floor keeps sending more.</summary>
    Hold,

    /// <summary>Several look identical; one of them is behaving differently. Break that one.</summary>
    Find,

    /// <summary>Each target drops a key. Seat every key in the seal.</summary>
    Carry,

    /// <summary>Touching the first target starts a clock. Finish before it runs out.</summary>
    Race,

    /// <summary>A boss, alone, on an otherwise empty floor.</summary>
    Fight,
}

/// <summary>
/// One floor of a tower: the task, and the furniture that belongs on it.
/// </summary>
/// <param name="Id">Content id. The scene node names this to inherit its tuning.</param>
/// <param name="Index">One-based depth. Floor 1 is the entrance.</param>
/// <param name="Name">Localisation key for the floor's name.</param>
/// <param name="Task">The verb.</param>
/// <param name="Targets">How many things must go down. For <see cref="FloorTask.Fight"/>, the boss.</param>
/// <param name="Decoys">Extra lookalikes that score nothing. <see cref="FloorTask.Find"/> only.</param>
/// <param name="Seconds">Duration for <see cref="FloorTask.Hold"/>, deadline for <see cref="FloorTask.Race"/>.</param>
/// <param name="Boss">Enemy id for <see cref="FloorTask.Fight"/>.</param>
/// <param name="Shrine">Shrine id standing on this floor, or empty.</param>
/// <param name="Bench">Whether an upgrade bench stands on this floor (FR-7.14).</param>
/// <param name="Refuge">Whether this floor carries a low-threat corner (FR-7.18).</param>
public sealed record TowerFloor(
    string Id,
    int Index,
    string Name,
    FloorTask Task,
    int Targets = 1,
    int Decoys = 0,
    double Seconds = 0,
    string Boss = "",
    string Shrine = "",
    bool Bench = false,
    bool Refuge = false)
{
    /// <summary>What the floor keeps sending while its task runs. Empty on a boss floor.</summary>
    /// <remarks>
    /// Pressure, never the task itself (FR-7.17). A floor is never finished by clearing it,
    /// so a player who wants off a hold floor early cannot fight their way out of it.
    /// </remarks>
    public IReadOnlyList<string> Waves { get; init; } = [];

    /// <summary>How many arrive per wave.</summary>
    public int WaveSize { get; init; } = 3;

    /// <summary>Seconds between waves.</summary>
    public double WaveSeconds { get; init; } = 14.0;
}

/// <summary>Where a floor currently is.</summary>
public enum FloorPhase
{
    /// <summary>The player has not reached this floor yet.</summary>
    Sealed,

    /// <summary>The task is running.</summary>
    Running,

    /// <summary>The task is finished and the way down is open.</summary>
    Open,
}

/// <summary>
/// A run through a floor tower (FR-7.11): which floor, how far into its task, and whether
/// the way down is open.
/// </summary>
/// <remarks>
/// Engine-free on purpose (NFR-M.1). Everything here is counting and clocks, which is exactly
/// the part that is fiddly enough to get wrong and cheap enough to test — the race clock that
/// must not start until the first target falls, the hold clock that must, and the difference
/// between a floor that is finished and a floor whose clock merely lapsed.
/// <para>
/// The run knows nothing about pylons, doors or enemies. The scene tells it a target went
/// down; it tells the scene when the floor is open.
/// </para>
/// </remarks>
public sealed class TowerRun
{
    private readonly IReadOnlyList<TowerFloor> _floors;

    private int _index;
    private int _down;
    private double _clock;
    private bool _ticking;

    public TowerRun(IReadOnlyList<TowerFloor> floors, int startAt = 1)
    {
        if (floors.Count == 0) throw new ArgumentException("A tower needs at least one floor.", nameof(floors));

        _floors = floors;
        _index = Math.Clamp(startAt, 1, floors.Count) - 1;

        Begin();
    }

    /// <summary>The floor the player is standing on.</summary>
    public TowerFloor Floor => _floors[_index];

    /// <summary>One-based depth of the current floor.</summary>
    public int Depth => _index + 1;

    public int FloorCount => _floors.Count;

    public FloorPhase Phase { get; private set; }

    /// <summary>True once the last floor's task is finished.</summary>
    public bool Finished => Phase == FloorPhase.Open && _index == _floors.Count - 1;

    /// <summary>How many of the floor's targets are down.</summary>
    public int Scored => _down;

    /// <summary>
    /// Seconds still on the clock, or zero when the floor has none.
    /// </summary>
    /// <remarks>
    /// The same number counts down for both clock verbs, because from the player's side they
    /// are the same number: how long until this is over. What differs is which way it resolves.
    /// </remarks>
    public double Remaining => Floor.Seconds <= 0 ? 0 : Math.Max(0, Floor.Seconds - _clock);

    /// <summary>True while a race clock is actually running — it does not start until first blood.</summary>
    public bool ClockRunning => _ticking;

    /// <summary>Raised when a race clock runs out. The floor resets rather than the run ending.</summary>
    public bool JustFailed { get; private set; }

    /// <summary>Advances whatever clock the floor has.</summary>
    public void Tick(double delta)
    {
        JustFailed = false;

        if (Phase != FloorPhase.Running || !_ticking || delta <= 0) return;

        _clock += delta;

        if (_clock < Floor.Seconds) return;

        if (Floor.Task == FloorTask.Hold)
        {
            Complete();
            return;
        }

        if (Floor.Task == FloorTask.Race)
        {
            // The floor resets; the run does not end. Failing a timer by two seconds and
            // being thrown out of the whole tower is the kind of punishment that stops
            // people trying the interesting line.
            JustFailed = true;
            Begin();
        }
    }

    /// <summary>
    /// Reports that one of the floor's real targets went down.
    /// </summary>
    /// <remarks>
    /// Decoys never reach here. A lookalike on a Find floor is simply not one of these, which
    /// is why striking one costs nothing but time — the floor is a perception test, not a
    /// punishment for guessing (FR-7.16).
    /// </remarks>
    public void ScoreTarget()
    {
        if (Phase != FloorPhase.Running) return;

        // Touching the first target is what starts a race. Starting it on arrival would make
        // the floor about how fast the player reads a room they have never seen.
        if (Floor.Task == FloorTask.Race) _ticking = true;

        _down++;

        if (_down >= Floor.Targets) Complete();
    }

    /// <summary>Moves down to the next floor. Does nothing until the current one is open.</summary>
    public bool Descend()
    {
        if (Phase != FloorPhase.Open || _index >= _floors.Count - 1) return false;

        _index++;
        Begin();

        return true;
    }

    /// <summary>Puts the current floor back to the start of its task.</summary>
    private void Begin()
    {
        Phase = FloorPhase.Running;
        _down = 0;
        _clock = 0;

        // A hold clock runs from arrival; a race clock waits for the player to start it.
        _ticking = Floor.Task == FloorTask.Hold;
    }

    private void Complete()
    {
        Phase = FloorPhase.Open;
        _ticking = false;
    }
}
