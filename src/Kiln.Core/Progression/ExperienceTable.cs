namespace Kiln.Core.Progression;

/// <summary>
/// The experience curve (doc 06 §3).
/// <para>
/// The single most important number in the project's pacing. Metin2's curve is deliberately
/// brutal because an MMO must keep players subscribed for years; a single-player game must
/// let them <em>reach</em> its content. Level 60 is meant to arrive along the campaign,
/// with no farming, and the balance simulator asserts exactly that.
/// </para>
/// </summary>
public static class ExperienceTable
{
    public const int MaxLevel = 60;

    /// <summary>Experience needed to go from <paramref name="level"/> to the next one.</summary>
    public static long ToNextLevel(int level)
    {
        if (level < 1 || level >= MaxLevel) return 0;

        return (long)Math.Round(55 * Math.Pow(level, 1.85), MidpointRounding.AwayFromZero);
    }

    /// <summary>Total experience from level 1 to <paramref name="level"/>.</summary>
    public static long CumulativeTo(int level)
    {
        long total = 0;

        for (var n = 1; n < Math.Min(level, MaxLevel); n++)
        {
            total += ToNextLevel(n);
        }

        return total;
    }

    /// <summary>Experience a same-level trash enemy is worth.</summary>
    public static long TrashXp(int enemyLevel) =>
        (long)Math.Round(1.1 * Math.Pow(Math.Max(1, enemyLevel), 1.85), MidpointRounding.AwayFromZero);

    /// <summary>Experience for breaking a shard of this tier.</summary>
    public static long ShardXp(int tier, int level) =>
        (long)Math.Round(11 * Math.Pow(Math.Max(1, level), 1.85) * (1 + (0.25 * (tier - 1))),
            MidpointRounding.AwayFromZero);

    /// <summary>
    /// Experience for a main-chain quest.
    /// <para>
    /// Worth about three and a half of the old story quests, because the chain is short on
    /// purpose (doc 02 §9): one quest per step of the road now carries the share of experience
    /// that thirty did. Tuned so the pacing simulation still lands every zone on its band.
    /// </para>
    /// <para>
    /// All three sources share the same exponent on purpose. With different exponents the
    /// mix between questing, shard-breaking and killing drifts as the player levels, so a
    /// game balanced at level 10 quietly becomes a different game at level 40.
    /// </para>
    /// </summary>
    public static long QuestXp(int level) =>
        (long)Math.Round(140 * Math.Pow(Math.Max(1, level), 1.85), MidpointRounding.AwayFromZero);

    /// <summary>
    /// Adjusts a reward for the gap between the player and the zone they are in (FR-2.7).
    /// <para>
    /// Under-levelled players catch up faster so a wall cannot form; over-levelled players
    /// earn little so grinding an easy zone is never the efficient route. Both directions
    /// exist to keep the player inside the content, not to punish.
    /// </para>
    /// </summary>
    public static double CatchUpMultiplier(int playerLevel, int zoneBand)
    {
        if (playerLevel <= zoneBand - 3) return 1.35;
        if (playerLevel >= zoneBand + 5) return 0.25;

        return 1.0;
    }

    /// <summary>
    /// Enemies this far below the player stop being a fight: one hit kills them and they do
    /// not aggro. Running back through a cleared zone should cost patience, not time.
    /// </summary>
    public const int TrivialLevelGap = 8;

    public static bool IsTrivial(int playerLevel, int enemyLevel) =>
        enemyLevel <= playerLevel - TrivialLevelGap;
}
