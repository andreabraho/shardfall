using Kiln.Core.Combat;
using Kiln.Core.Foundation;
using Kiln.Core.Items;
using Xunit;

namespace Kiln.Tests.Items;

public class BonusStatTests
{
    [Theory]
    [InlineData("damage_pct")]
    [InlineData("max_hp_flat")]
    [InlineData("vs_family.undead")]
    [InlineData("resist.fire")]
    [InlineData("resist_all")]
    public void KnownKeys_RoundTrip(string key)
    {
        Assert.True(BonusStat.TryParse(key, out var stat, out _));
        Assert.Equal(key, stat.Key);
    }

    [Theory]
    [InlineData("damage_pcnt")]
    [InlineData("vs_family.dragon")]
    [InlineData("resist.holy")]
    [InlineData("")]
    public void UnknownKeys_FailWithAMessage(string key)
    {
        Assert.False(BonusStat.TryParse(key, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void PercentStats_AreStoredAsFractions()
    {
        var mods = new StatModifiers();
        BonusStat.Parse("damage_pct").Apply(mods, 12);

        Assert.Equal(0.12, mods.DamagePct, 6);
    }

    [Fact]
    public void FlatStats_AreStoredAsWritten()
    {
        var mods = new StatModifiers();
        BonusStat.Parse("max_hp_flat").Apply(mods, 260);

        Assert.Equal(260, mods.MaxHpFlat);
    }

    [Fact]
    public void ResistAll_TouchesEveryElement()
    {
        var mods = new StatModifiers();
        BonusStat.Parse("resist_all").Apply(mods, 5);

        foreach (var element in Enum.GetValues<DamageElement>())
        {
            Assert.Equal(0.05, mods.ResistTo(element), 6);
        }
    }

    [Fact]
    public void Describe_ReadsAsPlainEnglish()
    {
        Assert.Equal("+12% damage", BonusStat.Parse("damage_pct").Describe(12));
        Assert.Equal("+260 maximum health", BonusStat.Parse("max_hp_flat").Describe(260));
        Assert.Equal("+25% damage against undeads", BonusStat.Parse("vs_family.undead").Describe(25));
    }
}

public class ItemFactoryTests
{
    [Fact]
    public void SameSeed_RollsTheSameItem()
    {
        // Loot has to be reproducible from the save seed, or reloading for a better roll
        // becomes the optimal play and the no-gambling design is decorative.
        var specs = TestSpecs.Standard();
        var a = new ItemFactory(specs, specs).Create("wpn_test_sword", new DeterministicRng(42));
        var b = new ItemFactory(specs, specs).Create("wpn_test_sword", new DeterministicRng(42));

        Assert.Equal(
            a.Bonuses.Select(l => (l.LineId, l.Magnitude)),
            b.Bonuses.Select(l => (l.LineId, l.Magnitude)));
    }

    [Fact]
    public void RolledLines_AreDistinctAndInRange()
    {
        var specs = TestSpecs.Standard();
        var factory = new ItemFactory(specs, specs);
        var pool = specs.Pool("bonus_test");

        for (var seed = 0; seed < 200; seed++)
        {
            var item = factory.Create("wpn_test_sword", new DeterministicRng((ulong)seed));

            Assert.Equal(3, item.Bonuses.Count);
            Assert.Equal(3, item.Bonuses.Select(l => l.LineId).Distinct().Count());

            foreach (var line in item.Bonuses)
            {
                var entry = pool.Find(line.LineId)!;
                Assert.InRange(line.Magnitude, entry.Min, entry.Max);
            }
        }
    }

    [Fact]
    public void SocketSlots_ComeFromTheSpecAndStartClosed()
    {
        var specs = TestSpecs.Standard();
        var item = new ItemFactory(specs, specs).Create("wpn_test_sword", new DeterministicRng(1));

        Assert.Equal(2, item.Sockets.Count);
        Assert.Equal(0, item.OpenSockets);
    }

    [Fact]
    public void Uids_AreUnique()
    {
        var specs = TestSpecs.Standard();
        var factory = new ItemFactory(specs, specs);
        var rng = new DeterministicRng(1);

        var uids = Enumerable.Range(0, 50).Select(_ => factory.Create("wpn_test_sword", rng).Uid).ToList();

        Assert.Equal(50, uids.Distinct().Count());
    }
}

public class SocketBenchTests
{
    private static (ItemInstance Item, Inventory Bag, TestSpecs Specs) Setup(long yang = 100_000)
    {
        var specs = TestSpecs.Standard();
        var bag = new Inventory(specs, 10, 8, yang);
        var factory = new ItemFactory(specs, specs);

        bag.TryAdd(factory.CreatePlain("mat_boring_stone", 5));
        bag.TryAdd(factory.CreatePlain("stn_test_ember", 3));

        var item = factory.Create("wpn_test_sword", new DeterministicRng(1));

        return (item, bag, specs);
    }

    [Fact]
    public void Boring_OpensOneSocketPerStone()
    {
        var (item, bag, _) = Setup();

        Assert.Equal(SocketOutcome.Success, SocketBench.TryBore(item, bag));
        Assert.Equal(1, item.OpenSockets);
        Assert.Equal(4, bag.CountOf("mat_boring_stone"));

        Assert.Equal(SocketOutcome.Success, SocketBench.TryBore(item, bag));
        Assert.Equal(SocketOutcome.NoSocketsLeft, SocketBench.TryBore(item, bag));
    }

    [Fact]
    public void Boring_WithoutAStone_ChangesNothing()
    {
        var specs = TestSpecs.Standard();
        var bag = new Inventory(specs, 10, 8, 100_000);
        var item = new ItemFactory(specs, specs).Create("wpn_test_sword", new DeterministicRng(1));

        Assert.Equal(SocketOutcome.CannotAfford, SocketBench.TryBore(item, bag));
        Assert.Equal(0, item.OpenSockets);
    }

    [Fact]
    public void Stones_SlotIntoOpenSocketsAndContributeStats()
    {
        var (item, bag, specs) = Setup();
        SocketBench.TryBore(item, bag);

        Assert.Equal(SocketOutcome.Success, SocketBench.TrySlot(item, 0, "stn_test_ember", specs, bag));
        Assert.Equal(1, item.FilledSockets);
        Assert.Equal(2, bag.CountOf("stn_test_ember"));

        var withStone = item.ModifiersFor(specs.Get("wpn_test_sword"), specs).DamagePct;

        SocketBench.TryRemove(item, 0, bag);

        Assert.True(withStone > item.ModifiersFor(specs.Get("wpn_test_sword"), specs).DamagePct);
    }

    [Fact]
    public void Stones_CannotGoIntoAClosedSocket()
    {
        var (item, bag, specs) = Setup();

        Assert.Equal(SocketOutcome.SocketClosed, SocketBench.TrySlot(item, 0, "stn_test_ember", specs, bag));
    }

    [Fact]
    public void RemovingAStone_ReturnsItIntact()
    {
        // The whole reason removal exists: a system that destroys what you take out is a
        // system where nobody ever tries a different stone.
        var (item, bag, specs) = Setup();
        SocketBench.TryBore(item, bag);
        SocketBench.TrySlot(item, 0, "stn_test_ember", specs, bag);

        Assert.Equal(2, bag.CountOf("stn_test_ember"));
        Assert.Equal(SocketOutcome.Success, SocketBench.TryRemove(item, 0, bag));
        Assert.Equal(3, bag.CountOf("stn_test_ember"));
        Assert.Equal(0, item.FilledSockets);
        Assert.Equal(1, item.OpenSockets);
    }

    [Fact]
    public void Equipment_IsNotAStone()
    {
        var (item, bag, specs) = Setup();
        SocketBench.TryBore(item, bag);

        Assert.Equal(SocketOutcome.NotAStone, SocketBench.TrySlot(item, 0, "arm_test_vest", specs, bag));
    }
}

public class RerollTests
{
    private static (ItemInstance Item, Inventory Bag, TestSpecs Specs) Setup(long yang = 1_000_000, int ink = 20)
    {
        var specs = TestSpecs.Standard();
        var bag = new Inventory(specs, 10, 8, yang);
        var factory = new ItemFactory(specs, specs);

        if (ink > 0) bag.TryAdd(factory.CreatePlain("mat_mutation_ink", ink));

        return (factory.Create("wpn_test_sword", new DeterministicRng(11)), bag, specs);
    }

    [Fact]
    public void Reroll_ChangesTheLinesButNotHowMany()
    {
        var (item, bag, specs) = Setup();
        var before = item.Bonuses.Select(l => (l.LineId, l.Magnitude)).ToList();

        Assert.Equal(RerollOutcome.Success, RerollTable.TryReroll(item, specs.Get("wpn_test_sword"), specs.Pool("bonus_test"), bag, new DeterministicRng(77)));

        Assert.Equal(before.Count, item.Bonuses.Count);
        Assert.NotEqual(before, item.Bonuses.Select(l => (l.LineId, l.Magnitude)).ToList());
    }

    [Fact]
    public void LockedLines_SurviveTheReroll()
    {
        // This is what turns rerolling from a slot machine into a process that converges.
        var (item, bag, specs) = Setup();
        item.SetLineLocked(0, true);

        var kept = item.Bonuses[0];

        for (var i = 0; i < 5; i++)
        {
            RerollTable.TryReroll(item, specs.Get("wpn_test_sword"), specs.Pool("bonus_test"), bag, new DeterministicRng((ulong)i));

            Assert.Contains(item.Bonuses, l => l.LineId == kept.LineId && l.Magnitude == kept.Magnitude && l.Locked);
        }
    }

    [Fact]
    public void LockingCostsMore()
    {
        var (item, bag, specs) = Setup();
        var spec = specs.Get("wpn_test_sword");

        var plain = RerollTable.Quote(item, spec, bag);
        item.SetLineLocked(0, true);
        var locked = RerollTable.Quote(item, spec, bag);

        Assert.True(locked.Yang > plain.Yang);
        Assert.True(locked.Materials["mat_mutation_ink"] > plain.Materials["mat_mutation_ink"]);
    }

    [Fact]
    public void HighRarityItems_MayLockTwoLines()
    {
        var specs = TestSpecs.Standard();
        var rare = specs.Get("wpn_test_sword");
        var epic = rare with { Rarity = Rarity.Epic };

        Assert.Equal(1, RerollTable.MaxLockedLines(rare));
        Assert.Equal(2, RerollTable.MaxLockedLines(epic));
    }

    [Fact]
    public void LockingEverything_IsRefusedRatherThanCharged()
    {
        var (item, bag, specs) = Setup();

        for (var i = 0; i < item.Bonuses.Count; i++) item.SetLineLocked(i, true);

        var yangBefore = bag.Yang;
        var outcome = RerollTable.TryReroll(item, specs.Get("wpn_test_sword"), specs.Pool("bonus_test"), bag, new DeterministicRng(1));

        Assert.NotEqual(RerollOutcome.Success, outcome);
        Assert.Equal(yangBefore, bag.Yang);
    }

    [Fact]
    public void WithoutInk_NothingHappens()
    {
        var (item, bag, specs) = Setup(ink: 0);
        var before = item.Bonuses.ToList();

        Assert.Equal(RerollOutcome.CannotAfford, RerollTable.TryReroll(item, specs.Get("wpn_test_sword"), specs.Pool("bonus_test"), bag, new DeterministicRng(1)));
        Assert.Equal(before, item.Bonuses.ToList());
    }
}

public class EquipmentTests
{
    [Fact]
    public void Equipping_RespectsLevelAndClass()
    {
        var specs = TestSpecs.Standard();
        var gear = new Equipment(specs);
        var sword = new ItemFactory(specs, specs).Create("wpn_test_sword", new DeterministicRng(1));

        Assert.Equal(EquipOutcome.LevelTooLow, gear.TryEquip(sword, 5, CharacterClass.Warrior, out _));
        Assert.Equal(EquipOutcome.WrongClass, gear.TryEquip(sword, 20, CharacterClass.Shaman, out _));
        Assert.Equal(EquipOutcome.Equipped, gear.TryEquip(sword, 20, CharacterClass.Warrior, out _));
    }

    [Fact]
    public void Equipping_HandsBackWhatItReplaced()
    {
        var specs = TestSpecs.Standard();
        var gear = new Equipment(specs);
        var factory = new ItemFactory(specs, specs);
        var first = factory.Create("wpn_test_sword", new DeterministicRng(1));
        var second = factory.Create("wpn_test_sword", new DeterministicRng(2));

        gear.TryEquip(first, 20, CharacterClass.Warrior, out _);
        gear.TryEquip(second, 20, CharacterClass.Warrior, out var displaced);

        Assert.Same(first, displaced);
        Assert.Same(second, gear.In(EquipSlot.Weapon));
    }

    [Fact]
    public void Unequipping_LeavesNoStatsBehind()
    {
        // The classic gear bug: a modifier that outlives the item it came from.
        var specs = TestSpecs.Standard();
        var gear = new Equipment(specs);
        var sword = new ItemFactory(specs, specs).Create("wpn_test_sword", new DeterministicRng(1));
        var stats = new StatBlock();

        gear.TryEquip(sword, 20, CharacterClass.Warrior, out _);
        gear.ApplyTo(stats);

        Assert.True(stats.WeaponDamage > 0);

        gear.Unequip(EquipSlot.Weapon);
        gear.ApplyTo(stats);

        Assert.Equal(0, stats.WeaponDamage);
        Assert.Equal(0, stats.Modifiers.DamagePct);
    }

    [Fact]
    public void ApplyTo_KeepsNonGearModifiers()
    {
        var specs = TestSpecs.Standard();
        var gear = new Equipment(specs);
        var stats = new StatBlock();

        var buff = new StatModifiers { DamagePct = 0.25 };
        gear.ApplyTo(stats, buff);

        Assert.Equal(0.25, stats.Modifiers.DamagePct, 6);

        // And the caller's own copy must not have been mutated by the aggregation.
        Assert.Equal(0.25, buff.DamagePct, 6);
    }

    [Fact]
    public void UpgradeLevel_FlowsThroughToDerivedStats()
    {
        var specs = TestSpecs.Standard();
        var gear = new Equipment(specs);
        var sword = new ItemFactory(specs, specs).Create("wpn_test_sword", new DeterministicRng(1));
        var stats = new StatBlock();

        gear.TryEquip(sword, 20, CharacterClass.Warrior, out _);
        gear.ApplyTo(stats);

        var before = stats.AttackPower;

        sword.UpgradeLevel = 9;
        gear.ApplyTo(stats);

        Assert.True(stats.AttackPower > before);
    }

    [Fact]
    public void ArmorValue_SumsEveryWornPiece()
    {
        var specs = TestSpecs.Standard();
        var gear = new Equipment(specs);
        var vest = new ItemFactory(specs, specs).CreatePlain("arm_test_vest");

        gear.TryEquip(vest, 20, CharacterClass.Warrior, out _);

        Assert.Equal(30, gear.ArmorValue());
    }
}
