using System.Text.RegularExpressions;

namespace Kiln.Data.Localisation;

/// <summary>
/// Finds every piece of text a player can read (UIX-06): the <c>$keys</c> content uses, and
/// the English wrapped in <c>L10n.T</c> / <c>L10n.F</c> in code.
/// </summary>
/// <remarks>
/// A scan rather than a registry, because a registry is a second list that has to be kept in
/// step with the first, and the first is the code. What the scan cannot see — text built by
/// gluing words together, or an interpolated string handed to <c>T</c> — it reports, since
/// either would reach the player untranslatable.
/// </remarks>
public static partial class StringScanner
{
    [GeneratedRegex("\"(\\$[a-z_]+\\.[a-z0-9_.]+)\"")]
    private static partial Regex ContentKey();

    [GeneratedRegex("L10n\\.[TF]\\(\\s*\"((?:[^\"\\\\]|\\\\.)*)\"")]
    private static partial Regex Wrapped();

    [GeneratedRegex("L10n\\.[TF]\\(\\s*\\$")]
    private static partial Regex Interpolated();

    /// <summary>Every <c>$key</c> a JSON file mentions.</summary>
    public static IEnumerable<string> ContentKeys(string json) =>
        ContentKey().Matches(json).Select(m => m.Groups[1].Value);

    /// <summary>Every English string wrapped for translation in a C# file, unescaped.</summary>
    public static IEnumerable<string> InterfaceStrings(string code) =>
        Wrapped().Matches(WithoutComments(code)).Select(m => Unescape(m.Groups[1].Value));

    /// <summary>
    /// The code with comment-only lines blanked, line count kept. Documentation that shows how
    /// to call <c>L10n.T</c> is not text a player will read.
    /// </summary>
    private static string WithoutComments(string code) =>
        string.Join('\n', code.Split('\n').Select(line => line.TrimStart().StartsWith("//") ? "" : line));

    /// <summary>Line numbers where an interpolated string is handed to T or F.</summary>
    /// <remarks>
    /// <c>L10n.T($"Sold {name}")</c> looks translated and never is: the text the table would
    /// need is different every time. It has to be <c>L10n.F("Sold {0}", name)</c>.
    /// </remarks>
    public static IEnumerable<int> InterpolatedCalls(string code)
    {
        code = WithoutComments(code);

        foreach (Match match in Interpolated().Matches(code))
        {
            yield return code[..match.Index].Count(c => c == '\n') + 1;
        }
    }

    private static string Unescape(string text) =>
        text.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\\", "\\");
}
