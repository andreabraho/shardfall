using System.Collections.Generic;
using System.Linq;
using Kiln.Core.Foundation;
using Kiln.Core.Quests;
using Kiln.Game.World;

namespace Kiln.Game.Quests;

/// <summary>
/// Where a quest goal can be done, and which way to walk to get there.
/// </summary>
/// <remarks>
/// Shared by the objective panel and the map so the two can never disagree. A panel saying
/// "in Vale Approach" while the map points at the Ridge is worse than either saying nothing.
/// </remarks>
public static class QuestPlaces
{
    /// <summary>The goal still to be done on the active quest, or null.</summary>
    public static QuestGoal? CurrentGoal()
    {
        var chain = PlayerProfile.Quests;

        if (chain.Active is not { } quest) return null;

        for (var i = 0; i < quest.Goals.Count; i++)
        {
            var have = i < chain.Progress.Count ? chain.Progress[i] : 0;

            if (have < quest.Goals[i].Count) return quest.Goals[i];
        }

        return null;
    }

    /// <summary>Every map where this goal can be done.</summary>
    /// <remarks>For a hunt, every map with a camp or a tower floor that produces the creature.</remarks>
    public static List<string> ZonesFor(QuestGoal goal)
    {
        if (!GameContent.IsLoaded) return [];

        return goal.Type switch
        {
            ObjectiveType.Kill => GameContent.Database.Zones.Values
                .Where(z => z.SpawnFields.Any(f => f.Entries.Any(e => e.Enemy == goal.Target))
                            || z.Floors.Any(f => f.Waves.Contains(goal.Target) || f.Boss == goal.Target))
                .Select(z => z.Id)
                .OrderBy(id => id, System.StringComparer.Ordinal)
                .ToList(),

            ObjectiveType.Shard => GameContent.Database.Zones.Values
                .Where(z => z.Shards.Contains(goal.Target))
                .Select(z => z.Id)
                .ToList(),

            ObjectiveType.Reach or ObjectiveType.ClearTower => [goal.Target],

            _ => [],
        };
    }

    /// <summary>
    /// The neighbouring map to walk into on the shortest road to any of these, or null when
    /// the player is already in one of them or none can be reached.
    /// </summary>
    /// <remarks>
    /// Ignores level and quest locks on purpose. The map says which way the goal is; whether
    /// the border lets you through yet is the border's business, and it says so itself.
    /// </remarks>
    public static string? NextStep(string from, IReadOnlyCollection<string> targets)
    {
        if (targets.Count == 0 || targets.Contains(from)) return null;

        var cameFrom = new Dictionary<string, string>(System.StringComparer.Ordinal) { [from] = from };
        var queue = new Queue<string>();

        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var here = queue.Dequeue();

            foreach (var exit in GameWorld.Graph[here]?.Exits ?? [])
            {
                if (cameFrom.ContainsKey(exit.To)) continue;

                cameFrom[exit.To] = here;

                if (targets.Contains(exit.To))
                {
                    // Walk back to the first step out of the starting map.
                    var step = exit.To;

                    while (cameFrom[step] != from) step = cameFrom[step];

                    return step;
                }

                queue.Enqueue(exit.To);
            }
        }

        return null;
    }
}
