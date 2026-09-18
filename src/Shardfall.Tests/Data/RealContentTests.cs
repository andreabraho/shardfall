using Shardfall.Core.Combat;
using Shardfall.Data.Loading;
using Shardfall.Data.Validation;
using Xunit;

namespace Shardfall.Tests.Data;

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
}
