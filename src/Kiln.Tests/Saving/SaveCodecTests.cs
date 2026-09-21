using Kiln.Core.Foundation;
using Kiln.Core.Items;
using Kiln.Core.Saving;
using Kiln.Tests.Items;
using Xunit;

namespace Kiln.Tests.Saving;

/// <summary>
/// The save file (UIX-01). Written against the ways a save goes wrong that the player cannot
/// recover from: a round trip that quietly loses something, a damaged file that loads anyway,
/// and a file from a newer build read as if it were current.
/// </summary>
public class SaveCodecTests
{
    private static SaveGame Sample() => new()
    {
        CreatedUtc = "2026-09-21T10:00:00Z",
        Label = "Quick save",
        Difficulty = "Adept",
        Seed = 42,
        CompletedQuests = ["qst_a"],
        Player = new SavedPlayer
        {
            Level = 19,
            Experience = 12345,
            AttributePoints = 2,
            SkillPoints = 1,
            Assigned = [10, 4, 0, 8],
            Skills = new() { ["skl_cleave"] = 40 },
            Health = 0.5,
            Yang = 99_000,
            NextUid = 311,
            Items = [new SavedItem { Uid = 7, Def = "wpn_test_sword", Upgrade = 3, At = [0, 0] }],
            Worn = new() { ["Weapon"] = 7 },
        },
        World = new SavedWorld
        {
            Zone = "zone_catacombs",
            Position = [1, 2, 3],
            DiscoveredShrines = ["shr_a"],
            Anchor = "shr_a",
            Depths = new() { ["zone_catacombs"] = 4 },
            Explored = new() { ["zone_ridge"] = [5, 6] },
        },
    };

    [Fact]
    public void ARoundTripKeepsEverything()
    {
        var back = SaveCodec.Decode(SaveCodec.Encode(Sample()));

        Assert.True(back.Ok, back.Message);

        var save = back.Save!;

        Assert.Equal("Adept", save.Difficulty);
        Assert.Equal(19, save.Player.Level);
        Assert.Equal([10, 4, 0, 8], save.Player.Assigned);
        Assert.Equal(40, save.Player.Skills["skl_cleave"]);
        Assert.Equal(311, save.Player.NextUid);
        Assert.Equal(7, save.Player.Worn["Weapon"]);
        Assert.Equal(3, save.Player.Items[0].Upgrade);
        Assert.Equal("zone_catacombs", save.World.Zone);
        Assert.Equal(4, save.World.Depths["zone_catacombs"]);
        Assert.Equal([5L, 6L], save.World.Explored["zone_ridge"]);
        Assert.Equal(["qst_a"], save.CompletedQuests);
    }

    /// <summary>
    /// FR-11.3: a clear error, never a silent corrupt load. One changed digit in the yang is
    /// exactly the kind of damage that would otherwise load and be believed.
    /// </summary>
    [Fact]
    public void ADamagedSaveIsRefused()
    {
        var text = SaveCodec.Encode(Sample()).Replace("99000", "99001");

        Assert.Equal(SaveReadStatus.Corrupt, SaveCodec.Decode(text).Status);
    }

    [Fact]
    public void ASaveFromANewerBuildIsRefused()
    {
        var text = SaveCodec.Encode(Sample());
        var newer = text.Replace($"\"kiln_save\":{SaveCodec.CurrentVersion}", $"\"kiln_save\":{SaveCodec.CurrentVersion + 1}");

        Assert.Equal(SaveReadStatus.TooNew, SaveCodec.Decode(newer).Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("{}\n{}")]
    [InlineData("not json\n{}")]
    public void SomethingThatIsNotASaveIsUnreadable(string text)
    {
        Assert.Equal(SaveReadStatus.Unreadable, SaveCodec.Decode(text).Status);
    }

    [Fact]
    public void AnItemComesBackAsTheSameItem()
    {
        var specs = TestSpecs.Standard();
        var factory = new ItemFactory(specs, specs);
        var sword = factory.Create("wpn_test_sword", new DeterministicRng(3));

        sword.Locked = true;
        sword.SetLineLocked(1, true);

        var back = ItemSnapshots.Restore(ItemSnapshots.Capture(sword, [2, 1]), specs, specs)!;

        Assert.Equal(sword.Uid, back.Uid);
        Assert.Equal(sword.DefId, back.DefId);
        Assert.True(back.Locked);
        Assert.Equal(sword.Bonuses.Select(l => (l.LineId, l.Magnitude, l.Locked)),
                     back.Bonuses.Select(l => (l.LineId, l.Magnitude, l.Locked)));
        Assert.Equal(sword.Sockets.Count, back.Sockets.Count);
    }

    [Fact]
    public void SocketsKeepTheirStateAndTheirStones()
    {
        var specs = TestSpecs.Standard();
        var saved = new SavedItem { Uid = 1, Def = "wpn_test_sword", Sockets = [null, "stn_test_ember"] };

        var back = ItemSnapshots.Restore(saved, specs, specs)!;

        Assert.False(back.Sockets[0].IsOpen);
        Assert.True(back.Sockets[1].IsOpen);
        Assert.Equal("stn_test_ember", back.Sockets[1].StoneId);

        var reopened = ItemSnapshots.Restore(ItemSnapshots.Capture(back, null), specs, specs)!;

        Assert.Equal("stn_test_ember", reopened.Sockets[1].StoneId);
    }

    [Fact]
    public void AnItemWhoseDefinitionIsGoneIsDroppedNotFatal()
    {
        var specs = TestSpecs.Standard();

        Assert.Null(ItemSnapshots.Restore(new SavedItem { Uid = 1, Def = "wpn_removed" }, specs, specs));
    }

    [Fact]
    public void TheBagIsRestoredWhereItWas()
    {
        var specs = TestSpecs.Standard();
        var factory = new ItemFactory(specs, specs);
        var bag = new Inventory(specs, 10, 8);

        var vest = factory.CreatePlain("arm_test_vest");
        var scrap = factory.CreatePlain("mat_test_scrap", 12);

        var homeless = bag.Restore(4500, -9, [(vest, 3, 2), (scrap, 0, 0)]);

        Assert.Empty(homeless);
        Assert.Equal(4500, bag.Yang);
        Assert.Equal(-9, bag.NextGrantUid);
        Assert.Equal(3, bag.Find(vest)!.X);
        Assert.Equal(12, bag.CountOf("mat_test_scrap"));
    }

    [Fact]
    public void AnItemThatNoLongerFitsItsSpotIsFoundAnotherRatherThanLost()
    {
        var specs = TestSpecs.Standard();
        var factory = new ItemFactory(specs, specs);
        var bag = new Inventory(specs, 10, 8);

        var a = factory.CreatePlain("arm_test_vest");
        var b = factory.CreatePlain("arm_test_vest");

        // Both claim the same corner. One of them has to move; neither may disappear.
        var homeless = bag.Restore(0, -1, [(a, 0, 0), (b, 0, 0)]);

        Assert.Empty(homeless);
        Assert.NotNull(bag.Find(a));
        Assert.NotNull(bag.Find(b));
    }
}
