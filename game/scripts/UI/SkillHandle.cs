using Godot;
using Kiln.Core.Foundation;

namespace Kiln.Game.UI;

/// <summary>
/// The grip on a row of the skill screen: what the player takes hold of to drag a skill down
/// onto a key (REF-03).
/// </summary>
/// <remarks>
/// A grip rather than a draggable row. The row carries a button and wrapped text, and making
/// the whole thing draggable means every attempt to press "+" that moves a pixel becomes a
/// drag instead of a click. A small handle with a grip mark on it says where to take hold and
/// leaves the rest of the row alone. The mark is two plain colons: the fallback font carries
/// no braille, and a grip drawn as a missing-glyph box says nothing.
/// </remarks>
public partial class SkillHandle : PanelContainer
{
    private Label _key = null!;

    public string SkillId { get; set; } = "";

    public string SkillName { get; set; } = "";

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(78, 34);
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.Move;

        AddThemeStyleboxOverride("panel", SkillText.Panel());

        _key = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _key.AddThemeFontSizeOverride("font_size", 12);
        AddChild(_key);

        ShowKey(-1);
    }

    /// <summary>Says which key the skill is on, or that it is on none.</summary>
    public void ShowKey(int slot)
    {
        if (_key is null) return;

        _key.Text = slot >= 0 ? L10n.F(":: Key {0}", slot + 1) : L10n.T(":: drag to a key");
        _key.AddThemeColorOverride("font_color", slot >= 0
            ? new Color(1f, 0.92f, 0.75f)
            : new Color(0.55f, 0.60f, 0.66f));
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (SkillId.Length == 0) return default;

        SetDragPreview(SkillDrag.Preview(SkillName));

        return SkillDrag.Data(SkillId);
    }

    /// <summary>
    /// Dropping a skill back onto a handle takes it off the bar: the gesture that put it on a
    /// key, run backwards.
    /// </summary>
    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        SkillDrag.SkillOf(data) is not null;

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (SkillDrag.SkillOf(data) is { } skillId) EmitSignal(SignalName.SkillReturned, skillId);
    }

    /// <summary>Told when a skill is dragged off the bar and dropped back on the list.</summary>
    [Signal] public delegate void SkillReturnedEventHandler(string skillId);
}
