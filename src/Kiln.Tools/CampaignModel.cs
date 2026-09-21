namespace Kiln.Tools;

/// <summary>A zone as the simulations see it: a level band and what it contains.</summary>
public sealed record Zone(
    string Name,
    int Band,
    /// <summary>Main-chain quests finished here. One per step of the road (doc 02 §9).</summary>
    int StoryQuests,
    int Shards,
    int ShardTier,
    int TrashKills,

    /// <summary>Upgrade level the player is expected to be running by the end of this zone.</summary>
    int TargetUpgrade,

    /// <summary>Sockets bored and bonus rerolls bought here, as planned play rather than obsession.</summary>
    int Bores,
    int Rerolls,

    /// <summary>The drop table this zone's enemies roll on. Moves into zone data in Phase 6.</summary>
    string DropTable);

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
        new("Prologue",        1,  1,  2, 1, 20, 0, 0, 0, "dt_valley_animal_t1"),
        new("Valley Approach", 4,  1,  5, 1, 45, 2, 1, 0, "dt_valley_animal_t2"),
        new("Valley Floor",    8,  1,  6, 2, 50, 4, 1, 1, "dt_valley_undead_t2"),
        new("Ridge",          12,  1,  7, 3, 55, 6, 1, 1, "dt_ridge_t3"),
        new("Broken Gate",    16,  1,  7, 3, 55, 7, 2, 2, "dt_gate_t4"),
        new("Catacombs",      20,  1,  6, 4, 60, 8, 2, 2, "dt_catacombs_t5"),
    ];
}
