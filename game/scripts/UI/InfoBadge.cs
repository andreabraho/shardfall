using Godot;

namespace Kiln.Game.UI;

/// <summary>
/// A small round "i" beside a foe's name: hovered, it shows what helps against the foe
/// (<see cref="FoeAdvice"/>; 2026-10-07, at your call — the tooltip was there, and nobody could tell).
/// </summary>
public partial class InfoBadge : PanelContainer
{
    public const float Size = 18;

    public InfoBadge()
    {
        CustomMinimumSize = new Vector2(Size, Size);
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.Help;

        var round = new StyleBoxFlat
        {
            // The menus' plate: dark, edged in their gold (2026-10-07, at your call: not blue).
            BgColor = new Color(0.10f, 0.09f, 0.08f),
            BorderColor = Menus.MenuStyle.Frame,
        };
        round.SetBorderWidthAll(1);
        round.SetCornerRadiusAll((int)(Size / 2));
        AddThemeStyleboxOverride("panel", round);

        // Lit under the mouse, as a menu button is.
        MouseEntered += () => { round.BgColor = new Color(0.17f, 0.13f, 0.08f); round.BorderColor = Menus.MenuStyle.Gold; };
        MouseExited += () => { round.BgColor = new Color(0.10f, 0.09f, 0.08f); round.BorderColor = Menus.MenuStyle.Frame; };

        var letter = new Label
        {
            Text = "i",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        letter.AddThemeFontSizeOverride("font_size", 12);
        letter.AddThemeColorOverride("font_color", Menus.MenuStyle.Gold);
        AddChild(letter);
    }

    /// <summary>What the tooltip says; set every frame, written only when it changes.</summary>
    public string Advice
    {
        set
        {
            if (TooltipText != value) TooltipText = value;
        }
    }
}
