using System.Linq;
using Kiln.Core.Foundation;

namespace Kiln.Game.Audio;

/// <summary>
/// The music the player can choose between (REF-20): the moments of the game that have music,
/// and a readable title for each track file.
/// </summary>
public static class MusicTracks
{
    /// <summary>What "every track in turn" is stored as.</summary>
    public const string Rotation = "";

    public const string Boss = "mus_boss";

    /// <summary>The moments, in the order the settings list them.</summary>
    /// <remarks>A property, so the words are read in whatever language is current.</remarks>
    public static (string Id, string Label)[] Moments =>
    [
        ("mus_menu", L10n.T("Main menu")),
        ("mus_village", L10n.T("Village")),
        ("mus_wilds", L10n.T("Open maps")),
        ("mus_catacombs", L10n.T("Demon Tower")),
        (Boss, L10n.T("Bosses")),
    ];

    private static readonly (string Prefix, string Author)[] Authors =
    [
        ("kevin-macleod-", "Kevin MacLeod"),
        ("alexander-nakarada-", "Alexander Nakarada"),
    ];

    private static readonly string[] Small = ["of", "the", "a", "an", "in", "on"];

    /// <summary>"res://audio/music/kevin-macleod-nu-flute.mp3" → "Nu Flute — Kevin MacLeod".</summary>
    public static string Title(string path)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        var author = "";

        foreach (var (prefix, who) in Authors)
        {
            if (!name.StartsWith(prefix, System.StringComparison.Ordinal)) continue;

            name = name[prefix.Length..];
            author = who;
        }

        var words = name.Split('-', System.StringSplitOptions.RemoveEmptyEntries)
            .Select((w, i) => i > 0 && Small.Contains(w) ? w : char.ToUpperInvariant(w[0]) + w[1..]);

        var title = string.Join(' ', words);

        return author.Length > 0 ? $"{title} — {author}" : title;
    }
}
