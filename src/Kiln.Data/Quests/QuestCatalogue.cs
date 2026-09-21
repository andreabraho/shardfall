using Kiln.Core.Quests;
using Kiln.Data.Definitions;
using Kiln.Data.Loading;

namespace Kiln.Data.Quests;

/// <summary>Projects quest content into the engine-free chain.</summary>
public static class QuestCatalogue
{
    /// <summary>
    /// Every quest, first to last.
    /// </summary>
    /// <remarks>
    /// Walked from the quest with no prerequisite along each one's single follower, so the
    /// list reads in the order the player meets it. The validator guarantees the chain is a
    /// line; anything left over after the walk (which it would have reported) is appended in
    /// id order rather than dropped, so a data mistake degrades instead of vanishing.
    /// </remarks>
    public static IReadOnlyList<QuestStep> Chain(ContentDatabase db)
    {
        var ordered = new List<QuestStep>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var byId = db.Quests.Values.OrderBy(q => q.Id, StringComparer.Ordinal).ToList();

        var current = byId.FirstOrDefault(q => q.Prerequisites.Length == 0);

        while (current is not null && seen.Add(current.Id))
        {
            ordered.Add(ToStep(current));

            var id = current.Id;
            current = byId.FirstOrDefault(q => q.Prerequisites.Contains(id) && !seen.Contains(q.Id));
        }

        foreach (var quest in byId.Where(q => !seen.Contains(q.Id)))
        {
            ordered.Add(ToStep(quest));
        }

        return ordered;
    }

    private static QuestStep ToStep(QuestDef def) => new(
        def.Id,
        def.Name,
        def.LevelReq,
        def.Prerequisites.FirstOrDefault(),
        def.Objectives.Select(o => new QuestGoal(o.Type, o.Target, Math.Max(1, o.Count))).ToList(),
        new QuestReward(def.Rewards.Xp, def.Rewards.Yang, def.Rewards.Items));
}
