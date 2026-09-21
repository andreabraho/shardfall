using Kiln.Core.Foundation;
using Kiln.Data.Definitions;
using Kiln.Data.Loading;
using Kiln.Data.Validation;
using Xunit;

namespace Kiln.Tests.Data;

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
        IEnumerable<VisualDef>? visuals = null,
        IEnumerable<ShardDef>? shards = null,
        IEnumerable<ZoneDef>? zones = null,
        IEnumerable<KitPieceDef>? kit = null,
        IEnumerable<NpcDef>? npcs = null) => new()
    {
        Npcs = Index(npcs),
        Shards = Index(shards),
        Zones = Index(zones),
        KitPieces = Index(kit),
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

    // -- the main chain (FR-8, doc 02 §9) ------------------------------------

    /// <summary>A hub with one camp of <c>mob_boar</c>, so a boar hunt is finishable.</summary>
    private static ContentDatabase ChainWorld(params QuestDef[] quests)
    {
        var hub = Zone("zone_hub", "hub", 1, 3);

        var camped = new ZoneDef
        {
            Id = hub.Id, Name = hub.Name, Kind = hub.Kind, LevelBand = hub.LevelBand,
            Scene = hub.Scene, Shrines = hub.Shrines, SafeRegions = hub.SafeRegions,
            SpawnFields =
            [
                new SpawnFieldDef
                {
                    Id = "spf_boars", Count = 4, RespawnSeconds = 20, ActivationRadius = 50, Radius = 8,
                    Entries = [new SpawnEntryDef { Enemy = "mob_boar", Weight = 1 }],
                },
            ],
        };

        return Db(
            enemies:
            [
                new EnemyDef { Id = "mob_boar", Name = "$m", Level = 2 },
                new EnemyDef { Id = "mob_nowhere", Name = "$m", Level = 2 },
            ],
            quests: quests,
            zones: [camped]);
    }

    private static QuestDef Hunt(string id, string enemy, int count = 5, string[]? after = null,
        QuestType type = QuestType.Story) => new()
    {
        Id = id,
        Name = $"$quest.{id}.name",
        Type = type,
        Prerequisites = after ?? [],
        Objectives = [new ObjectiveDef { Type = ObjectiveType.Kill, Target = enemy, Count = count }],
    };

    [Fact]
    public void Accepts_AHuntForACreatureTheWorldSpawns()
    {
        var report = ContentValidator.Validate(ChainWorld(Hunt("qst_a", "mob_boar")));

        Assert.False(HasError(report, "quest-chain"));
    }

    /// <summary>
    /// The one that strands a player hours in: a hunt for something no camp ever produces loads,
    /// runs, shows on the HUD, and can never be finished.
    /// </summary>
    [Fact]
    public void Flags_AHuntForACreatureNothingSpawns()
    {
        var report = ContentValidator.Validate(ChainWorld(Hunt("qst_a", "mob_nowhere")));

        Assert.True(HasError(report, "quest-chain"));
    }

    [Fact]
    public void Flags_ASideQuest()
    {
        var report = ContentValidator.Validate(ChainWorld(Hunt("qst_a", "mob_boar", type: QuestType.Side)));

        Assert.True(HasError(report, "quest-chain"));
    }

    [Fact]
    public void Flags_TwoQuestsFollowingTheSameOne()
    {
        // Two quests active at once, which the design has no room for (FR-8.3).
        var report = ContentValidator.Validate(ChainWorld(
            Hunt("qst_a", "mob_boar"),
            Hunt("qst_b", "mob_boar", after: ["qst_a"]),
            Hunt("qst_c", "mob_boar", after: ["qst_a"])));

        Assert.True(HasError(report, "quest-chain"));
    }

    [Fact]
    public void Flags_AQuestWithTwoPrerequisites()
    {
        var report = ContentValidator.Validate(ChainWorld(
            Hunt("qst_a", "mob_boar"),
            Hunt("qst_b", "mob_boar", after: ["qst_a"]),
            Hunt("qst_c", "mob_boar", after: ["qst_a", "qst_b"])));

        Assert.True(HasError(report, "quest-chain"));
    }

    [Fact]
    public void Flags_AnObjectiveTheChainDoesNotUse()
    {
        var quest = Hunt("qst_a", "mob_boar");
        var escort = new QuestDef
        {
            Id = quest.Id, Name = quest.Name, Type = quest.Type, Prerequisites = [],
            Objectives = [new ObjectiveDef { Type = ObjectiveType.Escort, Target = "npc_x", Count = 1 }],
        };

        Assert.True(HasError(ContentValidator.Validate(ChainWorld(escort)), "quest-chain"));
    }

    [Fact]
    public void Flags_ClearingSomethingThatIsNotATower()
    {
        var quest = new QuestDef
        {
            Id = "qst_a", Name = "$quest.qst_a.name", Type = QuestType.Story, Prerequisites = [],
            Objectives = [new ObjectiveDef { Type = ObjectiveType.ClearTower, Target = "zone_hub", Count = 1 }],
        };

        Assert.True(HasError(ContentValidator.Validate(ChainWorld(quest)), "quest-chain"));
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

    // -- greybox kit --------------------------------------------------------

    [Fact]
    public void Flags_KitPieceWithAZeroDimension()
    {
        // Invisible in the scene, and found by walking into it.
        var report = ContentValidator.Validate(Db(kit:
            [new KitPieceDef { Id = "kit_x", Size = [4, 0, 2] }]));

        Assert.True(HasError(report, "kit-size"));
    }

    [Fact]
    public void Flags_UnknownKitShape()
    {
        // Falls back to a box, so a ramp silently becomes a step nobody can climb.
        var report = ContentValidator.Validate(Db(kit:
            [new KitPieceDef { Id = "kit_x", Shape = "wedge", Size = [1, 1, 1] }]));

        Assert.True(HasError(report, "kit-shape"));
    }

    [Fact]
    public void Flags_NavigationGeometryThatIsNotSolid()
    {
        // Navigation is baked from collision, so this would carve a hole around decoration.
        var report = ContentValidator.Validate(Db(kit:
            [new KitPieceDef { Id = "kit_x", Size = [1, 1, 1], Solid = false, Navigation = true }]));

        Assert.True(HasError(report, "kit-shape"));
    }

    [Fact]
    public void Accepts_DecorationThatIsNeitherSolidNorBaked()
    {
        var report = ContentValidator.Validate(Db(kit:
            [new KitPieceDef { Id = "kit_x", Size = [1, 1, 1], Solid = false, Navigation = false }]));

        Assert.False(report.HasErrors);
    }

    [Fact]
    public void EmptyDatabase_ProducesNoErrors()
    {
        var report = ContentValidator.Validate(ContentDatabase.Empty());
        Assert.False(report.HasErrors);
    }
    // -- safe ground (WLD-12/13) --------------------------------------------

    private static SafeRegionDef Safe(string id = "saf_x", double radius = 40) =>
        new() { Id = id, Name = $"$safe.{id}.name", Radius = radius };

    private static ZoneDef Zone(
        string id, string kind, int min, int max, string[]? exits = null, SafeRegionDef[]? safe = null) => new()
    {
        Id = id,
        Name = $"$zone.{id}.name",
        Kind = kind,
        LevelBand = [min, max],
        Scene = "res://x.tscn",
        Exits = (exits ?? []).Select(e => new ZoneExitDef { To = e }).ToArray(),
        Shrines = [new ShrineDef { Id = $"shr_{id}", Name = $"$shrine.shr_{id}.name" }],
        SafeRegions = safe ?? (kind == "hub" ? [Safe($"saf_{id}")] : []),
    };

    [Fact]
    public void Accepts_TwoVillages()
    {
        // WLD-13. The rule used to be "exactly one hub" and would have rejected the world the
        // game is being built towards.
        var report = ContentValidator.Validate(Db(zones:
        [
            Zone("zone_hub_a", "hub", 1, 3, ["zone_hub_b"]),
            Zone("zone_hub_b", "hub", 3, 5, ["zone_hub_a"]),
        ]));

        Assert.False(report.HasErrors);
    }

    [Fact]
    public void Flags_WorldWithNoVillageAtAll()
    {
        var report = ContentValidator.Validate(Db(zones: [Zone("zone_a", "wilds", 1, 3)]));

        Assert.True(HasError(report, "world-graph"));
    }

    [Fact]
    public void Flags_VillageThatDeclaresNoSafeGround()
    {
        // A hub that is safe only because of its kind would be a second implementation of
        // "am I inside the village"; the runtime enforces the region, not the kind.
        var report = ContentValidator.Validate(Db(zones:
            [Zone("zone_hub", "hub", 1, 3, safe: [])]));

        Assert.True(HasError(report, "world-safe"));
    }

    [Fact]
    public void Flags_DungeonWithSafeGround()
    {
        var report = ContentValidator.Validate(Db(zones:
        [
            Zone("zone_hub", "hub", 1, 3, ["zone_deep"]),
            Zone("zone_deep", "dungeon", 3, 5, safe: [Safe("saf_deep", 10)]),
        ]));

        Assert.True(HasError(report, "world-safe"));
    }

    [Fact]
    public void Flags_SafeGroundWideEnoughToCoverTheMap()
    {
        var report = ContentValidator.Validate(Db(zones:
            [Zone("zone_hub", "hub", 1, 3, safe: [Safe("saf_x", 400)])]));

        Assert.True(HasError(report, "world-safe"));
    }

    [Fact]
    public void Flags_DuplicateSafeRegionId()
    {
        var report = ContentValidator.Validate(Db(zones:
        [
            Zone("zone_hub_a", "hub", 1, 3, ["zone_hub_b"], [Safe("saf_same")]),
            Zone("zone_hub_b", "hub", 3, 5, ["zone_hub_a"], [Safe("saf_same")]),
        ]));

        Assert.True(HasError(report, "world-safe"));
    }


    [Fact]
    public void Accepts_AVillageWithCampsAroundIt()
    {
        // This was an error until WLD-12, back when "hub" meant the whole map was safe. It is
        // now the intended shape — a village with creatures outside the wall — and the
        // distance between the two is enforced against real coordinates by the scene audit,
        // which is the only place that can see them.
        var hub = Zone("zone_hub", "hub", 1, 3);
        var camped = new ZoneDef
        {
            Id = hub.Id, Name = hub.Name, Kind = hub.Kind, LevelBand = hub.LevelBand,
            Scene = hub.Scene, Shrines = hub.Shrines, SafeRegions = hub.SafeRegions,
            SpawnFields =
            [
                new SpawnFieldDef
                {
                    Id = "spf_hollow", Count = 4, RespawnSeconds = 20,
                    ActivationRadius = 50, Radius = 8,
                    Entries = [new SpawnEntryDef { Enemy = "mob_x", Weight = 1 }],
                },
            ],
        };

        var report = ContentValidator.Validate(Db(
            enemies: [new EnemyDef { Id = "mob_x", Name = "$m", Level = 2 }],
            zones: [camped]));

        Assert.False(report.HasErrors);
    }

    // -- floor towers (FR-7.11–7.20) ----------------------------------------

    private static FloorDef Floor(
        string id, string task, int targets = 1, int decoys = 0, double seconds = 0,
        string boss = "", string shrine = "", bool bench = false, bool refuge = false) => new()
    {
        Id = id,
        Name = $"$floor.{id}.name",
        Task = task,
        Targets = targets,
        Decoys = decoys,
        Seconds = seconds,
        Boss = boss,
        Shrine = shrine,
        Bench = bench,
        Refuge = refuge,
    };

    /// <summary>A tower that passes every rule, so a test can break exactly one thing.</summary>
    private static ZoneDef Tower(params FloorDef[] floors)
    {
        var plain = Zone("zone_tower", "dungeon", 3, 6);

        return new ZoneDef
        {
            Id = plain.Id,
            Name = plain.Name,
            Kind = plain.Kind,
            LevelBand = plain.LevelBand,
            Scene = plain.Scene,
            Shrines = [new ShrineDef { Id = "shr_zone_tower", Name = "$shrine.shr_zone_tower.name" }],
            Floors = floors.Length > 0 ? floors : Sound(),
        };
    }

    private static FloorDef[] Sound() =>
    [
        Floor("flr_a", "break"),
        Floor("flr_b", "hold", seconds: 60),
        Floor("flr_c", "fight", boss: "mob_boss"),
        Floor("flr_d", "find", decoys: 4, shrine: "shr_zone_tower", bench: true, refuge: true),
        Floor("flr_e", "carry", targets: 3),
        Floor("flr_f", "fight", boss: "mob_boss"),
    ];

    private static ValidationReport Validate(ZoneDef tower) => ContentValidator.Validate(Db(
        enemies: [new EnemyDef { Id = "mob_boss", Name = "$m", Level = 5 }],
        zones: [Zone("zone_hub", "hub", 1, 3, [tower.Id]), tower]));

    [Fact]
    public void Accepts_ASoundTower()
    {
        Assert.False(Validate(Tower()).HasErrors);
    }

    /// <summary>
    /// FR-7.12, the rule the whole format rests on. Nine floors that all ask for the same
    /// thing are one floor nine times, and nothing else in the list can rescue that.
    /// </summary>
    [Fact]
    public void Flags_AFloorRepeatingTheVerbAboveIt()
    {
        var report = Validate(Tower(
            Floor("flr_a", "break"),
            Floor("flr_b", "break"),
            Floor("flr_c", "fight", boss: "mob_boss"),
            Floor("flr_d", "find", decoys: 4, shrine: "shr_zone_tower", bench: true, refuge: true),
            Floor("flr_e", "carry", targets: 3),
            Floor("flr_f", "fight", boss: "mob_boss")));

        Assert.True(HasError(report, "world-floors"));
    }

    [Fact]
    public void Flags_ATowerWithTooFewDistinctVerbs()
    {
        // Alternating two verbs passes the no-repeat rule and is still one floor six times.
        var report = Validate(Tower(
            Floor("flr_a", "break"),
            Floor("flr_b", "hold", seconds: 60),
            Floor("flr_c", "fight", boss: "mob_boss"),
            Floor("flr_d", "break", shrine: "shr_zone_tower", bench: true, refuge: true),
            Floor("flr_e", "hold", seconds: 60),
            Floor("flr_f", "fight", boss: "mob_boss")));

        Assert.True(HasError(report, "world-floors"));
    }

    [Fact]
    public void Flags_AThirdFloorThatIsNotABoss()
    {
        var report = Validate(Tower(
            Floor("flr_a", "break"),
            Floor("flr_b", "hold", seconds: 60),
            Floor("flr_c", "carry", targets: 3),
            Floor("flr_d", "find", decoys: 4, refuge: true),
            Floor("flr_e", "race", targets: 3, seconds: 60),
            Floor("flr_f", "fight", boss: "mob_boss")));

        Assert.True(HasError(report, "world-floors"));
    }

    [Fact]
    public void Flags_ABossOutOfStepWithThePulse()
    {
        var report = Validate(Tower(
            Floor("flr_a", "break"),
            Floor("flr_b", "fight", boss: "mob_boss"),
            Floor("flr_c", "fight", boss: "mob_boss"),
            Floor("flr_d", "find", decoys: 4, shrine: "shr_zone_tower", bench: true, refuge: true),
            Floor("flr_e", "carry", targets: 3),
            Floor("flr_f", "fight", boss: "mob_boss")));

        Assert.True(HasError(report, "world-floors"));
    }

    [Fact]
    public void Flags_ABossFloorWithNoShrineAfterIt()
    {
        var report = Validate(Tower(
            Floor("flr_a", "break"),
            Floor("flr_b", "hold", seconds: 60),
            Floor("flr_c", "fight", boss: "mob_boss"),
            Floor("flr_d", "find", decoys: 4, bench: true, refuge: true),
            Floor("flr_e", "carry", targets: 3),
            Floor("flr_f", "fight", boss: "mob_boss")));

        Assert.True(HasError(report, "world-floors"));
    }

    [Fact]
    public void Flags_ABossFloorWithNoBenchAfterIt()
    {
        var report = Validate(Tower(
            Floor("flr_a", "break"),
            Floor("flr_b", "hold", seconds: 60),
            Floor("flr_c", "fight", boss: "mob_boss"),
            Floor("flr_d", "find", decoys: 4, shrine: "shr_zone_tower", refuge: true),
            Floor("flr_e", "carry", targets: 3),
            Floor("flr_f", "fight", boss: "mob_boss")));

        Assert.True(HasError(report, "world-floors"));
    }

    [Fact]
    public void Flags_AFindFloorWithNothingToTellApart()
    {
        var report = Validate(Tower(
            Floor("flr_a", "break"),
            Floor("flr_b", "hold", seconds: 60),
            Floor("flr_c", "fight", boss: "mob_boss"),
            Floor("flr_d", "find", shrine: "shr_zone_tower", bench: true, refuge: true),
            Floor("flr_e", "carry", targets: 3),
            Floor("flr_f", "fight", boss: "mob_boss")));

        Assert.True(HasError(report, "world-floors"));
    }

    [Fact]
    public void Flags_AClockedFloorWithNoClock()
    {
        var report = Validate(Tower(
            Floor("flr_a", "break"),
            Floor("flr_b", "hold"),
            Floor("flr_c", "fight", boss: "mob_boss"),
            Floor("flr_d", "find", decoys: 4, shrine: "shr_zone_tower", bench: true, refuge: true),
            Floor("flr_e", "carry", targets: 3),
            Floor("flr_f", "fight", boss: "mob_boss")));

        Assert.True(HasError(report, "world-floors"));
    }

    [Fact]
    public void Flags_ABossFloorThatAlsoSendsWaves()
    {
        var crowded = Floor("flr_c", "fight", boss: "mob_boss");
        var report = Validate(Tower(
            Floor("flr_a", "break"),
            Floor("flr_b", "hold", seconds: 60),
            new FloorDef
            {
                Id = crowded.Id, Name = crowded.Name, Task = crowded.Task, Boss = crowded.Boss,
                Waves = ["mob_boss"],
            },
            Floor("flr_d", "find", decoys: 4, shrine: "shr_zone_tower", bench: true, refuge: true),
            Floor("flr_e", "carry", targets: 3),
            Floor("flr_f", "fight", boss: "mob_boss")));

        Assert.True(HasError(report, "world-floors"));
    }

    [Fact]
    public void Flags_AnUnknownVerb()
    {
        // "clear" in particular: the chore verb the format exists to avoid (FR-7.17).
        var report = Validate(Tower(
            Floor("flr_a", "clear"),
            Floor("flr_b", "hold", seconds: 60),
            Floor("flr_c", "fight", boss: "mob_boss"),
            Floor("flr_d", "find", decoys: 4, shrine: "shr_zone_tower", bench: true, refuge: true),
            Floor("flr_e", "carry", targets: 3),
            Floor("flr_f", "fight", boss: "mob_boss")));

        Assert.True(HasError(report, "world-floors"));
    }

    [Fact]
    public void Flags_FloorsOnAZoneThatIsNotADungeon()
    {
        var wilds = Zone("zone_field", "wilds", 3, 6);
        var report = ContentValidator.Validate(Db(
            enemies: [new EnemyDef { Id = "mob_boss", Name = "$m", Level = 5 }],
            zones:
            [
                Zone("zone_hub", "hub", 1, 3, ["zone_field"]),
                new ZoneDef
                {
                    Id = wilds.Id, Name = wilds.Name, Kind = wilds.Kind, LevelBand = wilds.LevelBand,
                    Scene = wilds.Scene, Shrines = wilds.Shrines, Exits = wilds.Exits,
                    Floors = [Floor("flr_a", "break")],
                },
            ]));

        Assert.True(HasError(report, "world-floors"));
    }

    // -- villagers ----------------------------------------------------------

    private static NpcDef Merchant(StockDef? stock) => new()
    {
        Id = "npc_test_idra",
        Name = "$npc.npc_test_idra.name",
        Role = NpcRole.Merchant,
        Zone = "zone_test",
        Visual = "mesh_test",
        Lines = ["Buying or selling?"],
        Stock = stock,
    };

    private static ContentDatabase VillageDb(params NpcDef[] npcs) => Db(
        items:
        [
            new ItemDef { Id = "mat_test_scrap", Name = "$x", SellValue = 40 },
            new ItemDef { Id = "wpn_test_blade", Name = "$x", Slot = EquipSlot.Weapon, SellValue = 100 },
        ],
        visuals: [new VisualDef { Id = "mesh_test" }],
        zones: [new ZoneDef { Id = "zone_test" }],
        npcs: npcs);

    [Fact]
    public void Accepts_AMerchantSellingMaterialsAndARotatingShelf()
    {
        var report = ContentValidator.Validate(VillageDb(Merchant(new StockDef { Staples = ["mat_test_scrap"], Rotating = 4 })));

        Assert.False(HasError(report, "npc"));
        Assert.False(HasError(report, "npc-stock"));
    }

    [Fact]
    public void Flags_AMerchantSellingGearAsAStapleOrSomethingMissing()
    {
        Assert.True(HasError(ContentValidator.Validate(
            VillageDb(Merchant(new StockDef { Staples = ["wpn_test_blade"] }))), "npc-stock"));

        Assert.True(HasError(ContentValidator.Validate(
            VillageDb(Merchant(new StockDef { Staples = ["mat_nothing"] }))), "npc-stock"));
    }

    [Fact]
    public void Flags_AMerchantThatCouldSellRareGear()
    {
        var stock = new StockDef { Rotating = 3, MaxRarity = Rarity.Rare };

        Assert.True(HasError(ContentValidator.Validate(VillageDb(Merchant(stock))), "npc-stock"));
    }

    [Fact]
    public void Flags_AMerchantWithNothingAndAVillagerWithNothingToSay()
    {
        Assert.True(HasError(ContentValidator.Validate(VillageDb(Merchant(null))), "npc-stock"));

        var mute = Merchant(new StockDef { Rotating = 2 });
        mute = new NpcDef { Id = mute.Id, Name = mute.Name, Role = NpcRole.Talk, Zone = "zone_test", Visual = "mesh_test" };

        Assert.True(HasError(ContentValidator.Validate(VillageDb(mute)), "npc"));
    }

    [Fact]
    public void Flags_AVillagerInAZoneThatDoesNotExist()
    {
        var lost = new NpcDef
        {
            Id = "npc_test_lost", Name = "$x", Role = NpcRole.Talk, Zone = "zone_nowhere", Visual = "mesh_test",
            Lines = ["Hello."], QuestLines = new() { ["qst_nothing"] = "Go." },
        };

        var report = ContentValidator.Validate(VillageDb(lost));

        Assert.Equal(2, report.Findings.Count(f => f.Rule == "npc"));
    }
}
