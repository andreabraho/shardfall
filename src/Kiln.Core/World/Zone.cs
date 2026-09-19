namespace Kiln.Core.World;

/// <summary>What a zone is for, which decides what the game is allowed to do in it.</summary>
public enum ZoneKind
{
    /// <summary>The village. No hostile spawns, every service, always safe.</summary>
    Hub,

    /// <summary>Open country. Spawn fields, shard nodes, free to leave in any direction.</summary>
    Wilds,

    /// <summary>A gated interior. Checkpoints instead of shrines, and a boss at the end.</summary>
    Dungeon,
}

/// <summary>
/// The level range a zone is built for.
/// </summary>
/// <remarks>
/// A single-player game has no other players to calibrate against, so the band is the only
/// signal telling the player whether they have wandered somewhere they should not be. It is
/// therefore a promise the content has to keep, which is why the validator checks that the
/// bands of the reachable zones form an unbroken staircase — a gap would leave the player at
/// a level no zone is built for.
/// </remarks>
public readonly record struct LevelBand(int Min, int Max)
{
    public bool Contains(int level) => level >= Min && level <= Max;

    /// <summary>Levels above the top of the band (positive) or below its floor (negative).</summary>
    public int Delta(int level) => level < Min ? level - Min : level > Max ? level - Max : 0;

    public override string ToString() => $"{Min}–{Max}";
}

/// <summary>How dangerous a zone is for a given level. Drives the warning on the zone banner.</summary>
public enum ZoneDanger
{
    /// <summary>Well past it. Nothing here is worth the walk.</summary>
    Trivial,

    /// <summary>Inside the band, or within one level of it.</summary>
    Fair,

    /// <summary>Two to four levels under. Survivable if played carefully.</summary>
    Dangerous,

    /// <summary>Five or more levels under. The player is being told, plainly, to leave.</summary>
    Lethal,
}

/// <summary>A way out of a zone, and what it costs to be allowed through.</summary>
/// <param name="To">The zone on the other side.</param>
/// <param name="RequiredLevel">0 when the exit is always open.</param>
/// <param name="RequiredQuest">A quest that must be complete, or null.</param>
public sealed record ZoneExit(string To, int RequiredLevel = 0, string? RequiredQuest = null);

/// <summary>A shrine: the save point, the respawn point and the fast-travel node, in one object.</summary>
/// <remarks>
/// One object rather than three because every one of them is a promise the player makes a
/// route decision on — "I can get back here" has to mean the same thing each time. Splitting
/// them would let a shrine exist that saves but does not respawn, which is exactly the kind of
/// inconsistency that makes a player stop trusting the map.
/// </remarks>
public sealed record Shrine(string Id, string Name, string Zone, bool FastTravel = true);

/// <summary>A zone as the game reasons about it — not as it is laid out.</summary>
/// <remarks>
/// Deliberately holds no positions. Where a shrine stands and where a spawn field sits are
/// level design, which lives in the scene where it can be dragged; what a zone is called, who
/// it is for and what connects to it is content, which lives in data where it can be validated.
/// A zone therefore names its shrines and fields, and the scene supplies their coordinates.
/// </remarks>
public sealed record Zone(
    string Id,
    string Name,
    ZoneKind Kind,
    LevelBand Band,
    string Scene,
    IReadOnlyList<ZoneExit> Exits,
    IReadOnlyList<string> Shrines)
{
    public bool IsSafe => Kind == ZoneKind.Hub;

    public ZoneDanger DangerFor(int level)
    {
        var delta = Band.Delta(level);

        return delta switch
        {
            >= 6 => ZoneDanger.Trivial,
            >= -1 => ZoneDanger.Fair,
            >= -4 => ZoneDanger.Dangerous,
            _ => ZoneDanger.Lethal,
        };
    }
}
