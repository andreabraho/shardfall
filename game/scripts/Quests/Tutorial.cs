using System.Collections.Generic;
using System.Linq;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.Quests;

namespace Kiln.Game.Quests;

/// <summary>
/// The training in Ember Hollow (2026-09-27, at your call): the things the player is made to do
/// before the village lets them out — spend their points, learn and use a skill, open the bag,
/// upgrade at the smith, drink, buy and pour a draught, use the shrine.
/// </summary>
/// <remarks>
/// Each step is a quest on the main chain with one <see cref="ObjectiveType.Tutorial"/> goal, so
/// the objective panel, the notices and the save all work as they do for any quest. The places
/// in the game where each thing happens call <see cref="Did"/>; this class only knows the steps.
/// </remarks>
public static class Tutorial
{
    /// <summary>Tells the chain the player did one training step. Counts only while it is the current one.</summary>
    public static void Did(SceneTree? tree, string step, int times = 1)
    {
        if (tree is null || !GameContent.IsLoaded) return;

        QuestTracker.Report(tree, ObjectiveType.Tutorial, step, times);
        CatchUp(tree);
    }

    /// <summary>Whether this step is the one the player is being asked to do now.</summary>
    public static bool IsCurrent(string step) =>
        QuestPlaces.CurrentGoal() is { Type: ObjectiveType.Tutorial } goal && goal.Target == step;

    /// <summary>The training quests, in no particular order.</summary>
    public static IEnumerable<string> QuestIds =>
        GameContent.IsLoaded
            ? GameContent.Database.Quests.Values.Where(q => q.Type == QuestType.Tutorial).Select(q => q.Id)
            : [];

    /// <summary>Whether the training is over — or there is none.</summary>
    public static bool IsDone => QuestIds.All(PlayerProfile.Quests.IsComplete);

    /// <summary>
    /// A character from before the training existed, already out in the world, is not sent back
    /// to it: from level 2 on it counts as done. A level-1 character does it like anyone new.
    /// </summary>
    public static void SkipForVeterans(int level)
    {
        if (level < 2 || IsDone) return;

        PlayerProfile.Quests.Skip(QuestIds);
        GD.Print("[quest] training skipped — the character was already out in the world");
    }

    /// <summary>
    /// Finishes a step the player has no way left to do: the status points already spent, or
    /// no skill point to spend. Only happens to a character older than the training.
    /// </summary>
    public static void CatchUp(SceneTree tree)
    {
        // A few times over: finishing one stuck step can leave the next one stuck too.
        for (var i = 0; i < TutorialStep.All.Count && CatchUpOnce(tree); i++) { }
    }

    private static bool CatchUpOnce(SceneTree tree)
    {
        if (!GameContent.IsLoaded || QuestPlaces.CurrentGoal() is not { Type: ObjectiveType.Tutorial } goal) return false;

        var progression = PlayerProfile.Progression;
        var chain = PlayerProfile.Quests;
        var have = chain.Progress.Count > 0 ? chain.Progress[0] : 0;

        var stuck = goal.Target switch
        {
            TutorialStep.SpendStat => progression.UnspentAttributePoints < goal.Count - have,
            TutorialStep.LearnSkill => progression.UnspentSkillPoints == 0,
            _ => false,
        };

        if (!stuck) return false;

        GD.Print($"[quest] training step {goal.Target} cannot be done by this character; counted as done");
        QuestTracker.Report(tree, ObjectiveType.Tutorial, goal.Target, goal.Count - have);

        return true;
    }
}
