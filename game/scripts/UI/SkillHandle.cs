using Godot;
using Kiln.Core.Foundation;

namespace Kiln.Game.UI;

/// <summary>
/// A skill's icon on the skill screen: what the player takes hold of to drag the skill down
/// onto a key (REF-03; an icon since REF-19, as in the original).
/// </summary>
/// <remarks>
/// The icon rather than the whole row: the row carries a button, and making all of it
/// draggable means every attempt to press "+" that moves a pixel becomes a drag instead of a
/// click. A skill with no picture yet shows its initials. The key it sits on is written in the
/// corner.
/// </remarks>
public partial class SkillHandle : PanelContainer
{
    private const float Side = 44f;

    private static readonly Color Body = new(0.86f, 0.52f, 0.34f);
    private static readonly Color Mental = new(0.44f, 0.62f, 0.9f);

    private Label _key = null!;
    private TextureRect _picture = null!;
    private Label _initials = null!;
    private bool _learned;

    public string SkillId { get; set; } = "";

    public string SkillName { get; set; } = "";

    /// <summary>False for a skill that has its own key and cannot go on the bar (Guard Stance).</summary>
    public bool Draggable { get; set; } = true;

    /// <summary>Dim until a point is spent on it.</summary>
    public bool Learned
    {
        get => _learned;
        set
        {
            _learned = value;
            Tint();
        }
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(Side, Side);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.Move;

        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.045f, 0.04f),
            BorderColor = new Color(0.62f, 0.50f, 0.28f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
        });

        var stack = new Control { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(stack);

        _picture = new TextureRect
        {
            Texture = SkillIcons.For(SkillId),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };

        _picture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _picture.OffsetLeft = 5;
        _picture.OffsetTop = 5;
        _picture.OffsetRight = -5;
        _picture.OffsetBottom = -5;
        stack.AddChild(_picture);

        _initials = new Label
        {
            Text = Initials(SkillName),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visible = _picture.Texture is null,
            MouseFilter = MouseFilterEnum.Ignore,
        };

        _initials.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _initials.AddThemeFontSizeOverride("font_size", 15);
        stack.AddChild(_initials);

        _key = new Label { Position = new Vector2(2, -2), MouseFilter = MouseFilterEnum.Ignore };
        _key.AddThemeFontSizeOverride("font_size", 10);
        _key.AddThemeColorOverride("font_color", new Color(1f, 0.92f, 0.75f));
        _key.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        _key.AddThemeConstantOverride("outline_size", 3);
        stack.AddChild(_key);

        Tint();
    }

    private void Tint()
    {
        if (_picture is null) return;

        var tree = GameContent.IsLoaded && GameContent.Database.Skills.TryGetValue(SkillId, out var def) && def.Tree == "mental"
            ? Mental
            : Body;

        var colour = _learned ? tree.Lightened(0.25f) : new Color(0.42f, 0.42f, 0.44f);

        _picture.Modulate = colour;
        _initials.AddThemeColorOverride("font_color", colour);
    }

    private static string Initials(string name)
    {
        var words = name.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);

        return words.Length switch
        {
            0 => "?",
            1 => words[0][..1].ToUpperInvariant(),
            _ => (words[0][..1] + words[1][..1]).ToUpperInvariant(),
        };
    }

    /// <summary>
    /// Writes the key the skill is on in the corner: the bar slot, or a fixed key for a skill
    /// that has one of its own.
    /// </summary>
    public void ShowKey(int slot, string? fixedKey = null)
    {
        if (_key is null) return;

        _key.Text = fixedKey ?? (slot >= 0 ? (slot + 1).ToString() : "");
        TooltipText = !Draggable ? "" : slot >= 0
            ? L10n.F("On key {0}. Drag it back here to take it off.", slot + 1)
            : L10n.T("Drag onto the bar to put it on a key.");
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (!Draggable || SkillId.Length == 0) return default;

        SetDragPreview(SkillDrag.Preview(SkillName));

        return SkillDrag.Data(SkillId);
    }

    /// <summary>
    /// Dropping a skill back onto an icon takes it off the bar: the gesture that put it on a
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
