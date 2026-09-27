using Kiln.Core.Combat;
using Kiln.Data.Loading;
using Kiln.Data.Validation;
using Xunit;

namespace Kiln.Tests.Data;

/// <summary>
/// Validates the actual shipping content under game/data. This is the test that turns the
/// validator from a tool you must remember to run into a gate you cannot forget.
/// </summary>
public class RealContentTests
{
    private static string DataRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "game", "data");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate game/data from the test assembly.");
    }

    [Fact]
    public void GameContent_Loads_WithoutErrors()
    {
        var result = ContentLoader.LoadFromDirectory(DataRoot());
        Assert.True(result.Success, "Content failed to load:\n  " + string.Join("\n  ", result.Errors));
    }

    [Fact]
    public void GameContent_PassesValidation()
    {
        var result = ContentLoader.LoadFromDirectory(DataRoot());
        var report = ContentValidator.Validate(result.Database);

        Assert.False(report.HasErrors, "Content validation failed:\n" + report.Format());
    }

    [Fact]
    public void GameContent_IsNotEmpty()
    {
        var db = ContentLoader.LoadFromDirectory(DataRoot()).Database;

        Assert.NotEmpty(db.Items);
        Assert.NotEmpty(db.Enemies);
        Assert.NotEmpty(db.Skills);
        Assert.NotEmpty(db.Quests);
        Assert.NotEmpty(db.Visuals);
    }

    [Fact]
    public void EveryCombatRole_HasAtLeastOneEnemy()
    {
        // The tactical layer of the redesign (doc 02 §2.4) needs all five roles to exist,
        // or wave composition silently degrades to "a pile of bruisers".
        var db = ContentLoader.LoadFromDirectory(DataRoot()).Database;
        var roles = db.Enemies.Values.Select(e => e.Role).Distinct().ToHashSet();

        foreach (var role in Enum.GetValues<Core.Foundation.EnemyRole>())
        {
            Assert.Contains(role, roles);
        }
    }

    [Fact]
    public void EveryUpgradePath_ReachesPlusNine()
    {
        var db = ContentLoader.LoadFromDirectory(DataRoot()).Database;

        foreach (var path in db.UpgradePaths.Values)
        {
            var top = path.Steps.Length == 0 ? 0 : path.Steps.Max(s => s.To);
            Assert.True(top == ContentValidator.MaxUpgradeLevel,
                $"{path.Id} tops out at +{top}, expected +{ContentValidator.MaxUpgradeLevel}");
        }
    }

    [Theory]
    [InlineData(Core.Foundation.Difficulty.Wanderer)]
    [InlineData(Core.Foundation.Difficulty.Disciple)]
    [InlineData(Core.Foundation.Difficulty.Adept)]
    [InlineData(Core.Foundation.Difficulty.Shardbound)]
    public void EveryTelegraph_IsEscapable_OnEveryDifficulty(Core.Foundation.Difficulty tier)
    {
        var db = ContentLoader.LoadFromDirectory(DataRoot()).Database;
        var settings = DifficultySettings.For(tier);

        foreach (var enemy in db.Enemies.Values)
        {
            foreach (var ability in enemy.Abilities)
            {
                if (ability.Telegraph is not { } tel) continue;

                var available = ability.Windup * settings.TelegraphScale;
                var needed = PlayerConstants.TimeToEscape(tel.Radius);

                Assert.True(available >= needed,
                    $"{enemy.Id}/{ability.Id} on {tier}: {available:F2}s available, {needed:F2}s needed");
            }
        }
    }

    /// <summary>
    /// The shape the user asked for (2026-09-27): the training in the first village, every step
    /// in order; then for each open map a hunt, a stone and its boss; then the catacombs.
    /// Written as a test so a content edit that quietly turns the chain into something else
    /// fails the build instead of shipping.
    /// </summary>
    [Fact]
    public void TheQuestChainIsTrainingThenHuntStoneAndBossPerMapThenTheTower()
    {
        var db = ContentLoader.LoadFromDirectory(DataRoot()).Database;
        var chain = Kiln.Data.Quests.QuestCatalogue.Chain(db);
        var steps = Kiln.Core.Quests.TutorialStep.All;
        var maps = new[] { "zone_ember_hollow", "zone_vale_approach", "zone_vale_floor", "zone_ridge", "zone_broken_gate" };

        Assert.Equal(steps.Count + (maps.Length * 3) + 1, chain.Count);

        for (var i = 0; i < steps.Count; i++)
        {
            var goal = Assert.Single(chain[i].Goals);
            Assert.Equal(Kiln.Core.Foundation.ObjectiveType.Tutorial, goal.Type);
            Assert.Equal(steps[i], goal.Target);
        }

        for (var m = 0; m < maps.Length; m++)
        {
            var zone = db.Zones[maps[m]];
            var at = steps.Count + (m * 3);
            var (hunt, stone, boss) = (Assert.Single(chain[at].Goals), Assert.Single(chain[at + 1].Goals), Assert.Single(chain[at + 2].Goals));

            Assert.Equal(Kiln.Core.Foundation.ObjectiveType.Kill, hunt.Type);
            Assert.Equal(Kiln.Core.Foundation.ObjectiveType.Shard, stone.Type);
            Assert.Contains(stone.Target, zone.Shards);
            Assert.Equal(Kiln.Core.Foundation.ObjectiveType.Kill, boss.Type);
            Assert.True(db.Enemies[boss.Target].Boss, $"{boss.Target} is not a boss");
            Assert.Contains(zone.SpawnFields, f => f.Entries.Any(e => e.Enemy == boss.Target));

            // The boss is what opens the road on.
            var onward = zone.Exits.Where(e => m + 1 < maps.Length ? e.To == maps[m + 1] : e.To == "zone_catacombs");
            Assert.Equal(chain[at + 2].Id, Assert.Single(onward).RequiredQuest);
        }

        Assert.Equal(Kiln.Core.Foundation.ObjectiveType.ClearTower, Assert.Single(chain[^1].Goals).Type);

        // The first quest has no prerequisite and each later one follows the one before it.
        Assert.Null(chain[0].Prerequisite);

        for (var i = 1; i < chain.Count; i++) Assert.Equal(chain[i - 1].Id, chain[i].Prerequisite);
    }
}
