using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Sohan.Core.Foundation;

/// <summary>
/// A validated content identifier, e.g. <c>mob_corrupted_wolf</c>.
/// Format is enforced at parse time so a typo in a JSON file fails the build
/// (doc 06 §10) instead of surfacing as a null reference hours into a playtest.
/// </summary>
public readonly partial struct ContentId : IEquatable<ContentId>, IComparable<ContentId>
{
    /// <summary>Lowercase, prefix, underscore, at least one more segment.</summary>
    [GeneratedRegex("^[a-z]+_[a-z0-9_]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidPattern();

    private readonly string? _value;

    private ContentId(string value) => _value = value;

    public string Value => _value ?? string.Empty;
    public bool IsEmpty => string.IsNullOrEmpty(_value);

    /// <summary>The part before the first underscore, e.g. <c>mob</c> for <c>mob_corrupted_wolf</c>.</summary>
    public string Prefix
    {
        get
        {
            if (string.IsNullOrEmpty(_value)) return string.Empty;
            var i = _value.IndexOf('_');
            return i < 0 ? _value : _value[..i];
        }
    }

    public static bool IsValid([NotNullWhen(true)] string? value) =>
        !string.IsNullOrEmpty(value) && ValidPattern().IsMatch(value);

    public static bool TryParse(string? value, out ContentId id)
    {
        if (IsValid(value))
        {
            id = new ContentId(value);
            return true;
        }

        id = default;
        return false;
    }

    public static ContentId Parse(string value) =>
        TryParse(value, out var id)
            ? id
            : throw new FormatException(
                $"'{value}' is not a valid content id. Expected lowercase prefix_name, e.g. 'mob_corrupted_wolf'.");

    public bool Equals(ContentId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is ContentId other && Equals(other);
    public override int GetHashCode() => _value?.GetHashCode(StringComparison.Ordinal) ?? 0;
    public int CompareTo(ContentId other) => string.CompareOrdinal(_value, other._value);
    public override string ToString() => Value;

    public static bool operator ==(ContentId a, ContentId b) => a.Equals(b);
    public static bool operator !=(ContentId a, ContentId b) => !a.Equals(b);
}
