namespace Kiln.Core.Quests;

/// <summary>
/// The things the training in the first village makes the player do (2026-09-27, at your call),
/// each the target of one <see cref="Foundation.ObjectiveType.Tutorial"/> goal.
/// </summary>
/// <remarks>
/// Strings rather than an enum because they are content targets, written in the quest data
/// like an enemy id; the validator checks each one against <see cref="All"/>.
/// </remarks>
public static class TutorialStep
{
    /// <summary>Spend status points in the character window. Counted per point.</summary>
    public const string SpendStat = "spend_stat";

    /// <summary>Put the first point into a skill.</summary>
    public const string LearnSkill = "learn_skill";

    /// <summary>Cast a skill from the bar.</summary>
    public const string UseSkill = "use_skill";

    /// <summary>Open the bag.</summary>
    public const string OpenBag = "open_bag";

    /// <summary>Upgrade a piece of gear at the smith.</summary>
    public const string Upgrade = "upgrade";

    /// <summary>Drink from the flask.</summary>
    public const string DrinkFlask = "drink_flask";

    /// <summary>Buy a flask draught from a merchant.</summary>
    public const string BuyDraught = "buy_draught";

    /// <summary>Pour a draught into the flask.</summary>
    public const string FillFlask = "fill_flask";

    /// <summary>Open a shrine.</summary>
    public const string UseShrine = "use_shrine";

    public static readonly IReadOnlyList<string> All =
        [SpendStat, LearnSkill, UseSkill, OpenBag, Upgrade, DrinkFlask, BuyDraught, FillFlask, UseShrine];
}
