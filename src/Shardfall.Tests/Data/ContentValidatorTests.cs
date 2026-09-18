using Shardfall.Core.Foundation;
using Shardfall.Data.Definitions;
using Shardfall.Data.Loading;
using Shardfall.Data.Validation;
using Xunit;

namespace Shardfall.Tests.Data;

/// <summary>
/// Each test proves a rule actually catches the mistake it exists for. A validator whose
/// rules are never tested quietly stops catching things.
/// </summary>
public class ContentValidatorTests
{
    private static ContentDatabase Db(
        IEnumerable<ItemDef>? items = null,
        IEnumerable<EnemyDef>? enemies = null,
        IEnumerable<SkillDef>? skills = null,
        IEnumerable<QuestDef>? quests = null,
        IEnumerable<DropTableDef>? dropTables = null,
        IEnumerable<BonusPoolDef>? bonusPools = null,
        IEnumerable<UpgradePathDef>? upgradePaths = null,
        IEnumerable<VisualDef>? visuals = null) => new()
    {
        Items = Index(items),
        Enemies = Index(enemies),
        Skills = Index(skills),
        Quests = Index(quests),
        DropTables = Index(dropTables),
        BonusPools = Index(bonusPools),
        UpgradePaths = Index(upgradePaths),
        Visuals = Index(visuals),
    };

    private static Dictionary<string, T> Index<T>(IEnumerable<T>? defs) where T : ContentDefBase
    {
        var dict = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var d in defs ?? [])
        {
            d.SourceFile = "test.json";
            dict[d.Id] = d;
        }

