using Kiln.Core.Combat;
using Kiln.Core.Foundation;
using Kiln.Core.Items;
using Xunit;

namespace Kiln.Tests.Items;

/// <summary>The stat a copy is born with (2026-10-02): a ring's damage, a bracelet's attribute.</summary>
public class InnateTests
{
    private static readonly ItemSpec Ring = new()
    {
        Id = "rng_born",
        Slot = EquipSlot.Ring1,
        Innate = new ItemInnate([BonusStat.Parse("damage_pct"), BonusStat.Parse("skill_damage_pct")], 5, []),
    };

    private static readonly ItemSpec Bracelet = new()
    {
        Id = "brc_born",
        Slot = EquipSlot.Bracelet,
        Innate = new ItemInnate(
            [BonusStat.Parse("vitality"), BonusStat.Parse("intelligence"), BonusStat.Parse("dexterity"), BonusStat.Parse("strength")],
            0, [3, 4, 5, 6, 7, 8, 9, 10, 12, 14]),
    };

    [Fact]
    public void ACopyKeepsItsStat_AndCopiesDiffer()
    {
        var kinds = new HashSet<BonusStatKind>();

        for (var uid = 1; uid <= 40; uid++)
        {
            var ring = new ItemInstance(uid, Ring.Id);
            var born = ring.InnateFor(Ring)!.Value.Stat.Kind;

            ring.UpgradeLevel = 9;

            Assert.Equal(born, ring.InnateFor(Ring)!.Value.Stat.Kind);
            kinds.Add(born);
        }

        Assert.Equal(2, kinds.Count);
    }

    [Fact]
    public void EveryAttributeTurnsUp()
    {
        var kinds = Enumerable.Range(1, 80).Select(uid => new ItemInstance(uid, Bracelet.Id).InnateFor(Bracelet)!.Value.Stat.Kind).ToHashSet();

        Assert.Equal(4, kinds.Count);
    }

    [Fact]
    public void EveryAttributeTurnsUp_HoweverFarApartTheCopiesAre()
    {
        // 2026-10-06: twenty earrings and not one of intelligence — copies a fixed number of uids
        // apart were all born the same. Forty must show all four, whatever the gap.
        for (var gap = 1; gap <= 64; gap++)
        {
            var kinds = Enumerable.Range(0, 40)
                .Select(i => new ItemInstance(2000 + (i * gap), Bracelet.Id).InnateFor(Bracelet)!.Value.Stat.Kind)
                .ToHashSet();

            Assert.True(kinds.Count == 4, $"gap {gap}: only {kinds.Count} kinds");
        }
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(5, 8)]
    [InlineData(9, 14)]
    public void ALadderIsReadByUpgradeLevel(int level, double worth)
    {
        var bracelet = new ItemInstance(7, Bracelet.Id) { UpgradeLevel = level };

        Assert.Equal(worth, bracelet.InnateFor(Bracelet)!.Value.Magnitude);
    }

    [Fact]
    public void AMagnitudeGrowsAsBaseStatsDo()
    {
        var ring = new ItemInstance(7, Ring.Id) { UpgradeLevel = 9 };

        Assert.Equal(5 * UpgradeScaling.Multiplier(9), ring.InnateFor(Ring)!.Value.Magnitude, 6);
    }

    [Fact]
    public void AnAttributeWornCountsAsPointsPlaced()
    {
        var bracelet = new ItemInstance(7, Bracelet.Id) { UpgradeLevel = 9 };
        var mods = bracelet.ModifiersFor(Bracelet, new TestSpecs());
        var raised = mods.Raise(new Attributes(6, 6, 6, 6));

        Assert.Equal(24 + 14, raised.Str + raised.Dex + raised.Int + raised.Vit);
    }

    [Fact]
    public void AnItemWithoutOneHasNone()
    {
        Assert.Null(new ItemInstance(1, "x").InnateFor(new ItemSpec { Id = "x" }));
    }
}
