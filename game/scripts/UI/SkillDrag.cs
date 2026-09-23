using Godot;
using Kiln.Core.Foundation;

namespace Kiln.Game.UI;

/// <summary>
/// What a skill being dragged looks like, and what it carries (REF-03).
/// </summary>
/// <remarks>
/// Shared by the two ends of the gesture — the skill screen a skill is dragged from and the
/// bar it is dropped on — so both agree on the shape of the payload. A drag that carried a
/// bare string would be accepted by every control in the game that takes a drop.
/// </remarks>
public static class SkillDrag
{
    /// <summary>Marks the payload as ours, so nothing else accepts it.</summary>
    private const string Marker = "kiln_skill";

    public static Variant Data(string skillId)
    {
        var payload = new Godot.Collections.Dictionary
        {
            ["kind"] = Marker,
            ["skill"] = skillId,
        };

        return payload;
    }

    /// <summary>The skill in a payload, or null when it is not one of ours.</summary>
    public static string? SkillOf(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary) return null;

        var payload = data.AsGodotDictionary();

        if (!payload.TryGetValue("kind", out var kind) || kind.AsString() != Marker) return null;

        var skill = payload.TryGetValue("skill", out var id) ? id.AsString() : "";

        return skill.Length > 0 ? skill : null;
    }

    /// <summary>The label that follows the cursor while a skill is being carried.</summary>
    public static Control Preview(string name)
    {
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };

        panel.AddThemeStyleboxOverride("panel", SkillText.Panel());

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        panel.AddChild(margin);

        var label = new Label { Text = name.Length > 0 ? name : L10n.T("Skill") };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.75f));
        margin.AddChild(label);

        return panel;
    }
}
