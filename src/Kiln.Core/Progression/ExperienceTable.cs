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

    /// <summary>Experience a same-level trash enemy is worth. Cut by a fifth on 2026-09-25 (REF-08).</summary>
    public static long TrashXp(int enemyLevel) =>
        (long)Math.Round(0.88 * Math.Pow(Math.Max(1, enemyLevel), 1.85), MidpointRounding.AwayFromZero);

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
        (long)Math.Round(134 * Math.Pow(Math.Max(1, level), 1.85), MidpointRounding.AwayFromZero);

    /// <summary>
    /// Adjusts a reward for the gap between the player and the zone they are in (FR-2.7).
    /// <para>
    /// Under-levelled players catch up faster so a wall cannot form; over-levelled players
    /// earn little so grinding an easy zone is never the efficient route. Both directions
    /// exist to keep the player inside the content, not to punish.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Falls with every level the player stands above what they kill (2026-09-23). It used to
    /// be a step — full experience up to four levels over, a quarter from five — which made
    /// the creatures a few levels under the player the best farm in the game: quick to kill
    /// and paying in full. Now each level over costs a share, and five over pays nothing — a
    /// fifth a level, so the creatures of the zone behind the player are worth nothing at all.
    /// </remarks>
    public static double CatchUpMultiplier(int playerLevel, int zoneBand)
    {
        var over = playerLevel - zoneBand;

        if (over <= -3) return 1.35;
        if (over <= 0) return 1.0;

        return over switch
        {
            1 => 0.80,
            2 => 0.60,
            3 => 0.40,
            4 => 0.20,
            _ => 0.0,
        };
    }

    /// <summary>
    /// Enemies this far below the player are trivial: they give no mana back and drop half as
    /// much (2026-09-24). They are still a fight — no one-hit kill — and they still attack.
    /// </summary>
    public const int TrivialLevelGap = 8;

    /// <summary>The share of yang and item chances a trivial enemy still drops.</summary>
    public const double TrivialLootShare = 0.5;

    public static bool IsTrivial(int playerLevel, int enemyLevel) =>
        enemyLevel <= playerLevel - TrivialLevelGap;

    /// <summary>What fraction of its drop table an enemy pays this player.</summary>
    public static double LootShare(int playerLevel, int enemyLevel) =>
        IsTrivial(playerLevel, enemyLevel) ? TrivialLootShare : 1.0;
}
