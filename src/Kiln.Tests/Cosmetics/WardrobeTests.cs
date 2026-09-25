using Kiln.Core.Cosmetics;
using Xunit;

namespace Kiln.Tests.Cosmetics;

public class WardrobeTests
{
    [Fact]
    public void Unlocking_AddsOnce()
    {
        var wardrobe = new Wardrobe();

        Assert.True(wardrobe.Unlock("cos_frost_blade"));
        Assert.False(wardrobe.Unlock("cos_frost_blade"));
        Assert.True(wardrobe.Owns("cos_frost_blade"));
        Assert.Single(wardrobe.Owned);
    }

    [Fact]
    public void OnlyWhatIsOwned_CanBeWorn()
    {
        var wardrobe = new Wardrobe();

        Assert.False(wardrobe.Wear("sword", "cos_frost_blade"));
        Assert.Null(wardrobe.WornIn("sword"));

        wardrobe.Unlock("cos_frost_blade");

        Assert.True(wardrobe.Wear("sword", "cos_frost_blade"));
        Assert.Equal("cos_frost_blade", wardrobe.WornIn("sword"));

        // Null takes it off.
        Assert.True(wardrobe.Wear("sword", null));
        Assert.Null(wardrobe.WornIn("sword"));
    }

    [Fact]
    public void OneSlotPerKind()
    {
        var wardrobe = new Wardrobe();
        wardrobe.Unlock("cos_frost_blade");
        wardrobe.Unlock("cos_venom_blade");
        wardrobe.Unlock("cos_bone_armour");

        wardrobe.Wear("sword", "cos_frost_blade");
        wardrobe.Wear("sword", "cos_venom_blade");
        wardrobe.Wear("armour", "cos_bone_armour");

        Assert.Equal("cos_venom_blade", wardrobe.WornIn("sword"));
        Assert.Equal("cos_bone_armour", wardrobe.WornIn("armour"));
        Assert.Equal(2, wardrobe.Worn.Count);
    }

    [Fact]
    public void Loading_DropsWhatNoLongerExists_AndWhatWasNeverOwned()
    {
        var wardrobe = new Wardrobe();

        wardrobe.Load(
            ["cos_frost_blade", "cos_removed_since"],
            new Dictionary<string, string> { ["sword"] = "cos_frost_blade", ["armour"] = "cos_never_won" },
            id => id != "cos_removed_since");

        Assert.Equal(["cos_frost_blade"], wardrobe.Owned);
        Assert.Equal("cos_frost_blade", wardrobe.WornIn("sword"));
        Assert.Null(wardrobe.WornIn("armour"));
    }

    [Fact]
    public void Changes_AreAnnounced()
    {
        var wardrobe = new Wardrobe();
        var changes = 0;
        wardrobe.Changed += () => changes++;

        wardrobe.Unlock("cos_imp");
        wardrobe.Unlock("cos_imp");
        wardrobe.Wear("companion", "cos_imp");
        wardrobe.Wear("companion", "cos_imp");
        wardrobe.Wear("companion", null);

        Assert.Equal(3, changes);
    }
}
