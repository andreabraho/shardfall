using Kiln.Core.Combat;
using Kiln.Core.Progression;
using Xunit;

namespace Kiln.Tests.Progression;

public class ExperienceTableTests
{
    [Fact]
    public void Curve_RisesWithLevel()
    {
        for (var level = 1; level < ExperienceTable.MaxLevel - 1; level++)
        {
            Assert.True(ExperienceTable.ToNextLevel(level + 1) > ExperienceTable.ToNextLevel(level));
        }
    }

    [Fact]
    public void Curve_IsSubQuadratic()
    {
        // The whole point of departing from the original: the cost per level must not
        // explode, or late levels become the grind wall the design exists to remove.
        var early = ExperienceTable.ToNextLevel(10);
        var late = ExperienceTable.ToNextLevel(50);

        Assert.True(late < early * 25, $"level 50 costs {late}, level 10 costs {early}");
    }

    [Fact]
    public void MaxLevel_CostsNothingFurther()
    {
        Assert.Equal(0, ExperienceTable.ToNextLevel(ExperienceTable.MaxLevel));
        Assert.Equal(0, ExperienceTable.ToNextLevel(ExperienceTable.MaxLevel + 5));
    }

    [Fact]
    public void TotalToCap_IsInTheDocumentedRange()
    {
        // Doc 06 §3 budgets roughly 2.1M experience for the whole campaign. A large drift
        // here silently rewrites the game's length.
        var total = ExperienceTable.CumulativeTo(ExperienceTable.MaxLevel);

        Assert.InRange(total, 1_900_000, 2_400_000);
    }

    [Fact]
    public void CatchUp_HelpsTheUnderLevelled()
    {
        Assert.Equal(1.35, ExperienceTable.CatchUpMultiplier(playerLevel: 10, zoneBand: 14), 3);
        Assert.Equal(1.0, ExperienceTable.CatchUpMultiplier(playerLevel: 14, zoneBand: 14), 3);
    }

    [Fact]
    public void CatchUp_MakesGrindingEasyZonesPointless()
    {
        Assert.Equal(0.25, ExperienceTable.CatchUpMultiplier(playerLevel: 20, zoneBand: 14), 3);
    }

    [Fact]
    public void TrivialEnemies_StopBeingAFight()
    {
        Assert.True(ExperienceTable.IsTrivial(playerLevel: 20, enemyLevel: 12));
        Assert.False(ExperienceTable.IsTrivial(playerLevel: 20, enemyLevel: 13));
    }
}

public class CharacterProgressionTests
{
    [Fact]
    public void StartsAtLevelOne_WithNothingSpent()
    {
        var p = new CharacterProgression();

        Assert.Equal(1, p.Level);
        Assert.Equal(0, p.UnspentAttributePoints);
        Assert.Equal(Attributes.Starting, p.TotalAttributes);
    }

    [Fact]
    public void Grant_LevelsUpAndAwardsPoints()
    {
        var p = new CharacterProgression();
        var levels = p.Grant(ExperienceTable.ToNextLevel(1));

        Assert.Single(levels);
        Assert.Equal(2, p.Level);
        Assert.Equal(CharacterProgression.AttributePointsPerLevel, p.UnspentAttributePoints);
        Assert.Equal(CharacterProgression.SkillPointsPerLevel, p.UnspentSkillPoints);
    }

    [Fact]
    public void Grant_CanCrossSeveralLevelsAtOnce()
    {
        // A story quest reward late in an act can easily be worth more than one level.
        var p = new CharacterProgression();
        var levels = p.Grant(ExperienceTable.CumulativeTo(5));

        Assert.Equal(4, levels.Count);
        Assert.Equal(5, p.Level);
    }

    [Fact]
    public void Grant_KeepsTheRemainder()
    {
        var p = new CharacterProgression();
        p.Grant(ExperienceTable.ToNextLevel(1) + 10);

        Assert.Equal(2, p.Level);
        Assert.Equal(10, p.Experience);
    }

    [Fact]
    public void Grant_IgnoresNonPositiveAmounts()
    {
        var p = new CharacterProgression();

        Assert.Empty(p.Grant(0));
        Assert.Empty(p.Grant(-500));
        Assert.Equal(1, p.Level);
    }

    [Fact]
    public void Cap_StopsLevellingAndClearsTheBar()
    {
        var p = new CharacterProgression();
        p.Grant(ExperienceTable.CumulativeTo(ExperienceTable.MaxLevel) + 5_000_000);

        Assert.Equal(ExperienceTable.MaxLevel, p.Level);
        Assert.True(p.IsMaxLevel);

        // A bar that can never fill is worse than no bar.
        Assert.Equal(0, p.Experience);
        Assert.Equal(1.0, p.LevelProgress, 6);
    }

    [Fact]
    public void SpendingAttributePoints_RaisesTheTotal()
    {
        var p = new CharacterProgression();
        p.Grant(ExperienceTable.ToNextLevel(1));

        Assert.True(p.SpendAttributePoint(AttributeKind.Str));
        Assert.True(p.SpendAttributePoint(AttributeKind.Vit));

        Assert.Equal(Attributes.StartingValue + 1, p.TotalAttributes.Str);
        Assert.Equal(Attributes.StartingValue + 1, p.TotalAttributes.Vit);
        Assert.Equal(CharacterProgression.AttributePointsPerLevel - 2, p.UnspentAttributePoints);
    }

    [Fact]
    public void CannotSpendPointsYouDoNotHave()
    {
        var p = new CharacterProgression();

        Assert.False(p.SpendAttributePoint(AttributeKind.Str));
        Assert.False(p.SpendSkillPoint());
    }

    [Fact]
    public void Respec_ReturnsEveryPoint()
    {
        var p = new CharacterProgression();
        p.Grant(ExperienceTable.CumulativeTo(6));

        var granted = p.UnspentAttributePoints;

        for (var i = 0; i < granted; i++)
        {
            p.SpendAttributePoint(AttributeKind.Dex);
        }

        Assert.Equal(0, p.UnspentAttributePoints);

        p.Respec();

        Assert.Equal(granted, p.UnspentAttributePoints);
        Assert.Equal(Attributes.Starting, p.TotalAttributes);
    }

    [Fact]
    public void Load_RestoresSavedState()
    {
        var p = new CharacterProgression();
        p.Load(level: 23, experience: 1234, attributePoints: 5, skillPoints: 2, assigned: new Attributes(10, 4, 0, 8));

        Assert.Equal(23, p.Level);
        Assert.Equal(1234, p.Experience);
        Assert.Equal(Attributes.StartingValue + 10, p.TotalAttributes.Str);
    }

    [Fact]
    public void Load_ClampsNonsense()
    {
        var p = new CharacterProgression();
        p.Load(level: 9999, experience: -50, attributePoints: -3, skillPoints: -1, assigned: default);

        Assert.Equal(ExperienceTable.MaxLevel, p.Level);
        Assert.Equal(0, p.Experience);
        Assert.Equal(0, p.UnspentAttributePoints);
    }

    [Fact]
    public void ProgressionFeedsStatBlock()
    {
        // The link that matters: levelling has to actually change the character.
        var p = new CharacterProgression();
        p.Grant(ExperienceTable.CumulativeTo(20));

        var stats = new StatBlock { Level = p.Level, Attributes = p.TotalAttributes };
        var baseline = new StatBlock { Level = 1, Attributes = Attributes.Starting };

        Assert.True(stats.MaxHp > baseline.MaxHp);
        Assert.True(stats.AttackPower > baseline.AttackPower);
    }
}
