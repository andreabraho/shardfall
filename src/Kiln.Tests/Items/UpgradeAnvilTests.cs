using Kiln.Core.Foundation;
using Kiln.Core.Items;
using Xunit;

namespace Kiln.Tests.Items;

/// <summary>
/// The no-gambling ladder (FR-5.5, decision D7). These are the tests that guard the promise
/// the whole itemisation redesign is built on, so they are written as statements about what
/// the player is owed rather than about the implementation.
/// </summary>
public class UpgradeAnvilTests
{
    private static (ItemInstance Item, Inventory Bag, UpgradeLadder Ladder) Setup(long yang = 1_000_000, int scrap = 500)
    {
        var specs = TestSpecs.Standard();
        var bag = new Inventory(specs, 10, 8, yang);
        var factory = new ItemFactory(specs, specs);

        bag.TryAdd(factory.CreatePlain("mat_test_scrap", scrap));

        var item = factory.Create("wpn_test_sword", new DeterministicRng(1));
        bag.TryAdd(item);

        return (item, bag, TestSpecs.Ladder());
    }

    [Fact]
    public void Failure_NeverDestroysAndNeverDowngrades()
    {
        // The single most important guarantee in the game. Run the fallible rungs many times
        // with an unlucky stream and assert the level only ever goes up.
        var (item, bag, ladder) = Setup();
        var rng = new DeterministicRng(99);

        UpgradeAnvil.Attempt(item, ladder, bag, rng);
        UpgradeAnvil.Attempt(item, ladder, bag, rng);

        var highest = item.UpgradeLevel;

        for (var i = 0; i < 200; i++)
        {
            var before = item.UpgradeLevel;
            var result = UpgradeAnvil.Attempt(item, ladder, bag, rng);

            Assert.True(item.UpgradeLevel >= before, "an upgrade attempt lowered the item's level");
            Assert.NotEqual(UpgradeOutcome.NoLadder, result.Outcome);

            highest = Math.Max(highest, item.UpgradeLevel);
        }

        Assert.Equal(ladder.MaxLevel, highest);
    }

    [Fact]
    public void Pity_GuaranteesTheNextAttemptAndIsVisibleBeforehand()
    {
        var (item, bag, ladder) = Setup();
        var rng = new DeterministicRng(7);

        // Walk up to the first fallible rung.
        UpgradeAnvil.Attempt(item, ladder, bag, rng);
        UpgradeAnvil.Attempt(item, ladder, bag, rng);
        Assert.Equal(2, item.UpgradeLevel);

        var step = ladder.StepTo(3)!;

        // Force the counter to the brink without relying on luck.
        item.UpgradeFailures = step.Pity;

        var quote = UpgradeAnvil.Quote(item, ladder, bag)!;

        Assert.True(quote.Guaranteed);
        Assert.Equal(1.0, quote.DisplayedChance);
        Assert.Equal(0, quote.AttemptsToGuarantee);

        var result = UpgradeAnvil.Attempt(item, ladder, bag, rng);

        Assert.Equal(UpgradeOutcome.Success, result.Outcome);
        Assert.True(result.WasGuaranteed);
        Assert.Equal(3, item.UpgradeLevel);
    }

    [Fact]
    public void Quote_CountsDownTowardsTheGuarantee()
    {
        // The counter is shown in the UI, so the player can always see the ladder converging.
        var (item, bag, ladder) = Setup();
        item.UpgradeLevel = 2;

        var step = ladder.StepTo(3)!;

        Assert.Equal(step.Pity, UpgradeAnvil.Quote(item, ladder, bag)!.AttemptsToGuarantee);

        item.UpgradeFailures = 1;

        Assert.Equal(step.Pity - 1, UpgradeAnvil.Quote(item, ladder, bag)!.AttemptsToGuarantee);
        Assert.False(UpgradeAnvil.Quote(item, ladder, bag)!.Guaranteed);
    }

