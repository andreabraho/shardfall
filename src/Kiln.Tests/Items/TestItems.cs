using Kiln.Core.Foundation;
using Kiln.Core.Items;

namespace Kiln.Tests.Items;

/// <summary>
/// A tiny hand-built catalogue. The item tests deliberately do not read the real content
/// tree: a balance change to a sword must never turn a rule test red.
/// </summary>
internal sealed class TestSpecs : IItemSpecs, IBonusPools
{
    private readonly Dictionary<string, ItemSpec> _specs = [];
    private readonly Dictionary<string, BonusPool> _pools = [];

    public TestSpecs Add(ItemSpec spec)
    {
        _specs[spec.Id] = spec;
        return this;
    }

    public TestSpecs Add(BonusPool pool)
    {
        _pools[pool.Id] = pool;
        return this;
    }

    public bool TryGet(string id, out ItemSpec spec) => _specs.TryGetValue(id, out spec!);

    bool IBonusPools.TryGet(string id, out BonusPool pool) => _pools.TryGetValue(id, out pool!);

    public BonusPool Pool(string id) => _pools[id];

    public static TestSpecs Standard()
    {
        var specs = new TestSpecs();

        specs.Add(new ItemSpec
        {
            Id = "wpn_test_sword",
            Slot = EquipSlot.Weapon,
            Rarity = Rarity.Rare,
            LevelReq = 10,
            Width = 1,
            Height = 3,
            WeaponDamageMin = 20,
            WeaponDamageMax = 40,
            Sockets = 2,
            MinBonusLines = 3,
            MaxBonusLines = 3,
            BonusPoolId = "bonus_test",
            UpgradePathId = "upg_test",
            ClassRestriction = [CharacterClass.Warrior],
        });

        specs.Add(new ItemSpec
        {
            Id = "arm_test_vest",
            Slot = EquipSlot.Armor,
            Rarity = Rarity.Common,
            LevelReq = 1,
            Width = 2,
            Height = 3,
            ArmorValue = 30,
            UpgradePathId = "upg_test",
        });

        specs.Add(new ItemSpec { Id = "mat_test_scrap", Width = 1, Height = 1, MaxStack = 99, SellValue = 10 });
        specs.Add(new ItemSpec { Id = "mat_boring_stone", Width = 1, Height = 1, MaxStack = 20 });
        specs.Add(new ItemSpec { Id = "mat_mutation_ink", Width = 1, Height = 1, MaxStack = 20 });

        specs.Add(new ItemSpec
        {
            Id = "stn_test_ember",
            Width = 1,
            Height = 1,
            MaxStack = 20,
            Grants = [new BonusLine("stn_test_ember", BonusStat.Parse("damage_pct"), 5)],
        });

        specs.Add(new BonusPool("bonus_test",
        [
            new BonusPoolEntry("bon_dmg", BonusStat.Parse("damage_pct"), 3, 12, 100),
            new BonusPoolEntry("bon_crit", BonusStat.Parse("crit_chance"), 1, 6, 50),
            new BonusPoolEntry("bon_hp", BonusStat.Parse("max_hp_flat"), 40, 260, 80),
            new BonusPoolEntry("bon_undead", BonusStat.Parse("vs_family.undead"), 5, 25, 40),
        ]));

        return specs;
    }

    /// <summary>The ladder the tests upgrade along: free to +2, then fallible with pity.</summary>
    public static UpgradeLadder Ladder() => new("upg_test",
    [
        new UpgradeStep(1, 100, new Dictionary<string, int> { ["mat_test_scrap"] = 1 }, 1.00, 0),
        new UpgradeStep(2, 200, new Dictionary<string, int> { ["mat_test_scrap"] = 2 }, 1.00, 0),
        new UpgradeStep(3, 400, new Dictionary<string, int> { ["mat_test_scrap"] = 3 }, 0.50, 2),
        new UpgradeStep(4, 800, new Dictionary<string, int> { ["mat_test_scrap"] = 4 }, 0.25, 3),
    ]);
}
