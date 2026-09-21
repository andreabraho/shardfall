using Kiln.Core.Foundation;

namespace Kiln.Core.Quests;

/// <summary>One thing a quest asks for.</summary>
/// <param name="Type">Kill, reach, shard or clear-tower.</param>
/// <param name="Target">Enemy, zone or shard id, by type.</param>
/// <param name="Count">How many. Always one for anything that is not a kill.</param>
public sealed record QuestGoal(ObjectiveType Type, string Target, int Count = 1);

/// <summary>What finishing a quest pays.</summary>
public sealed record QuestReward(long Xp, long Yang, IReadOnlyList<string> Items);

/// <summary>A quest as the chain sees it.</summary>
/// <param name="Id">Content id.</param>
/// <param name="Name">Localisation key.</param>
/// <param name="LevelReq">Suggested level. Shown, never enforced — the chain does not stall.</param>
/// <param name="Prerequisite">The quest before this one, or null for the first.</param>
public sealed record QuestStep(
    string Id,
    string Name,
    int LevelReq,
    string? Prerequisite,
    IReadOnlyList<QuestGoal> Goals,
    QuestReward Reward);

/// <summary>What happened when the world reported something to the chain.</summary>
public enum QuestEventKind
{
    /// <summary>A goal moved forward but the quest is not done.</summary>
    Progressed,

    /// <summary>The active quest is finished and its reward is due.</summary>
    Completed,

    /// <summary>The next quest in the chain became active.</summary>
    Started,
}

public sealed record QuestEvent(QuestEventKind Kind, QuestStep Quest);

/// <summary>
/// The main quest chain (FR-8): at most one quest active, each finished by the world
/// reporting what the player did.
/// </summary>
/// <remarks>
/// Deliberately small (doc 02 §9). There is no journal, no abandoning, no choice of which
/// quest to pursue: the chain is a line, one quest leads to the next, and the only question
/// the game ever has to answer is "what am I doing now?".
/// <para>
/// Progress counts only while its quest is active. A wolf killed before the wolf quest
/// starts does not count — which is the rule every player expects, and the only one under
/// which "kill twelve wolves" still means going to find twelve wolves.
/// </para>
/// </remarks>
public sealed class QuestChain
{
    private readonly IReadOnlyList<QuestStep> _quests;
    private readonly HashSet<string> _completed = new(StringComparer.Ordinal);
    private int[] _progress = [];

    public QuestChain(IReadOnlyList<QuestStep> quests)
    {
        _quests = quests;
        Active = Next();
        _progress = Fresh(Active);
    }

    /// <summary>The quest being pursued, or null once the chain is finished.</summary>
    public QuestStep? Active { get; private set; }

    public IReadOnlyCollection<string> Completed => _completed;

    public bool IsComplete(string questId) => _completed.Contains(questId);

    /// <summary>How far each goal of the active quest has got, in goal order.</summary>
    public IReadOnlyList<int> Progress => _progress;

    /// <summary>
    /// Reports something the player did. Returns what it changed, in order: at most one
    /// <see cref="QuestEventKind.Progressed"/> or <see cref="QuestEventKind.Completed"/>,
    /// and a <see cref="QuestEventKind.Started"/> after a completion.
    /// </summary>
    public IReadOnlyList<QuestEvent> Report(ObjectiveType type, string target, int amount = 1)
    {
        if (Active is not { } quest || amount <= 0) return [];

        var moved = false;

        for (var i = 0; i < quest.Goals.Count; i++)
        {
            var goal = quest.Goals[i];

            if (goal.Type != type || !string.Equals(goal.Target, target, StringComparison.Ordinal)) continue;
            if (_progress[i] >= goal.Count) continue;

            _progress[i] = Math.Min(goal.Count, _progress[i] + amount);
            moved = true;
        }

        if (!moved) return [];

        if (!IsDone(quest)) return [new QuestEvent(QuestEventKind.Progressed, quest)];

        var events = new List<QuestEvent> { new(QuestEventKind.Completed, quest) };

        _completed.Add(quest.Id);
        Active = Next();
        _progress = Fresh(Active);

        if (Active is not null) events.Add(new QuestEvent(QuestEventKind.Started, Active));

        return events;
    }

    /// <summary>
    /// Restores a saved chain.
    /// </summary>
    /// <remarks>
    /// The active quest is recomputed from what is finished rather than trusted from the file:
    /// a quest removed or renamed in a patch would otherwise leave the save pointing at nothing
    /// and the chain stuck forever. Saved progress is kept only when the save's active quest is
    /// still the one the chain arrives at.
    /// </remarks>
    public void Load(IEnumerable<string> completed, string? active, IReadOnlyList<int>? progress)
    {
        _completed.Clear();

        foreach (var id in completed) _completed.Add(id);

        Active = Next();
        _progress = Fresh(Active);

        if (Active is null || Active.Id != active || progress is null) return;

        // One short of the goal at most. A patch that lowers a count (twelve wolves become ten)
        // would otherwise load a quest already at its target with nothing left to report, and
        // a quest only completes on a report — it would sit there finished and never pay out.
        for (var i = 0; i < _progress.Length && i < progress.Count; i++)
        {
            _progress[i] = Math.Clamp(progress[i], 0, Math.Max(0, Active.Goals[i].Count - 1));
        }
    }

    /// <summary>The first unfinished quest whose prerequisite is done, in list order.</summary>
    private QuestStep? Next() =>
        _quests.FirstOrDefault(q =>
            !_completed.Contains(q.Id)
            && (q.Prerequisite is null || _completed.Contains(q.Prerequisite)));

    private bool IsDone(QuestStep quest)
    {
        for (var i = 0; i < quest.Goals.Count; i++)
        {
            if (_progress[i] < quest.Goals[i].Count) return false;
        }

        return true;
    }

    private static int[] Fresh(QuestStep? quest) => quest is null ? [] : new int[quest.Goals.Count];
}
