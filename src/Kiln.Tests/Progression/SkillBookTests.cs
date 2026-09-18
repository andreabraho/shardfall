using Kiln.Core.Progression;
using Xunit;

namespace Kiln.Tests.Progression;

public class SkillBookTests
{
    [Fact]
    public void UnknownSkills_AreLockedAndRankNormal()
    {
        var book = new SkillBook();

        Assert.False(book.IsUnlocked("skl_whirlwind"));
        Assert.Equal(MasteryRank.Normal, book.RankOf("skl_whirlwind"));
    }

    [Fact]
    public void Unlock_LearnsOnce()
    {
        var book = new SkillBook();

        Assert.True(book.Unlock("skl_cleave"));
        Assert.False(book.Unlock("skl_cleave"));
        Assert.Equal(1, book.Count);
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
    public void Mastery_AdvancesThroughTheRanks()
    {
        var book = new SkillBook();
        book.Unlock("skl_cleave");

        MasteryRank? promotion = null;

        for (var i = 0; i < SkillBook.MasterUses; i++)
        {
            promotion = book.RecordUse("skl_cleave") ?? promotion;
        }

        Assert.Equal(MasteryRank.Master, book.RankOf("skl_cleave"));
        Assert.Equal(MasteryRank.Master, promotion);
    }

    [Fact]
    public void Mastery_ReportsAPromotionExactlyOnce()
    {
        // The caller announces promotions, so a duplicate would fire the message twice.
        var book = new SkillBook();
        book.Unlock("skl_cleave");

        var promotions = 0;

        for (var i = 0; i < SkillBook.PerfectUses + 50; i++)
        {
            if (book.RecordUse("skl_cleave") is not null) promotions++;
        }

        Assert.Equal(3, promotions);
        Assert.Equal(MasteryRank.Perfect, book.RankOf("skl_cleave"));
    }

    [Fact]
    public void Mastery_StopsAtPerfect()
    {
        var book = new SkillBook();
        book.Unlock("skl_cleave");

        for (var i = 0; i < SkillBook.PerfectUses * 2; i++)
        {
            book.RecordUse("skl_cleave");
        }

        Assert.Equal(MasteryRank.Perfect, book.RankOf("skl_cleave"));
        Assert.Equal(0, book.UsesToNextRank("skl_cleave"));
    }

    [Fact]
    public void UsesToNextRank_CountsDown()
    {
        var book = new SkillBook();
        book.Unlock("skl_cleave");

        Assert.Equal(SkillBook.MasterUses, book.UsesToNextRank("skl_cleave"));

        book.RecordUse("skl_cleave");
        Assert.Equal(SkillBook.MasterUses - 1, book.UsesToNextRank("skl_cleave"));
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var book = new SkillBook();
        book.Unlock("skl_cleave");
        book.Unlock("skl_whirlwind");

        for (var i = 0; i < 100; i++) book.RecordUse("skl_cleave");

        var restored = new SkillBook();
        restored.Load(book.Save());

        Assert.Equal(MasteryRank.Master, restored.RankOf("skl_cleave"));
        Assert.True(restored.IsUnlocked("skl_whirlwind"));
        Assert.Equal(MasteryRank.Normal, restored.RankOf("skl_whirlwind"));
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
