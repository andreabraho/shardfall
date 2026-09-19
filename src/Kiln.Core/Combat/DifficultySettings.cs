using Kiln.Core.Foundation;

namespace Kiln.Core.Combat;

/// <summary>
/// The difficulty table from doc 02 §1.
/// <para>
/// Two rules are load-bearing and must not be quietly broken later:
/// difficulty never changes drop tables or XP, and it never changes enemy count in the
/// campaign (count scaling belongs to the endgame tower affixes only).
/// </para>
/// </summary>
public sealed record DifficultySettings(
    Difficulty Tier,
    double EnemyHpMultiplier,
    double EnemyDamageMultiplier,
    double AoeTelegraphSeconds,
    int FlaskCharges,
    double YangLossOnDeath,
    bool MidDungeonCheckpoints)
{
    /// <summary>
    /// The Disciple telegraph duration is the authoring baseline: ability wind-ups in JSON
    /// are written for Disciple, and other tiers scale from it.
    /// </summary>
    public const double BaselineTelegraphSeconds = 1.5;

    /// <summary>Multiplier applied to an ability's authored wind-up on this tier.</summary>
    public double TelegraphScale => AoeTelegraphSeconds / BaselineTelegraphSeconds;

    public static readonly DifficultySettings Wanderer =
        new(Difficulty.Wanderer, 0.70, 0.55, 2.00, 7, 0.00, true);

    public static readonly DifficultySettings Disciple =
        new(Difficulty.Disciple, 1.00, 1.00, 1.50, 5, 0.10, true);

    public static readonly DifficultySettings Adept =
        new(Difficulty.Adept, 1.45, 1.60, 1.10, 4, 0.25, true);

    public static readonly DifficultySettings Shardbound =
        new(Difficulty.Shardbound, 2.10, 2.40, 0.85, 3, 0.25, false);

    public static readonly IReadOnlyList<DifficultySettings> All =
        [Wanderer, Disciple, Adept, Shardbound];

    public static DifficultySettings For(Difficulty tier) => tier switch
    {
        Difficulty.Wanderer => Wanderer,
        Difficulty.Disciple => Disciple,
        Difficulty.Adept => Adept,
        Difficulty.Shardbound => Shardbound,
        _ => Disciple,
    };
}

/// <summary>
/// Player constants that gameplay rules and the content validator both depend on.
/// These live in one place so a movement-speed change automatically re-validates every
/// telegraph in the game (BAL-03).
/// </summary>
public static class PlayerConstants
{
    /// <summary>Base move speed in metres/second (doc 06 §2).</summary>
    public const double BaseMoveSpeed = 5.2;

    /// <summary>
    /// Fraction of maximum mana a kill restores.
    /// <para>
    /// Mana regeneration alone cannot fund skills: a trickle that refills the pool over a
    /// minute means the right play is to auto-attack and wait, which is the opposite of what
    /// the skill system is for. Tying the refund to kills makes using a skill to end a fight
    /// faster pay for the next one, so the resource follows the loop instead of throttling it.
    /// </para>
    /// <para>
    /// At roughly a tenth of the pool, an average skill costs about one kill. Enemies far
    /// below the player give nothing, for the same reason they give no experience — farming
    /// trivia must never be the efficient route.
    /// </para>
    /// </summary>
    public const double ManaPerKillFraction = 0.10;

    /// <summary>
    /// Budget for "see the decal, decide, move the mouse, click" under click-to-move.
    /// Higher than an action game's reaction budget on purpose: the player cannot simply
    /// hold a direction, they must aim and click a destination first.
    /// </summary>
    public const double ClickReactionSeconds = 0.25;

    /// <summary>Extra clearance required beyond the telegraph edge, in metres.</summary>
    public const double SafetyMarginMetres = 0.5;

    /// <summary>
    /// Worst-case time for a player standing dead-centre to leave a telegraph of this size.
    /// Used by the BAL-03 validator rule.
    /// </summary>
    public static double TimeToEscape(double radiusMetres, double moveSpeed = BaseMoveSpeed) =>
        ClickReactionSeconds + ((radiusMetres + SafetyMarginMetres) / moveSpeed);
}
