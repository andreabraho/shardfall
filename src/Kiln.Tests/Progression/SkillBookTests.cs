using Kiln.Core.Progression;
using Xunit;

namespace Kiln.Tests.Progression;

public class SkillBookTests
{
    /// <summary>Spends every point a skill can take, which is what masters it.</summary>
    private static SkillBook Mastered(string skillId)
    {
        var book = new SkillBook();

        for (var i = 0; i < SkillBook.MaxPoints; i++) book.Invest(skillId);

        return book;
    }

    [Fact]
    public void UnknownSkills_AreLockedAndRankNormal()
    {
        var book = new SkillBook();

        Assert.False(book.IsUnlocked("skl_whirlwind"));
        Assert.Equal(MasteryRank.Normal, book.RankOf("skl_whirlwind"));
        Assert.Equal(0, book.PointsIn("skl_whirlwind"));
    }

    [Fact]
    public void Unlock_LearnsOnce()
    {
        var book = new SkillBook();

        Assert.True(book.Unlock("skl_cleave"));
        Assert.False(book.Unlock("skl_cleave"));
        Assert.Equal(1, book.Count);
        Assert.Equal(1, book.PointsIn("skl_cleave"));
    }

    [Fact]
    public void Invest_StopsAtTheCap()
    {
        var book = Mastered("skl_cleave");

        Assert.Equal(SkillBook.MaxPoints, book.PointsIn("skl_cleave"));
        Assert.False(book.Invest("skl_cleave"));
    }

    [Fact]
    public void SeventhPoint_Masters()
    {
        var book = new SkillBook();

        for (var i = 1; i < SkillBook.MaxPoints; i++)
        {
            book.Invest("skl_cleave");
            Assert.Equal(MasteryRank.Normal, book.RankOf("skl_cleave"));
        }

        book.Invest("skl_cleave");
        Assert.Equal(MasteryRank.Master, book.RankOf("skl_cleave"));
    }

    [Fact]
    public void RecordUse_OnAnUnknownSkill_DoesNothing()
    {
        // Guards against a caster spending a point's worth of progress on a skill the
        // player never learned.
        var book = new SkillBook();

        Assert.Null(book.RecordUse("skl_cleave"));
        Assert.Equal(0, book.UsesOf("skl_cleave"));
    }

    [Fact]
    public void RecordUse_CountsNothingBeforeMastery()
    {
        // Otherwise a skill ground out at one point would jump straight past Master the
        // moment the seventh point landed.
        var book = new SkillBook();
        book.Invest("skl_cleave");

        for (var i = 0; i < 500; i++) book.RecordUse("skl_cleave");

        Assert.Equal(0, book.UsesOf("skl_cleave"));
        Assert.Equal(MasteryRank.Normal, book.RankOf("skl_cleave"));
    }

    [Fact]
    public void Mastery_AdvancesThroughTheRanksByUse()
    {
        var book = Mastered("skl_cleave");

        MasteryRank? promotion = null;

        for (var i = 0; i < SkillBook.GrandMasterUses; i++)
        {
            promotion = book.RecordUse("skl_cleave") ?? promotion;
        }

        Assert.Equal(MasteryRank.GrandMaster, book.RankOf("skl_cleave"));
        Assert.Equal(MasteryRank.GrandMaster, promotion);
    }

    [Fact]
    public void Mastery_ReportsAPromotionExactlyOnce()
    {
        // The caller announces promotions, so a duplicate would fire the message twice.
        var book = Mastered("skl_cleave");

        var promotions = 0;

        for (var i = 0; i < SkillBook.PerfectUses + 50; i++)
        {
            if (book.RecordUse("skl_cleave") is not null) promotions++;
        }

        Assert.Equal(2, promotions);
        Assert.Equal(MasteryRank.Perfect, book.RankOf("skl_cleave"));
    }

    [Fact]
    public void Mastery_StopsAtPerfect()
    {
        var book = Mastered("skl_cleave");

        for (var i = 0; i < SkillBook.PerfectUses * 2; i++)
        {
            book.RecordUse("skl_cleave");
        }

        Assert.Equal(MasteryRank.Perfect, book.RankOf("skl_cleave"));
        Assert.Equal(0, book.ToNextRank("skl_cleave"));
    }

    [Fact]
    public void ToNextRank_CountsPointsFirst_ThenUses()
    {
        var book = new SkillBook();
        book.Invest("skl_cleave");

        Assert.True(book.NextRankCostsPoints("skl_cleave"));
        Assert.Equal(SkillBook.MaxPoints - 1, book.ToNextRank("skl_cleave"));

        while (book.Invest("skl_cleave")) { }

        Assert.False(book.NextRankCostsPoints("skl_cleave"));
        Assert.Equal(SkillBook.GrandMasterUses, book.ToNextRank("skl_cleave"));

        book.RecordUse("skl_cleave");
        Assert.Equal(SkillBook.GrandMasterUses - 1, book.ToNextRank("skl_cleave"));
    }

    [Fact]
    public void Refund_ReturnsEveryPointAndForgetsEverything()
    {
        var book = Mastered("skl_cleave");
        book.Invest("skl_whirlwind");

        Assert.Equal(SkillBook.MaxPoints + 1, book.Refund());
        Assert.Equal(0, book.Count);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var book = Mastered("skl_cleave");
        book.Invest("skl_whirlwind");

        for (var i = 0; i < 100; i++) book.RecordUse("skl_cleave");

        var restored = new SkillBook();
        restored.Load(book.Save(), book.SavePoints());

        Assert.Equal(MasteryRank.Master, restored.RankOf("skl_cleave"));
        Assert.Equal(SkillBook.MaxPoints, restored.PointsIn("skl_cleave"));
        Assert.Equal(100, restored.UsesOf("skl_cleave"));
        Assert.True(restored.IsUnlocked("skl_whirlwind"));
        Assert.Equal(MasteryRank.Normal, restored.RankOf("skl_whirlwind"));
    }

    [Fact]
    public void Load_WithoutPoints_LeavesTheSkillLearnedAtOne()
    {
        // A save written before points existed.
        var book = new SkillBook();
        book.Load(new Dictionary<string, int> { ["skl_cleave"] = 300 });

        Assert.True(book.IsUnlocked("skl_cleave"));
        Assert.Equal(1, book.PointsIn("skl_cleave"));
        Assert.Equal(MasteryRank.Normal, book.RankOf("skl_cleave"));
    }

    [Fact]
    public void Load_ClampsNegativeUses()
    {
        var book = new SkillBook();
        book.Load(new Dictionary<string, int> { ["skl_cleave"] = -50 });

        Assert.Equal(0, book.UsesOf("skl_cleave"));
        Assert.True(book.IsUnlocked("skl_cleave"));
    }

    [Fact]
    public void Forget_SupportsRespec()
    {
        var book = new SkillBook();
        book.Unlock("skl_cleave");

        Assert.True(book.Forget("skl_cleave"));
        Assert.False(book.IsUnlocked("skl_cleave"));
    }
}
