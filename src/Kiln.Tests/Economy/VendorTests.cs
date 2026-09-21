using Kiln.Core.Economy;
using Kiln.Core.Foundation;
using Kiln.Core.Items;
using Kiln.Tests.Items;
using Xunit;

namespace Kiln.Tests.Economy;

public class VendorTests
{
    private static TestSpecs Specs()
    {
        var specs = new TestSpecs();

        specs.Add(new ItemSpec { Id = "mat_scrap", MaxStack = 20, SellValue = 40 });
        specs.Add(new ItemSpec { Id = "mat_dust", MaxStack = 1, SellValue = 0 });

        // Gear from level 1 to 20, one slot each, all within the fine cap except the last two.
        for (var level = 1; level <= 20; level += 2)
        {
            specs.Add(new ItemSpec
            {
                Id = $"wpn_blade_{level}",
                Slot = EquipSlot.Weapon,
                Rarity = level >= 17 ? Rarity.Rare : Rarity.Fine,
                LevelReq = level,
                Height = 2,
                SellValue = 100 * level,
            });
        }

        return specs;
    }

    private static (Vendor Vendor, Inventory Bag, ItemFactory Factory) Setup(long yang = 10_000, int w = 6, int h = 4)
    {
        var specs = Specs();
        var gear = Enumerable.Range(0, 10).Select(i => specs.Get($"wpn_blade_{1 + (2 * i)}"));
        var vendor = new Vendor("npc_test", specs, ["mat_scrap", "mat_missing"], gear, rotating: 3);

        return (vendor, new Inventory(specs, w, h, yang), new ItemFactory(specs, specs));
    }

    [Fact]
    public void Staples_SkipIdsThatDoNotExist()
    {
        var (vendor, _, _) = Setup();

        Assert.Equal(["mat_scrap"], vendor.Staples.Select(s => s.ItemId));
        Assert.Equal(40, vendor.Staples[0].Price);
    }

    [Fact]
    public void Shelf_IsTheSameForTheSameSeedAndLevel()
    {
        var (a, _, fa) = Setup();
        var (b, _, fb) = Setup();

        a.Restock(9, 42, fa);
        b.Restock(9, 42, fb);

        Assert.Equal(a.Shelf.Select(o => o.ItemId), b.Shelf.Select(o => o.ItemId));
        Assert.Equal(3, a.Shelf.Count);
    }

    [Fact]
    public void Shelf_StaysNearThePlayersLevelAndUnderTheRarityCap()
    {
        var (vendor, _, factory) = Setup();

        foreach (var level in new[] { 1, 6, 12, 20 })
        {
            vendor.Restock(level, 7, factory);

            foreach (var offer in vendor.Shelf)
            {
                var req = int.Parse(offer.ItemId.Split('_')[^1]);

                Assert.InRange(req, level - Vendor.LevelsBelow, level + Vendor.LevelsAbove);
                Assert.True(req < 17, $"{offer.ItemId} is above the fine cap");
            }
        }
    }

    [Fact]
    public void Shelf_ChangesOnLevelUpButNotOnARevisit()
    {
        var (vendor, bag, factory) = Setup();

        vendor.Restock(9, 42, factory);
        var first = vendor.Shelf[0];

        Assert.Equal(TradeResult.Done, vendor.Buy(first, bag, factory));

        vendor.Restock(9, 42, factory);
        Assert.DoesNotContain(first, vendor.Shelf);
        Assert.Equal(2, vendor.Shelf.Count);

        vendor.Restock(10, 42, factory);
        Assert.Equal(3, vendor.Shelf.Count);
    }

    [Fact]
    public void BuyingAStaple_ChargesPerUnitAndStacks()
    {
        var (vendor, bag, factory) = Setup(yang: 1_000);

        Assert.Equal(TradeResult.Done, vendor.Buy(vendor.Staples[0], bag, factory, 10));
        Assert.Equal(TradeResult.Done, vendor.Buy(vendor.Staples[0], bag, factory, 5));

        Assert.Equal(1_000 - (15 * 40), bag.Yang);
        Assert.Equal(15, bag.CountOf("mat_scrap"));
        Assert.Single(bag.Items);
    }

    [Fact]
    public void Buying_RefusesWhenPoorAndTakesNothing()
    {
        var (vendor, bag, factory) = Setup(yang: 100);

        Assert.Equal(TradeResult.TooPoor, vendor.Buy(vendor.Staples[0], bag, factory, 3));
        Assert.Equal(100, bag.Yang);
        Assert.Empty(bag.Items);
    }

    [Fact]
    public void Buying_RefusesWhenTheBagIsFullAndKeepsTheYang()
    {
        var (vendor, bag, factory) = Setup(yang: 10_000, w: 1, h: 1);

        Assert.True(bag.TryAdd(factory.CreatePlain("mat_dust")));

        Assert.Equal(TradeResult.NoRoom, vendor.Buy(vendor.Staples[0], bag, factory));
        Assert.Equal(10_000, bag.Yang);
    }