    [Fact]
    public void Success_ResetsThePityCounter()
    {
        var (item, bag, ladder) = Setup();
        item.UpgradeLevel = 2;
        item.UpgradeFailures = 1;

        UpgradeAnvil.Attempt(item, ladder, bag, new DeterministicRng(3));

        if (item.UpgradeLevel == 3) Assert.Equal(0, item.UpgradeFailures);
        else Assert.Equal(2, item.UpgradeFailures);
    }

    [Fact]
    public void Attempt_TakesPaymentOnlyWhenItProceeds()
    {
        var specs = TestSpecs.Standard();
        var bag = new Inventory(specs, 10, 8, yang: 50);
        var factory = new ItemFactory(specs, specs);
        var item = factory.Create("wpn_test_sword", new DeterministicRng(1));

        var result = UpgradeAnvil.Attempt(item, TestSpecs.Ladder(), bag, new DeterministicRng(1));

        Assert.Equal(UpgradeOutcome.CannotAfford, result.Outcome);
        Assert.Equal(50, bag.Yang);
        Assert.Equal(0, item.UpgradeLevel);
    }

    [Fact]
    public void Failure_StillConsumesTheMaterials()
    {
        // Risk has to cost something, or the ladder is just a slow walk.
        var (item, bag, ladder) = Setup();
        item.UpgradeLevel = 3;

        var before = bag.CountOf("mat_test_scrap");
        var yangBefore = bag.Yang;

        var result = UpgradeAnvil.Attempt(item, ladder, bag, new DeterministicRng(5));

        Assert.True(result.Attempted);
        Assert.True(bag.CountOf("mat_test_scrap") < before);
        Assert.True(bag.Yang < yangBefore);
    }

    [Fact]
    public void AtMaxLevel_NothingIsSpent()
    {
        var (item, bag, ladder) = Setup();
        item.UpgradeLevel = ladder.MaxLevel;

        var yangBefore = bag.Yang;
        var result = UpgradeAnvil.Attempt(item, ladder, bag, new DeterministicRng(1));

        Assert.Equal(UpgradeOutcome.AtMaxLevel, result.Outcome);
        Assert.Equal(yangBefore, bag.Yang);
        Assert.Null(UpgradeAnvil.Quote(item, ladder, bag));
    }

    [Fact]
    public void PityBoundsTheWorstCase()
    {
        // What the counter promises: a rung can never take more than pity + 1 attempts.
        foreach (var step in TestSpecs.Ladder().Steps)
        {
            Assert.True(UpgradeAnvil.MaxAttempts(step) <= step.Pity + 1);
        }
    }

    [Fact]
    public void UpgradeScaling_RoughlyDoublesBaseStatsAtNine()
    {
        Assert.Equal(1.0, UpgradeScaling.Multiplier(0));
        Assert.True(UpgradeScaling.Multiplier(9) > 1.9);
        Assert.True(UpgradeScaling.Multiplier(9) < 2.2);

        // Each level must be worth more than the one before it, so the expensive end of the
        // ladder is where the payoff is.
        for (var n = 1; n < UpgradeScaling.MaxLevel; n++)
        {
            var step = UpgradeScaling.Multiplier(n) - UpgradeScaling.Multiplier(n - 1);
            var next = UpgradeScaling.Multiplier(n + 1) - UpgradeScaling.Multiplier(n);

            Assert.True(next > step);
        }
    }

    [Fact]
    public void UpgradeLevel_ScalesTheFrameAndLeavesRolledLinesAlone()
    {
        var specs = TestSpecs.Standard();
        var factory = new ItemFactory(specs, specs);
        var item = factory.Create("wpn_test_sword", new DeterministicRng(4));
        var spec = specs.Get("wpn_test_sword");

        var linesBefore = item.Bonuses.Select(l => l.Magnitude).ToList();
        var damageBefore = item.WeaponDamageMax(spec);

        item.UpgradeLevel = 9;

        Assert.True(item.WeaponDamageMax(spec) > damageBefore);
        Assert.Equal(linesBefore, item.Bonuses.Select(l => l.Magnitude));
    }
}
