namespace Kiln.Tools;

/// <summary>A zone as the simulations see it: a level band and what it contains.</summary>
public sealed record Zone(
    string Name,
    int Band,
    int StoryQuests,
    int SideQuests,
    int Shards,
    int ShardTier,
    int TrashKills,

    /// <summary>Upgrade level the player is expected to be running by the end of this zone.</summary>
    int TargetUpgrade,

    /// <summary>Sockets bored and bonus rerolls bought here, as planned play rather than obsession.</summary>
    int Bores,
    int Rerolls);

/// <summary>
/// The MVP's planned content (doc 00 §3 Tier A, doc 02 §8), shared by the pacing and economy
/// simulations so the two can never describe different campaigns.
/// <para>
/// Placeholder until zones are real data in Phase 6. The point is that the shape is checkable
/// now, while it is still cheap to change.
/// </para>
/// </summary>
public static class CampaignModel
{
    public static readonly Zone[] Zones =
    [
        new("Prologue",        1,  2, 0,  2, 1, 20, 0, 0, 0),
        new("Valley Approach", 4,  2, 2,  5, 1, 45, 2, 1, 0),
        new("Valley Floor",    8,  2, 3,  6, 2, 50, 4, 1, 1),
        new("Ridge",          12,  2, 3,  7, 3, 55, 6, 1, 1),
        new("Broken Gate",    16,  3, 3,  7, 3, 55, 7, 2, 2),
        new("Catacombs",      20,  3, 3,  6, 4, 60, 8, 2, 2),
    ];
}
