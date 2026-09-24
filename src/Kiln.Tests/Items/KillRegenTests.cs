using Kiln.Core.Combat;
using Kiln.Core.Items;
using Xunit;

namespace Kiln.Tests.Items;

/// <summary>The earrings' base stat: healing after a kill, growing with the upgrade.</summary>
public class KillRegenTests
{
    private static ItemInstance Earring(int level) => new(1, "ear_test") { UpgradeLevel = level };

    [Fact]
    public void ParsesAndAppliesAsWholePercent()
    {
        Assert.True(BonusStat.TryParse("kill_regen_pct", out var stat, out _));

        var mods = new StatModifiers();
        stat.Apply(mods, 1.5);

        Assert.Equal(1.5, mods.KillRegenPct, 3);
    }

    [Fact]
    public void GrowsFromOnePerCentAtPlusZeroToThreeAtPlusNine()
    {
        var grant = new BonusLine("kill_regen_pct", BonusStat.Parse("kill_regen_pct"), 1);

        Assert.Equal(1.0, Earring(0).ScaledGrant(grant), 3);
        Assert.Equal(3.0, Earring(9).ScaledGrant(grant), 3);
    }

    [Fact]
    public void OtherBaseStatsKeepTheirUsualGrowth()
    {
        var grant = new BonusLine("max_mana_flat", BonusStat.Parse("max_mana_flat"), 100);

        Assert.Equal(100 * UpgradeScaling.Multiplier(9), Earring(9).ScaledGrant(grant), 3);
    }
}
