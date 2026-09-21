using System.Text.Encodings.Web;
using System.Text.Json;
using Kiln.Core.Foundation;
using Kiln.Data.Loading;
using Kiln.Data.Localisation;

namespace Kiln.Tools.Commands;

/// <summary>
/// The string tables (UIX-06): what text exists, and how much of it each language covers.
/// </summary>
/// <remarks>
/// <code>
///   strings                 report; exit 1 if content uses a key English does not define,
///                           or code hands an interpolated string to L10n
///   strings --seed-en       add every missing $key to en.json, guessed from its id
///   strings --template it   add every missing key and interface string to it.json, empty,
///                           ready to translate; drops entries nothing uses any more
/// </code>
/// </remarks>
public static class StringsCommand
{
    private static readonly JsonSerializerOptions Write = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static int Run(string[] args)
    {
        var dataRoot = Program.ResolveDataRoot(args);
        var repo = Path.GetFullPath(Path.Combine(dataRoot, "..", ".."));
        var stringsDir = Path.Combine(dataRoot, "strings");

        var contentKeys = Directory.EnumerateFiles(dataRoot, "*.json", SearchOption.AllDirectories)
            .Where(f => !f.StartsWith(stringsDir, StringComparison.OrdinalIgnoreCase))
            .SelectMany(f => StringScanner.ContentKeys(File.ReadAllText(f)))
            .ToHashSet(StringComparer.Ordinal);

        var interfaceText = new HashSet<string>(StringComparer.Ordinal);
        var problems = new List<string>();

        foreach (var dir in new[] { Path.Combine(repo, "src", "Kiln.Core"), Path.Combine(repo, "game", "scripts") })
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;

                var code = File.ReadAllText(file);

                interfaceText.UnionWith(StringScanner.InterfaceStrings(code));

                foreach (var line in StringScanner.InterpolatedCalls(code))
                {
                    problems.Add($"{Path.GetRelativePath(repo, file)}:{line}: interpolated string handed to L10n — use L10n.F(\"… {{0}} …\", value)");
                }
            }
        }

        var load = ContentLoader.LoadFromDirectory(dataRoot);
        var english = load.Database.StringsFor(L10n.English);

        if (args.Contains("--seed-en"))
        {
            var table = english.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            var added = 0;

            foreach (var key in contentKeys.Where(k => !table.ContainsKey(k)))
            {
                table[key] = L10n.Guess(key);
                added++;
            }

            Save(Path.Combine(stringsDir, "en.json"), table.OrderBy(p => p.Key, StringComparer.Ordinal));
            Console.WriteLine($"en.json: added {added} guessed name(s). Read them over: a guess is only the id.");
            english = table;
        }

        if (Program.OptionValue(args, "--template") is { } language && language != L10n.English)
        {
            var existing = load.Database.StringsFor(language);
            var wanted = contentKeys.OrderBy(k => k, StringComparer.Ordinal)
                .Concat(interfaceText.OrderBy(k => k, StringComparer.Ordinal));

            Save(Path.Combine(stringsDir, $"{language}.json"),
                wanted.Select(k => new KeyValuePair<string, string>(k, existing.GetValueOrDefault(k, ""))));

            Console.WriteLine($"{language}.json: {wanted.Count()} entries, "
                              + $"{wanted.Count(k => existing.GetValueOrDefault(k, "").Length > 0)} translated.");
        }

        // -- report
        var missingEnglish = contentKeys.Where(k => !english.ContainsKey(k)).Order(StringComparer.Ordinal).ToList();

        Console.WriteLine($"{contentKeys.Count} content keys, {interfaceText.Count} interface strings.");

        foreach (var (code, table) in load.Database.Strings.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (code == L10n.English) continue;

            var all = contentKeys.Concat(interfaceText).ToList();
            var done = all.Count(k => table.GetValueOrDefault(k, "").Length > 0);
            var stale = table.Keys.Count(k => !contentKeys.Contains(k) && !interfaceText.Contains(k));

            foreach (var (source, text) in table.Where(p => !p.Key.StartsWith('$') && p.Value.Length > 0))
            {
                if (!StringScanner.SamePlaceholders(source, text))
                {
                    problems.Add($"{code}.json: \"{text}\" does not keep the placeholders of \"{source}\"");
                }
            }

            Console.WriteLine($"  {code}: {done}/{all.Count} translated ({100.0 * done / Math.Max(1, all.Count):0}%)"
                              + (stale > 0 ? $", {stale} no longer used" : ""));
        }

        foreach (var key in missingEnglish) problems.Add($"en.json has no text for {key} (run: strings --seed-en)");

        foreach (var problem in problems) Console.Error.WriteLine($"  ERROR {problem}");

        return problems.Count == 0 ? 0 : 1;
    }

    private static void Save(string path, IEnumerable<KeyValuePair<string, string>> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var ordered = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, value) in entries) ordered[key] = value;

        File.WriteAllText(path, JsonSerializer.Serialize(ordered, Write).ReplaceLineEndings("\n") + "\n");
    }
}
