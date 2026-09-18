using Kiln.Core.Foundation;
using Kiln.Core.Items;
using Xunit;

namespace Kiln.Tests.Items;

public class InventoryTests
{
    private static (Inventory Bag, ItemFactory Factory) Setup(int w = 6, int h = 4, long yang = 0)
    {
        var specs = TestSpecs.Standard();
        return (new Inventory(specs, w, h, yang), new ItemFactory(specs, specs));
    }

    [Fact]
    public void MultiCellItems_OccupyTheirWholeFootprint()
    {
        var (bag, factory) = Setup();
        var vest = factory.CreatePlain("arm_test_vest");

        Assert.True(bag.TryPlace(vest, 0, 0));

        // 2x3 starting at the origin.
        Assert.NotNull(bag.At(1, 2));
        Assert.Null(bag.At(2, 0));
        Assert.Equal(6, bag.UsedCells);
    }

    [Fact]
    public void Placement_RefusesOverlapAndOutOfBounds()
    {
        var (bag, factory) = Setup();

        Assert.True(bag.TryPlace(factory.CreatePlain("arm_test_vest"), 0, 0));
        Assert.False(bag.TryPlace(factory.CreatePlain("arm_test_vest"), 1, 1));
        Assert.False(bag.TryPlace(factory.CreatePlain("arm_test_vest"), 5, 3));
    }

    [Fact]
    public void TryAdd_FindsTheFirstFreeSpot()
    {
        var (bag, factory) = Setup();

        Assert.True(bag.TryAdd(factory.CreatePlain("arm_test_vest")));
        Assert.True(bag.TryAdd(factory.CreatePlain("arm_test_vest")));
        Assert.True(bag.TryAdd(factory.CreatePlain("arm_test_vest")));

        // Three 2x3 vests exactly fill a 6x4... no: 6x4 holds three 2x3 in a row, 18 of 24 cells.
        Assert.Equal(18, bag.UsedCells);
        Assert.False(bag.TryAdd(factory.CreatePlain("arm_test_vest")));
    }

    [Fact]
    public void TryAdd_ReportsFailureRatherThanSwallowingTheItem()
    {
        // The caller needs the false so it can leave the loot on the ground instead of
        // deleting it, which is the behaviour the original is notorious for.
        var (bag, factory) = Setup(1, 1);

        Assert.True(bag.TryAdd(factory.CreatePlain("mat_test_scrap")));
        Assert.False(bag.TryAdd(factory.CreatePlain("arm_test_vest")));
    }

    [Fact]
    public void Stackables_MergeUpToTheStackLimit()
    {
        var (bag, factory) = Setup();

        bag.TryAdd(factory.CreatePlain("mat_test_scrap", 60));
        bag.TryAdd(factory.CreatePlain("mat_test_scrap", 60));

        Assert.Equal(120, bag.CountOf("mat_test_scrap"));
        Assert.Equal(2, bag.Items.Count);
    }

    [Fact]
    public void Equipment_NeverStacks()
    {
        var (bag, factory) = Setup();

        bag.TryAdd(factory.Create("wpn_test_sword", new DeterministicRng(1)));
        bag.TryAdd(factory.Create("wpn_test_sword", new DeterministicRng(2)));

        Assert.Equal(2, bag.Items.Count);
    }

    [Fact]
    public void TryMove_TreatsTheItemsOwnCellsAsFree()
    {
        // Sliding a 2x3 vest one cell across must not collide with itself.
        var (bag, factory) = Setup();
        var vest = factory.CreatePlain("arm_test_vest");

        bag.TryPlace(vest, 0, 0);

        Assert.True(bag.TryMove(vest, 1, 0));
        Assert.Equal(1, bag.Find(vest)!.X);
    }

    [Fact]
    public void TryMove_LeavesTheItemWhereItWasWhenBlocked()
    {
        var (bag, factory) = Setup();
        var vest = factory.CreatePlain("arm_test_vest");

        bag.TryPlace(vest, 0, 0);
        bag.TryPlace(factory.CreatePlain("arm_test_vest"), 2, 0);

        Assert.False(bag.TryMove(vest, 2, 0));
        Assert.Equal(0, bag.Find(vest)!.X);
        Assert.Equal(12, bag.UsedCells);
    }

    [Fact]
    public void Spending_IsAtomic()
    {
        var (bag, factory) = Setup(yang: 500);
        bag.TryAdd(factory.CreatePlain("mat_test_scrap", 3));

        var tooMuch = new Dictionary<string, int> { ["mat_test_scrap"] = 5 };

        Assert.False(bag.TrySpend(100, tooMuch));
        Assert.Equal(500, bag.Yang);
        Assert.Equal(3, bag.CountOf("mat_test_scrap"));
    }

    [Fact]
    public void Spending_DrainsStacksAndFreesEmptyCells()
    {
        var (bag, factory) = Setup(yang: 500);
        bag.TryAdd(factory.CreatePlain("mat_test_scrap", 99));
        bag.TryAdd(factory.CreatePlain("mat_test_scrap", 99));

        Assert.True(bag.TrySpend(100, new Dictionary<string, int> { ["mat_test_scrap"] = 150 }));
        Assert.Equal(48, bag.CountOf("mat_test_scrap"));
        Assert.Single(bag.Items);
        Assert.Equal(400, bag.Yang);
    }

    [Fact]
    public void AutoSort_MergesPartialStacksAndKeepsEverything()
    {
        var (bag, factory) = Setup(8, 6);

        bag.TryAdd(factory.Create("wpn_test_sword", new DeterministicRng(1)));
        bag.TryPlace(factory.CreatePlain("mat_test_scrap", 10), 4, 4);
        bag.TryPlace(factory.CreatePlain("mat_test_scrap", 10), 7, 0);
        bag.TryAdd(factory.CreatePlain("arm_test_vest"));

        bag.AutoSort();

        Assert.Equal(20, bag.CountOf("mat_test_scrap"));
        Assert.Equal(1, bag.Items.Count(p => p.Item.DefId == "mat_test_scrap"));
        Assert.Equal(1, bag.Items.Count(p => p.Item.DefId == "wpn_test_sword"));
        Assert.Equal(1, bag.Items.Count(p => p.Item.DefId == "arm_test_vest"));
    }

    [Fact]
    public void AutoSort_PutsEquipmentBeforeMaterials()
    {
        var (bag, factory) = Setup(8, 6);

        bag.TryPlace(factory.CreatePlain("mat_test_scrap", 5), 0, 0);
        bag.TryAdd(factory.Create("wpn_test_sword", new DeterministicRng(1)));

        bag.AutoSort();

        var first = bag.Items.OrderBy(p => p.Y).ThenBy(p => p.X).First();

        Assert.Equal("wpn_test_sword", first.Item.DefId);
    }
}
