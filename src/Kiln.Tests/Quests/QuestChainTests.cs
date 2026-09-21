using Kiln.Core.Foundation;
using Kiln.Core.Quests;
using Xunit;

namespace Kiln.Tests.Quests;

/// <summary>
/// The main quest chain. Written against the ways a short chain goes wrong that nobody notices
/// until the fifth hour: kills counted for a quest you did not have, a chain that stops
/// advancing, and a save that points at a quest that no longer exists.
/// </summary>
public class QuestChainTests
{
    private static readonly QuestReward Nothing = new(0, 0, []);

    private static QuestStep Hunt(string id, string enemy, int count, string? after = null) =>
        new(id, $"$quest.{id}.name", 1, after, [new QuestGoal(ObjectiveType.Kill, enemy, count)], Nothing);

    private static QuestChain Chain() => new(
    [
        Hunt("qst_a", "mob_boar", 3),
        Hunt("qst_b", "mob_wolf", 2, after: "qst_a"),
        new QuestStep("qst_c", "$quest.qst_c.name", 20, "qst_b",
            [new QuestGoal(ObjectiveType.ClearTower, "zone_catacombs")], Nothing),
    ]);

    [Fact]
    public void TheFirstQuestIsActiveFromTheStart()
    {
        Assert.Equal("qst_a", Chain().Active?.Id);
    }

    [Fact]
    public void KillsOfTheRightCreatureCountUp()
    {
        var chain = Chain();

        var events = chain.Report(ObjectiveType.Kill, "mob_boar");

        Assert.Equal(QuestEventKind.Progressed, Assert.Single(events).Kind);
        Assert.Equal(1, chain.Progress[0]);
    }

    [Fact]
    public void KillsOfAnythingElseAreIgnored()
    {
        var chain = Chain();

        Assert.Empty(chain.Report(ObjectiveType.Kill, "mob_wolf"));
        Assert.Equal(0, chain.Progress[0]);
    }

    /// <summary>
    /// The rule every player expects: a wolf killed before the wolf quest starts does not
    /// count, or "kill twelve wolves" stops meaning go and find them.
    /// </summary>
    [Fact]
    public void KillsForALaterQuestDoNotCountEarly()
    {
        var chain = Chain();

        chain.Report(ObjectiveType.Kill, "mob_wolf");
        chain.Report(ObjectiveType.Kill, "mob_wolf");

        for (var i = 0; i < 3; i++) chain.Report(ObjectiveType.Kill, "mob_boar");

        Assert.Equal("qst_b", chain.Active?.Id);
        Assert.Equal(0, chain.Progress[0]);
    }

    [Fact]
    public void FinishingAQuestCompletesItAndStartsTheNext()
    {
        var chain = Chain();

        chain.Report(ObjectiveType.Kill, "mob_boar");
        chain.Report(ObjectiveType.Kill, "mob_boar");
        var events = chain.Report(ObjectiveType.Kill, "mob_boar");

        Assert.Equal([QuestEventKind.Completed, QuestEventKind.Started], events.Select(e => e.Kind));
        Assert.Equal("qst_a", events[0].Quest.Id);
        Assert.Equal("qst_b", events[1].Quest.Id);
        Assert.True(chain.IsComplete("qst_a"));
    }

    [Fact]
    public void ProgressNeverRunsPastTheCount()
    {
        var chain = Chain();

        chain.Report(ObjectiveType.Kill, "mob_boar", 2);
        var events = chain.Report(ObjectiveType.Kill, "mob_boar", 50);

        Assert.Equal(QuestEventKind.Completed, events[0].Kind);
        Assert.Equal(0, chain.Progress[0]);
    }

    [Fact]
    public void ClearingTheTowerFinishesTheChain()
    {
        var chain = Chain();

        chain.Report(ObjectiveType.Kill, "mob_boar", 3);
        chain.Report(ObjectiveType.Kill, "mob_wolf", 2);

        Assert.Empty(chain.Report(ObjectiveType.ClearTower, "zone_somewhere_else"));

        var events = chain.Report(ObjectiveType.ClearTower, "zone_catacombs");

        Assert.Equal(QuestEventKind.Completed, Assert.Single(events).Kind);
        Assert.Null(chain.Active);
    }

    [Fact]
    public void AFinishedChainIgnoresEverything()
    {
        var chain = Chain();

        chain.Report(ObjectiveType.Kill, "mob_boar", 3);
        chain.Report(ObjectiveType.Kill, "mob_wolf", 2);
        chain.Report(ObjectiveType.ClearTower, "zone_catacombs");

        Assert.Empty(chain.Report(ObjectiveType.Kill, "mob_boar"));
    }

    [Fact]
    public void ALoadedChainResumesWithItsProgress()
    {
        var chain = Chain();

        chain.Load(["qst_a"], "qst_b", [1]);

        Assert.Equal("qst_b", chain.Active?.Id);
        Assert.Equal(1, chain.Progress[0]);
    }

    /// <summary>
    /// A save whose active quest was renamed in a patch must not strand the player. The chain
    /// works out where it is from what is finished, and drops progress that no longer applies.
    /// </summary>
    [Fact]
    public void ASaveNamingAQuestThatNoLongerExistsDoesNotStrandTheChain()
    {
        var chain = Chain();

        chain.Load(["qst_a"], "qst_renamed", [99]);

        Assert.Equal("qst_b", chain.Active?.Id);
        Assert.Equal(0, chain.Progress[0]);
    }

    /// <summary>
    /// A patch that lowers a count must leave the quest one kill from done, not already done
    /// with nothing left to trigger the payout.
    /// </summary>
    [Fact]
    public void LoadedProgressStopsOneShortSoTheNextKillFinishesIt()
    {
        var chain = Chain();

        chain.Load([], "qst_a", [40]);

        Assert.Equal(2, chain.Progress[0]);
        Assert.Equal(QuestEventKind.Completed, chain.Report(ObjectiveType.Kill, "mob_boar")[0].Kind);
    }
}
