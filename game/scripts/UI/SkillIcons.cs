using System.Collections.Generic;
using Godot;

namespace Kiln.Game.UI;

/// <summary>
/// The picture for each skill (REF-19): <c>res://assets/icons/skills/&lt;skill id&gt;.svg</c>,
/// white shapes tinted in game like the item icons. A skill with no picture yet is drawn with
/// its name instead, so a missing file is never an empty square.
/// </summary>
public static class SkillIcons
{
    private static readonly Dictionary<string, Texture2D?> Loaded = new(System.StringComparer.Ordinal);

    public static Texture2D? For(string skillId)
    {
        if (skillId.Length == 0) return null;

        if (Loaded.TryGetValue(skillId, out var known)) return known;

        var path = $"res://assets/icons/skills/{skillId}.svg";
        var texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;

        Loaded[skillId] = texture;

        return texture;
    }
}
