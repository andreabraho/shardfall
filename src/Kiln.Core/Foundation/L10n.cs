using System.Globalization;

namespace Kiln.Core.Foundation;

/// <summary>
/// Every string a player reads goes through here (UIX-06, NFR-L.1).
/// </summary>
/// <remarks>
/// Two kinds of text, looked up two ways.
/// <para>
/// <b>Interface text</b> is written in English in the code and wrapped: <c>L10n.T("Buy")</c>,
/// <c>L10n.F("Sold {0} for {1:N0} yang.", name, price)</c>. The English is the key. That keeps
/// the code readable, cannot drift from a key nobody remembers the meaning of, and makes
/// "extract every string" a scan for these two calls — which is what <c>Kiln.Tools strings</c>
/// does. A translation is a table from the English to the other language; anything missing
/// from it shows in English rather than as a key.
/// </para>
/// <para>
/// <b>Content names</b> — items, creatures, maps, quests, villagers' lines — are keys in the
/// data (<c>$item.wpn_iron_sword.name</c>) with the English in <c>data/strings/en.json</c>,
/// because content is written by people editing JSON, not code. <see cref="Name"/> reads them.
/// </para>
/// <para>
/// Engine-free, so the item text built in Core can be translated like the rest. The game
/// installs the tables at boot; until then — and in every test — text passes through as
/// written.
/// </para>
/// </remarks>
public static class L10n
{
    public const string English = "en";

    private static IReadOnlyDictionary<string, string> _current = new Dictionary<string, string>();
    private static IReadOnlyDictionary<string, string> _english = new Dictionary<string, string>();

    /// <summary>The active language code, e.g. <c>en</c> or <c>it</c>.</summary>
    public static string Language { get; private set; } = English;

    /// <summary>Number formatting for the active language: 12,000.5 or 12.000,5.</summary>
    /// <remarks>
    /// Built by hand rather than from <see cref="CultureInfo"/>: the projects run with invariant
    /// globalisation, where named cultures do not exist. Separators are all a number needs.
    /// </remarks>
    public static NumberFormatInfo Culture { get; private set; } = Numbers(",", ".");

    /// <summary>Installs the tables for a language, with English as the fallback.</summary>
    public static void Use(string language, IReadOnlyDictionary<string, string> table, IReadOnlyDictionary<string, string> english)
    {
        Language = language;
        _current = table;
        _english = english;

        // English groups with commas; the continental languages this game is likely to get
        // group with points and use a comma for the decimal.
        Culture = language == English ? Numbers(",", ".") : Numbers(".", ",");
    }

    private static NumberFormatInfo Numbers(string group, string point)
    {
        var format = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();

        format.NumberGroupSeparator = group;
        format.NumberDecimalSeparator = point;
        format.PercentGroupSeparator = group;
        format.PercentDecimalSeparator = point;

        // "80%", not the invariant culture's "80 %".
        format.PercentPositivePattern = 1;
        format.PercentNegativePattern = 1;

        return format;
    }

    /// <summary>Interface text: the translation of this English, or the English itself.</summary>
    public static string T(string english) =>
        !string.IsNullOrEmpty(english) && _current.TryGetValue(english, out var text) && text.Length > 0 ? text : english;

    /// <summary>Interface text with values in it, formatted for the active language.</summary>
    public static string F(string english, params object?[] args) => string.Format(Culture, T(english), args);

    /// <summary>
    /// A content name or line: <c>$item.wpn_iron_sword.name</c> in the active language, else in
    /// English, else a readable guess built from the key so a missing entry is ugly, not blank.
    /// </summary>
    /// <remarks>Text that does not start with <c>$</c> is returned as it is.</remarks>
    public static string Name(string key)
    {
        if (string.IsNullOrEmpty(key) || !key.StartsWith('$')) return key ?? "";

        if (_current.TryGetValue(key, out var text) && text.Length > 0) return text;
        if (_english.TryGetValue(key, out var english) && english.Length > 0) return english;

        return Guess(key);
    }

    /// <summary>
    /// <c>$item.wpn_iron_sword.name</c> → "Iron Sword": the id with its prefix dropped, title-cased.
    /// </summary>
    /// <remarks>The stand-in used before the tables existed, kept as the last resort.</remarks>
    public static string Guess(string key)
    {
        var parts = key.TrimStart('$').Split('.');
        var stem = parts.Length >= 2 ? parts[^2] : parts[0];
        var words = stem.Split('_', StringSplitOptions.RemoveEmptyEntries);

        return string.Join(' ', words.Skip(words.Length > 1 ? 1 : 0)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }
}
