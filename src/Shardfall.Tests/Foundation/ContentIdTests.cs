using Shardfall.Core.Foundation;
using Xunit;

namespace Shardfall.Tests.Foundation;

public class ContentIdTests
{
    [Theory]
    [InlineData("mob_corrupted_wolf")]
    [InlineData("wpn_iron_sword")]
    [InlineData("qst_a1_00_prologue")]
    [InlineData("dt_valley_animal_t2")]
    public void Accepts_WellFormedIds(string id)
    {
        Assert.True(ContentId.IsValid(id));
        Assert.Equal(id, ContentId.Parse(id).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("NoPrefix")]
    [InlineData("Mob_Corrupted_Wolf")]   // uppercase
    [InlineData("mob")]                   // no underscore
    [InlineData("mob-corrupted-wolf")]    // hyphens
    [InlineData("mob corrupted wolf")]    // spaces
    [InlineData("1mob_wolf")]             // leading digit in prefix
    [InlineData("mob_")]                  // empty tail
    public void Rejects_MalformedIds(string? id)
    {
        Assert.False(ContentId.IsValid(id));
        Assert.False(ContentId.TryParse(id, out _));
    }

    [Fact]
    public void Parse_Throws_WithAHelpfulMessage()
    {
        var ex = Assert.Throws<FormatException>(() => ContentId.Parse("Bad Id"));
        Assert.Contains("prefix_name", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Prefix_IsTheLeadingSegment()
    {
        Assert.Equal("mob", ContentId.Parse("mob_corrupted_wolf").Prefix);
        Assert.Equal("qst", ContentId.Parse("qst_a1_00_prologue").Prefix);
    }

    [Fact]
    public void Equality_IsByValue()
    {
        var a = ContentId.Parse("wpn_iron_sword");
        var b = ContentId.Parse("wpn_iron_sword");
        var c = ContentId.Parse("wpn_steel_sword");

        Assert.True(a == b);
        Assert.True(a != c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Default_IsEmpty()
    {
        ContentId id = default;
        Assert.True(id.IsEmpty);
        Assert.Equal(string.Empty, id.Value);
        Assert.Equal(string.Empty, id.Prefix);
    }
}
