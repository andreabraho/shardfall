using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Kiln.Core.Saving;

/// <summary>What happened when a save was read.</summary>
public enum SaveReadStatus
{
    Ok,

    /// <summary>The bytes do not match the checksum written with them (FR-11.3).</summary>
    Corrupt,

    /// <summary>Not a save file, or not one this build can parse.</summary>
    Unreadable,

    /// <summary>Written by a newer build than this one. Loading it would silently drop fields.</summary>
    TooNew,
}

public sealed record SaveReadResult(SaveReadStatus Status, SaveGame? Save, string Message)
{
    public bool Ok => Status == SaveReadStatus.Ok && Save is not null;
}

/// <summary>
/// Turns a <see cref="SaveGame"/> into text and back, with a version and a checksum
/// (FR-11.2, FR-11.3).
/// </summary>
/// <remarks>
/// The file is two parts: one header line, then the save itself as indented JSON. The
/// checksum covers the second part byte for byte, which is why it is a separate line rather
/// than a field inside the object — hashing a document that contains its own hash means
/// re-serialising it to check it, and a serialiser that orders one field differently next
/// year would report every old save as corrupt.
/// <para>
/// The body stays readable on purpose. When a tester says "my save broke", opening the file
/// and seeing what is in it is worth more than the bytes a binary format would save.
/// </para>
/// </remarks>
public static class SaveCodec
{
    /// <summary>
    /// The version this build writes. Raise it whenever a field changes meaning, and add a step
    /// to <see cref="Migrations"/> that brings the previous version up to it.
    /// </summary>
    public const int CurrentVersion = 1;

    private const string Magic = "kiln_save";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = null,
        WriteIndented = true,
    };

    /// <summary>
    /// Steps that bring a save from one version to the next, keyed by the version they start
    /// from (FR-11.2). Empty while there has only ever been one version.
    /// </summary>
    /// <remarks>
    /// Each works on the raw JSON rather than on <see cref="SaveGame"/>, because the whole point
    /// of a migration is that the old shape no longer deserialises into the new type.
    /// </remarks>
    private static readonly Dictionary<int, Action<JsonObject>> Migrations = new();

    public static string Encode(SaveGame save)
    {
        save.SaveVersion = CurrentVersion;

        var body = JsonSerializer.Serialize(save, Options);
        var header = new JsonObject
        {
            [Magic] = CurrentVersion,
            ["checksum"] = Hash(body),
        };

        return header.ToJsonString() + "\n" + body;
    }

    public static SaveReadResult Decode(string text)
    {
        var split = text.IndexOf('\n');

        if (split < 0) return Fail(SaveReadStatus.Unreadable, "not a save file (no header line)");

        var body = text[(split + 1)..];
        JsonNode? header;

        try
        {
            header = JsonNode.Parse(text[..split]);
        }
        catch (JsonException)
        {
            return Fail(SaveReadStatus.Unreadable, "not a save file (header is not JSON)");
        }

        var version = header?[Magic]?.GetValue<int>();
        var checksum = header?["checksum"]?.GetValue<string>();

        if (version is null || checksum is null) return Fail(SaveReadStatus.Unreadable, "not a save file (no version or checksum)");

        if (!string.Equals(Hash(body), checksum, StringComparison.OrdinalIgnoreCase))
        {
            return Fail(SaveReadStatus.Corrupt,
                "the save does not match its checksum — it was damaged or edited by hand");
        }

        if (version > CurrentVersion)
        {
            return Fail(SaveReadStatus.TooNew,
                $"the save is version {version}, and this build only understands up to {CurrentVersion}");
        }

        try
        {
            if (JsonNode.Parse(body) is not JsonObject root) return Fail(SaveReadStatus.Unreadable, "the save body is not an object");

            for (var from = version.Value; from < CurrentVersion; from++)
            {
                if (!Migrations.TryGetValue(from, out var step))
                {
                    return Fail(SaveReadStatus.Unreadable, $"no migration from version {from}");
                }

                step(root);
            }

            var save = root.Deserialize<SaveGame>(Options);

            return save is null
                ? Fail(SaveReadStatus.Unreadable, "the save body is empty")
                : new SaveReadResult(SaveReadStatus.Ok, save, "ok");
        }
        catch (JsonException ex)
        {
            return Fail(SaveReadStatus.Unreadable, $"the save body could not be read: {ex.Message}");
        }
    }

    private static SaveReadResult Fail(SaveReadStatus status, string message) => new(status, null, message);

    private static string Hash(string body) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)));
}
