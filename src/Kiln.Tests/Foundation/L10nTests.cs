using Kiln.Core.Foundation;
using Kiln.Data.Localisation;
using Xunit;

namespace Kiln.Tests.Foundation;

/// <summary>
/// L10n is global state. These tests only use strings nothing else in the suite reads, and put
/// English back when they finish, so they cannot change what a parallel test sees.
/// </summary>
public class L10nTests
{
    private static readonly Dictionary<string, string> English = new()
    {
        ["$item.test_l10n_thing.name"] = "Test Thing",
    };

    private static void WithItalian(Dictionary<string, string> italian, Action body)
    {
        try
        {
            L10n.Use("it", italian, English);
            body();
        }
        finally
        {
            L10n.Use(L10n.English, new Dictionary<string, string>(), new Dictionary<string, string>());
        }
    }

    [Fact]
    public void InterfaceText_TranslatesOrFallsBackToTheEnglish()
    {
        WithItalian(new() { ["L10n test: buy"] = "Compra", ["L10n test: empty"] = "" }, () =>
        {
            Assert.Equal("Compra", L10n.T("L10n test: buy"));
            Assert.Equal("L10n test: sell", L10n.T("L10n test: sell"));
            Assert.Equal("L10n test: empty", L10n.T("L10n test: empty"));
        });
    }

    [Fact]
    public void Formatting_UsesTheLanguagesNumbers()
    {
        WithItalian(new() { ["L10n test: {0:N0} yang"] = "{0:N0} yang (it)" }, () =>
        {
            Assert.Equal("12.000 yang (it)", L10n.F("L10n test: {0:N0} yang", 12000));
        });
    }

    [Fact]
    public void Names_FallBackToEnglishThenToAGuess()
    {
        WithItalian(new(), () =>
        {
            Assert.Equal("Test Thing", L10n.Name("$item.test_l10n_thing.name"));
            Assert.Equal("Iron Sword", L10n.Name("$item.wpn_l10n_iron_sword.name".Replace("l10n_", "")));
            Assert.Equal("plain text", L10n.Name("plain text"));
        });
    }

    [Fact]
    public void Guess_DropsThePrefixAndTitleCases() =>
        Assert.Equal("Corrupted Wolf", L10n.Guess("$mob.mob_corrupted_wolf.name"));

    [Fact]
    public void Scanner_FindsWrappedTextAndContentKeys()
    {
        const string code = """
            // L10n.T("in a comment")
            label.Text = L10n.T("Buy");
            status = L10n.F("Sold {0} for {1:N0} yang.", name, price);
            quote = L10n.T("Say \"hello\"");
            """;

        Assert.Equal(["Buy", "Sold {0} for {1:N0} yang.", "Say \"hello\""], StringScanner.InterfaceStrings(code));
        Assert.Equal(["$item.wpn_x.name"], StringScanner.ContentKeys("""{ "name": "$item.wpn_x.name", "id": "wpn_x" }"""));
    }

    [Fact]
    public void Scanner_ReportsInterpolatedStringsHandedToL10n()
    {
        const string code = "a = 1;\nb = L10n.T($\"Sold {name}\");\n";

        Assert.Equal([2], StringScanner.InterpolatedCalls(code));
    }

    [Fact]
    public void Placeholders_MustSurviveTranslation()
    {
        Assert.True(StringScanner.SamePlaceholders("Sold {0} for {1:N0} yang.", "Venduto {0} per {1:N0} yang."));
        Assert.True(StringScanner.SamePlaceholders("{0} of {1}", "{1}: {0}"));
        Assert.False(StringScanner.SamePlaceholders("Sold {0} for {1:N0} yang.", "Venduto {0}."));
        Assert.False(StringScanner.SamePlaceholders("{0:N0} yang", "{0} yang"));
    }
}