    [Fact]
    public void Buying_TopsUpStacksOnlyWhenTheRestFits()
    {
        var (vendor, bag, factory) = Setup(yang: 10_000, w: 1, h: 1);

        Assert.Equal(TradeResult.Done, vendor.Buy(vendor.Staples[0], bag, factory, 18));

        // Two more fit in the stack; three would need a second cell the bag does not have.
        Assert.Equal(TradeResult.NoRoom, vendor.Buy(vendor.Staples[0], bag, factory, 3));
        Assert.Equal(18, bag.CountOf("mat_scrap"));
        Assert.Equal(TradeResult.Done, vendor.Buy(vendor.Staples[0], bag, factory, 2));
        Assert.Equal(20, bag.CountOf("mat_scrap"));
    }

    [Fact]
    public void Selling_PaysAQuarterForTheWholeStack()
    {
        var (vendor, bag, factory) = Setup(yang: 0);
        var scrap = factory.CreatePlain("mat_scrap", 8);

        bag.TryAdd(scrap);

        Assert.Equal(TradeResult.Done, vendor.Sell(scrap, bag));
        Assert.Equal(8 * 10, bag.Yang);
        Assert.Empty(bag.Items);
    }

    [Fact]
    public void Selling_RefusesLockedAndWorthlessItems()
    {
        var (vendor, bag, factory) = Setup(yang: 0);
        var sword = factory.CreatePlain("wpn_blade_5");
        var dust = factory.CreatePlain("mat_dust");

        bag.TryAdd(sword);
        bag.TryAdd(dust);
        sword.Locked = true;

        Assert.Equal(TradeResult.Locked, vendor.Sell(sword, bag));
        Assert.Equal(TradeResult.Worthless, vendor.Sell(dust, bag));
        Assert.Equal(2, bag.Items.Count);
        Assert.Equal(0, bag.Yang);
    }

    [Fact]
    public void Buyback_ReturnsTheSameItemForWhatItFetched()
    {
        var (vendor, bag, factory) = Setup(yang: 0);
        var sword = factory.CreatePlain("wpn_blade_9");

        bag.TryAdd(sword);
        vendor.Sell(sword, bag);

        var offer = Assert.Single(vendor.Buyback);
        Assert.Equal(225, offer.Price);

        Assert.Equal(TradeResult.Done, vendor.Buy(offer, bag, factory));
        Assert.Same(sword, bag.Items.Single().Item);
        Assert.Equal(0, bag.Yang);
        Assert.Empty(vendor.Buyback);
    }

    [Fact]
    public void Buyback_KeepsOnlyTheMostRecentSales()
    {
        var (vendor, bag, factory) = Setup(yang: 0, w: 10, h: 4);

        for (var i = 0; i < Vendor.BuybackSlots + 2; i++)
        {
            var scrap = factory.CreatePlain("mat_scrap", i + 1);

            bag.TryPlace(scrap, i, 0);
            vendor.Sell(scrap, bag);
        }

        Assert.Equal(Vendor.BuybackSlots, vendor.Buyback.Count);
        Assert.Equal(Vendor.BuybackSlots + 2, vendor.Buyback[0].Item!.Count);
    }

    [Fact]
    public void AnOfferNoLongerOnTheShelf_CannotBeBoughtTwice()
    {
        var (vendor, bag, factory) = Setup();

        vendor.Restock(9, 42, factory);
        var offer = vendor.Shelf[0];

        Assert.Equal(TradeResult.Done, vendor.Buy(offer, bag, factory));
        Assert.Equal(TradeResult.Gone, vendor.Buy(offer, bag, factory));
    }

    [Fact]
    public void Restore_PutsBackTheSameShelfMinusWhatWasBought()
    {
        var (before, bag, factory) = Setup();

        before.Restock(9, 42, factory);
        var shelf = before.Shelf.Select(o => o.ItemId).ToList();
        before.Buy(before.Shelf[1], bag, factory);

        var sword = factory.CreatePlain("wpn_blade_3");
        bag.TryAdd(sword);
        before.Sell(sword, bag);

        var (after, _, factory2) = Setup();
        after.Restore(before.StockedFor, 42, factory2, before.Bought, before.Buyback.Select(o => (o.Item!, o.Price)));

        Assert.Equal(shelf.Where((_, i) => i != 1), after.Shelf.Select(o => o.ItemId));
        Assert.Equal(before.Bought, after.Bought);
        Assert.Same(sword, Assert.Single(after.Buyback).Item);
        Assert.Equal(before.Buyback[0].Price, after.Buyback[0].Price);

        // Still the same shelf: a restore is not a restock.
        after.Restock(9, 42, factory2);
        Assert.Equal(2, after.Shelf.Count);
    }

    [Fact]
    public void BuyingBack_IsNotABuyFromTheShelf()
    {
        var (vendor, bag, factory) = Setup(yang: 0);
        var sword = factory.CreatePlain("wpn_blade_9");

        bag.TryAdd(sword);
        vendor.Sell(sword, bag);
        vendor.Buy(vendor.Buyback[0], bag, factory);

        Assert.Empty(vendor.Bought);
    }
}
