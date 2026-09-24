using Kiln.Core.Foundation;
using Kiln.Data.Definitions;
using Kiln.Data.Encounters;
using Kiln.Data.Loading;
using Xunit;

namespace Kiln.Tests.Encounters;

/// <summary>Who a shard's waves are drawn from (REF-06): the map's own creatures, never a boss.</summary>
public class ShardRosterTests
{
    private static EnemyDef Enemy(string id, int level, bool boss = false) =>
        new() { Id = id, Name = "$m", Role = EnemyRole.Bruiser, Level = level, Boss = boss };

    private static ContentDatabase Db() => new()
    {
        Enemies = new[]
        {
            Enemy("mob_rat", 2),
            Enemy("mob_orc", 3),
            Enemy("mob_wolf_boss", 3, boss: true),
        }.ToDictionary(e => e.Id, StringComparer.Ordinal),
        Zones = new[]
        {
            new ZoneDef
            {
                Id = "zone_valley",
                Name = "$z",
                SpawnFields =
                [
                    new SpawnFieldDef { Id = "spf_a", Entries = [new SpawnEntryDef { Enemy = "mob_orc" }] },
                    new SpawnFieldDef { Id = "spf_b", Entries = [new SpawnEntryDef { Enemy = "mob_wolf_boss" }] },
                ],
            },
        }.ToDictionary(z => z.Id, StringComparer.Ordinal),
        Items = new Dictionary<string, ItemDef>(),
        Skills = new Dictionary<string, SkillDef>(),
        Quests = new Dictionary<string, QuestDef>(),
        DropTables = new Dictionary<string, DropTableDef>(),
        BonusPools = new Dictionary<string, BonusPoolDef>(),
        UpgradePaths = new Dictionary<string, UpgradePathDef>(),
        Visuals = new Dictionary<string, VisualDef>(),
        Shards = new Dictionary<string, ShardDef>(),
        KitPieces = new Dictionary<string, KitPieceDef>(),
    };

    [Fact]
    public void NeverDrawsABoss()
    {
        var roster = new ShardCatalogue(Db());

        Assert.DoesNotContain("mob_wolf_boss", roster.ByRole(EnemyRole.Bruiser, 3));
    }

    [Fact]
    public void DrawsOnTheMapsOwnCreaturesFirst()
    {
        var db = Db();
        var roster = new ShardCatalogue(db, ShardCatalogue.ZonePool(db, "zone_valley"));

        Assert.Equal(["mob_orc"], roster.ByRole(EnemyRole.Bruiser, 3));
    }

    [Fact]
    public void FallsBackToTheWholeGameForARoleTheMapLacks()
    {
        var db = Db();
        var roster = new ShardCatalogue(db, new HashSet<string> { "mob_nobody" });

        Assert.Contains("mob_rat", roster.ByRole(EnemyRole.Bruiser, 3));
    }

    [Fact]
    public void FindsTheMapsBoss()
    {
        Assert.Equal("mob_wolf_boss", ShardCatalogue.ZoneBoss(Db(), "zone_valley"));
        Assert.Null(ShardCatalogue.ZoneBoss(Db(), "zone_elsewhere"));
    }
}