        return dict;
    }

    private static bool HasError(ValidationReport report, string rule) =>
        report.Findings.Any(f => f.Severity == Severity.Error && f.Rule == rule);

    // -- id format ----------------------------------------------------------

    [Fact]
    public void Flags_MalformedId()
    {
        var report = ContentValidator.Validate(Db(items: [new ItemDef { Id = "BadId", Name = "$x" }]));
        Assert.True(HasError(report, "id-format"));
    }

    // -- localisation -------------------------------------------------------

    [Fact]
    public void Flags_LiteralPlayerFacingText()
    {
        var report = ContentValidator.Validate(Db(items: [new ItemDef { Id = "wpn_x", Name = "Iron Sword" }]));
        Assert.True(HasError(report, "localisation"));
    }

    [Fact]
    public void Accepts_LocalisationKey()
    {
        var report = ContentValidator.Validate(Db(items: [new ItemDef { Id = "wpn_x", Name = "$item.wpn_x.name" }]));
        Assert.False(HasError(report, "localisation"));
    }

    // -- cross references ---------------------------------------------------

    [Fact]
    public void Flags_MissingDropTable()
    {
        var report = ContentValidator.Validate(Db(enemies:
            [new EnemyDef { Id = "mob_x", Name = "$m", Level = 5, DropTable = "dt_does_not_exist" }]));

        Assert.True(HasError(report, "cross-ref"));
    }

    [Fact]
    public void Flags_MissingVisual()
    {
        var report = ContentValidator.Validate(Db(enemies:
            [new EnemyDef { Id = "mob_x", Name = "$m", Level = 5, Visual = "mesh_missing" }]));

        Assert.True(HasError(report, "cross-ref"));
    }

    [Fact]
    public void Flags_DropTableReferencingUnknownItem()
    {
        var report = ContentValidator.Validate(Db(dropTables:
        [
            new DropTableDef
            {
                Id = "dt_x",
                Entries = [new DropEntryDef { Item = "itm_ghost", Weight = 1 }],
            },
        ]));

        Assert.True(HasError(report, "cross-ref"));
    }

    // -- upgrade safety (decision D7 / FR-5.5) ------------------------------

    [Fact]
    public void Flags_DestructiveUpgradePath()
    {
        var report = ContentValidator.Validate(Db(upgradePaths:
        [
            new UpgradePathDef
            {
                Id = "upg_x",
                OnFailure = "destroy_item",
                Steps = [new UpgradeStepDef { To = 1, Chance = 1.0 }],
            },
        ]));

        Assert.True(HasError(report, "upgrade-safety"));
    }

    [Fact]
    public void Flags_FallibleStepWithoutPity()
    {
        var report = ContentValidator.Validate(Db(upgradePaths:
        [
            new UpgradePathDef
            {
                Id = "upg_x",
                Steps = [new UpgradeStepDef { To = 1, Chance = 0.4, Pity = 0 }],
            },
        ]));

        Assert.True(HasError(report, "upgrade-safety"));
    }

    [Fact]
    public void Accepts_FallibleStepWithPity()
    {
        var report = ContentValidator.Validate(Db(upgradePaths:
        [
            new UpgradePathDef
            {
                Id = "upg_x",
                Steps = [new UpgradeStepDef { To = 1, Chance = 0.4, Pity = 3 }],
            },
        ]));

        Assert.False(HasError(report, "upgrade-safety"));
    }

    [Fact]
    public void Flags_NonAscendingUpgradeSteps()
    {
        var report = ContentValidator.Validate(Db(upgradePaths:
        [
            new UpgradePathDef
            {
                Id = "upg_x",
                Steps =
                [
                    new UpgradeStepDef { To = 3, Chance = 1.0 },
                    new UpgradeStepDef { To = 2, Chance = 1.0 },
                ],
            },
        ]));

        Assert.True(HasError(report, "upgrade-safety"));
    }

    // -- quest graph --------------------------------------------------------

    [Fact]
    public void Flags_PrerequisiteCycle()
    {
        var report = ContentValidator.Validate(Db(quests:
        [
            new QuestDef { Id = "qst_a", Name = "$a", Prerequisites = ["qst_b"], Type = QuestType.Story },
            new QuestDef { Id = "qst_b", Name = "$b", Prerequisites = ["qst_a"], Type = QuestType.Story },
        ]));

        Assert.True(HasError(report, "quest-graph"));
    }

    [Fact]
    public void Flags_NoStartingQuest()
    {
        var report = ContentValidator.Validate(Db(quests:
        [
            new QuestDef { Id = "qst_a", Name = "$a", Prerequisites = ["qst_b"], Type = QuestType.Story },
            new QuestDef { Id = "qst_b", Name = "$b", Prerequisites = ["qst_c"], Type = QuestType.Story },
            new QuestDef { Id = "qst_c", Name = "$c", Prerequisites = ["qst_a"], Type = QuestType.Story },
        ]));

        Assert.True(HasError(report, "quest-graph"));
    }

    [Fact]
    public void Accepts_LinearQuestChain()
    {
        var report = ContentValidator.Validate(Db(quests:
        [
            new QuestDef { Id = "qst_a", Name = "$a", Prerequisites = [], Type = QuestType.Story },
            new QuestDef { Id = "qst_b", Name = "$b", Prerequisites = ["qst_a"], Type = QuestType.Story },
        ]));

        Assert.False(HasError(report, "quest-graph"));
    }

    // -- side quest rewards (FR-8.4) ----------------------------------------

    [Fact]
    public void Flags_SideQuestWithOnlyXpAndYang()
    {
        var report = ContentValidator.Validate(Db(quests:
        [
            new QuestDef
            {
                Id = "qst_filler", Name = "$f", Type = QuestType.Side, Prerequisites = [],
                Rewards = new QuestRewardsDef { Xp = 500, Yang = 300 },
            },
        ]));

        Assert.True(HasError(report, "side-quest-reward"));
    }

    [Fact]
    public void Accepts_SideQuestWithAnUnlock()
    {
        var report = ContentValidator.Validate(Db(quests:
        [
            new QuestDef
            {
                Id = "qst_good", Name = "$g", Type = QuestType.Side, Prerequisites = [],
                Rewards = new QuestRewardsDef { Xp = 500, Yang = 300, Unlock = "recipe_x" },
            },
        ]));

        Assert.False(HasError(report, "side-quest-reward"));
    }

    // -- BAL-03 telegraph escape --------------------------------------------

    [Fact]
    public void Flags_TelegraphTooShortToEscape()
    {
        // A 5 m circle needs ~1.31 s to leave; a 0.5 s wind-up is impossible on any tier.
        var report = ContentValidator.Validate(Db(enemies:
        [
            new EnemyDef
            {
                Id = "mob_x", Name = "$m", Level = 10,
                Abilities =
                [
                    new AbilityDef
                    {
                        Id = "abl_slam", Windup = 0.5,
                        Telegraph = new TelegraphDef { Shape = TelegraphShape.Circle, Radius = 5.0 },
                    },
                ],
            },
        ]));

        Assert.True(HasError(report, "telegraph-escape"));
    }

    [Fact]
    public void Accepts_TelegraphWithEnoughWindup()
    {
        var report = ContentValidator.Validate(Db(enemies:
        [
            new EnemyDef
            {
                Id = "mob_x", Name = "$m", Level = 10,
                Abilities =
                [
                    new AbilityDef
                    {
                        Id = "abl_slam", Windup = 2.4,
                        Telegraph = new TelegraphDef { Shape = TelegraphShape.Circle, Radius = 5.0 },
                    },
                ],
            },
        ]));

        Assert.False(HasError(report, "telegraph-escape"));
    }

    [Fact]
    public void Ignores_UntelegraphedAttacks()
    {
        // A fast melee swing has no ground decal, so the escape rule does not apply to it.
        var report = ContentValidator.Validate(Db(enemies:
        [
            new EnemyDef
            {
                Id = "mob_x", Name = "$m", Level = 10,
                Abilities = [new AbilityDef { Id = "abl_bite", Windup = 0.4, Telegraph = null }],
            },
        ]));

        Assert.False(HasError(report, "telegraph-escape"));
    }

    // -- weights ------------------------------------------------------------

    [Fact]
    public void Flags_DropTableThatCanNeverDrop()
    {
        var report = ContentValidator.Validate(Db(
            items: [new ItemDef { Id = "itm_x", Name = "$x" }],
            dropTables:
            [
                new DropTableDef { Id = "dt_x", Entries = [new DropEntryDef { Item = "itm_x", Weight = 0 }] },
            ]));

        Assert.True(HasError(report, "weights"));
    }

    [Fact]
    public void Flags_InvertedBonusRange()
    {
        var report = ContentValidator.Validate(Db(bonusPools:
        [
            new BonusPoolDef
            {
                Id = "bonus_x",
                Lines = [new BonusLineDef { Id = "bon_x", Stat = "damage_pct", Range = [12, 3], Weight = 1 }],
            },
        ]));

        Assert.True(HasError(report, "weights"));
    }

    // -- level ranges -------------------------------------------------------

    [Fact]
    public void Flags_ItemAboveLevelCap()
    {
        var report = ContentValidator.Validate(Db(items:
            [new ItemDef { Id = "wpn_x", Name = "$x", LevelReq = 105 }]));

        Assert.True(HasError(report, "level-range"));
    }

    [Fact]
    public void EmptyDatabase_ProducesNoErrors()
    {
        var report = ContentValidator.Validate(ContentDatabase.Empty());
        Assert.False(report.HasErrors);
    }
}
