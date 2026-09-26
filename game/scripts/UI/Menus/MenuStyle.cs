using Godot;
using Kiln.Core.Combat;
using Kiln.Core.Foundation;

namespace Kiln.Game.UI.Menus;

/// <summary>
/// The look shared by the main menu, the pause menu and every page they open — settings,
/// saves, characters, a new game — so none of them can drift.
/// </summary>
/// <remarks>
/// In the original's style, as the windows in play are (REF-19, 2026-09-26 at your call): a dark
/// body in a gold frame, the window's name on a brown title bar, sections under gold headings
/// with a line beneath, buttons as dark plates edged in bronze that light gold under the mouse.
/// The controls inside a window — lists, sliders, switches, the name field, the scroll bar —
/// take the same colours from one theme put on the window, rather than one by one.
/// </remarks>
public static class MenuStyle
{
    public static readonly Color Gold = new(0.96f, 0.86f, 0.58f);
    public static readonly Color Text = new(0.90f, 0.88f, 0.82f);
    public static readonly Color Dim = new(0.55f, 0.53f, 0.48f);
    public static readonly Color Heading = new(0.85f, 0.72f, 0.45f);

    /// <summary>The gold of a window's frame.</summary>
    public static readonly Color Frame = new(0.62f, 0.50f, 0.28f);

    /// <summary>The bronze round a plate.</summary>
    private static readonly Color Edge = new(0.30f, 0.25f, 0.18f);

    private static readonly Color Body = new(0.06f, 0.055f, 0.05f, 0.96f);
    private static readonly Color Inset = new(0.045f, 0.042f, 0.04f);

    private static Theme? _theme;

    /// <summary>A window's body without its bars: dark, in a gold frame, with room inside.</summary>
    public static StyleBoxFlat Panel(float alpha = 0.96f) => Box(Body with { A = alpha }, 2, 20);

    /// <summary>
    /// A window in the original's style: a title bar with the window's name (and a cross, when it
    /// can be closed), a body, and a strip along the bottom when there is something to say there.
    /// </summary>
    /// <param name="title">Its name, on the title bar; the label is handed back to be renamed.</param>
    /// <param name="close">What the cross does, or null for no cross.</param>
    /// <param name="body">Where its content goes.</param>
    /// <param name="heading">The title bar's label.</param>
    /// <param name="foot">A line for the bottom strip, or null for none.</param>
    public static PanelContainer Window(string title, System.Action? close, out VBoxContainer body, out Label heading, string? foot = null)
    {
        var root = new PanelContainer { Theme = Theme() };
        root.AddThemeStyleboxOverride("panel", Box(Body, 2, 0));

        var outer = new VBoxContainer();
        outer.AddThemeConstantOverride("separation", 0);
        root.AddChild(outer);

        var bar = new PanelContainer();
        bar.AddThemeStyleboxOverride("panel", Box(new Color(0.20f, 0.14f, 0.07f), 0, 8, bottom: 1));
        outer.AddChild(bar);

        var barRow = new HBoxContainer();
        bar.AddChild(barRow);

        heading = new Label { Text = title, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
        heading.AddThemeFontSizeOverride("font_size", 15);
        heading.AddThemeColorOverride("font_color", Gold);
        barRow.AddChild(heading);

        if (close is not null)
        {
            var cross = new Button { Text = "✕", Flat = true, FocusMode = Control.FocusModeEnum.None };
            cross.AddThemeFontSizeOverride("font_size", 13);
            cross.AddThemeColorOverride("font_color", Text);
            cross.AddThemeColorOverride("font_hover_color", Gold);
            cross.Pressed += close;
            barRow.AddChild(cross);
        }

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        outer.AddChild(margin);

        body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        margin.AddChild(body);

        if (foot is null) return root;

        var strip = new PanelContainer();
        strip.AddThemeStyleboxOverride("panel", Box(new Color(0.10f, 0.08f, 0.06f), 0, 8, top: 1));
        outer.AddChild(strip);

        var line = Label(foot, 11, Dim, wrap: true);
        line.CustomMinimumSize = new Vector2(1, 0);
        strip.AddChild(line);

        return root;
    }

    /// <summary>A section's gold heading with a thin line under it, as the windows in play have.</summary>
    public static Control Section(string text)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);

        box.AddChild(Label(text, 14, Heading));
        box.AddChild(new ColorRect { Color = new Color(Frame, 0.6f), CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore });

        return box;
    }

    /// <summary>A menu's own button: a wide plate.</summary>
    public static Button Big(string text, System.Action pressed)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(260, 40),
            FocusMode = Control.FocusModeEnum.None,
        };

        Plate(button, 16);
        button.Pressed += pressed;

        return button;
    }

    public static Button Small(string text, System.Action pressed)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(110, 30), FocusMode = Control.FocusModeEnum.None };

        Plate(button, 13);
        button.Pressed += pressed;

        return button;
    }

    /// <summary>A button as one of the window's plates: dark, edged in bronze, lit gold under the mouse.</summary>
    public static void Plate(Button button, int size = 13)
    {
        button.AddThemeStyleboxOverride("normal", PlateBox(new Color(0.10f, 0.09f, 0.08f), Edge));
        button.AddThemeStyleboxOverride("hover", PlateBox(new Color(0.17f, 0.13f, 0.08f), Frame));
        button.AddThemeStyleboxOverride("pressed", PlateBox(new Color(0.22f, 0.16f, 0.08f), Gold));
        button.AddThemeStyleboxOverride("disabled", PlateBox(new Color(0.07f, 0.065f, 0.06f), new Color(0.20f, 0.18f, 0.15f)));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeColorOverride("font_color", Text);
        button.AddThemeColorOverride("font_hover_color", Gold);
        button.AddThemeColorOverride("font_pressed_color", Gold);
        button.AddThemeColorOverride("font_disabled_color", Dim);
        button.AddThemeFontSizeOverride("font_size", size);
    }

    public static Label Label(string text, int size, Color colour, bool wrap = false)
    {
        var label = new Label { Text = text };

        if (wrap) label.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", colour);

        return label;
    }

    /// <summary>
    /// The colours of everything else in a window: the lists that drop down, the sliders, the
    /// switches, the name field, the scroll bar, the tooltips.
    /// </summary>
    public static Theme Theme()
    {
        if (_theme is not null) return _theme;

        var theme = new Theme();

        foreach (var type in new[] { "Button", "OptionButton", "MenuButton" })
        {
            theme.SetStylebox("normal", type, PlateBox(new Color(0.10f, 0.09f, 0.08f), Edge));
            theme.SetStylebox("hover", type, PlateBox(new Color(0.17f, 0.13f, 0.08f), Frame));
            theme.SetStylebox("pressed", type, PlateBox(new Color(0.22f, 0.16f, 0.08f), Gold));
            theme.SetStylebox("disabled", type, PlateBox(new Color(0.07f, 0.065f, 0.06f), new Color(0.20f, 0.18f, 0.15f)));
            theme.SetStylebox("focus", type, new StyleBoxEmpty());
            theme.SetColor("font_color", type, Text);
            theme.SetColor("font_hover_color", type, Gold);
            theme.SetColor("font_pressed_color", type, Gold);
            theme.SetColor("font_hover_pressed_color", type, Gold);
            theme.SetColor("font_focus_color", type, Text);
            theme.SetColor("font_disabled_color", type, Dim);
            theme.SetFontSize("font_size", type, 14);
        }

        // A switch is its knob, with no plate round it.
        foreach (var name in new[] { "normal", "hover", "pressed", "disabled", "focus", "hover_pressed" })
        {
            theme.SetStylebox(name, "CheckButton", new StyleBoxEmpty());
        }

        theme.SetColor("font_color", "CheckButton", Text);
        theme.SetColor("font_hover_color", "CheckButton", Gold);
        theme.SetColor("font_pressed_color", "CheckButton", Text);

        // The name field: an inset, gold-edged while typing in it.
        theme.SetStylebox("normal", "LineEdit", Box(Inset, 1, 8, edge: Edge));
        theme.SetStylebox("focus", "LineEdit", Box(Inset, 1, 8, edge: Gold));
        theme.SetColor("font_color", "LineEdit", Text);
        theme.SetColor("font_placeholder_color", "LineEdit", Dim);
        theme.SetColor("caret_color", "LineEdit", Gold);
        theme.SetColor("selection_color", "LineEdit", new Color(Frame, 0.5f));

        // The list a choice drops down.
        theme.SetStylebox("panel", "PopupMenu", Box(Body with { A = 0.98f }, 1, 6));
        theme.SetStylebox("hover", "PopupMenu", Box(new Color(0.22f, 0.16f, 0.08f), 0, 4));
        theme.SetColor("font_color", "PopupMenu", Text);
        theme.SetColor("font_hover_color", "PopupMenu", Gold);
        theme.SetColor("font_disabled_color", "PopupMenu", Dim);
        theme.SetFontSize("font_size", "PopupMenu", 14);

        // Sliders: a dark groove, filled bronze up to the knob.
        theme.SetStylebox("slider", "HSlider", Groove(Inset, Edge));
        theme.SetStylebox("grabber_area", "HSlider", Groove(new Color(0.46f, 0.36f, 0.18f), new Color(0.46f, 0.36f, 0.18f)));
        theme.SetStylebox("grabber_area_highlight", "HSlider", Groove(Frame, Frame));

        // The scroll bar: thin, bronze on dark.
        theme.SetStylebox("scroll", "VScrollBar", Thin(Inset));
        theme.SetStylebox("grabber", "VScrollBar", Thin(new Color(0.36f, 0.29f, 0.17f)));
        theme.SetStylebox("grabber_highlight", "VScrollBar", Thin(Frame));
        theme.SetStylebox("grabber_pressed", "VScrollBar", Thin(Gold));

        theme.SetStylebox("separator", "HSeparator", new StyleBoxLine { Color = new Color(Frame, 0.6f), Thickness = 1 });

        theme.SetStylebox("panel", "TooltipPanel", Box(Body with { A = 0.98f }, 1, 8));
        theme.SetColor("font_color", "TooltipLabel", Text);

        _theme = theme;
        return theme;
    }

    private static StyleBoxFlat PlateBox(Color fill, Color edge) => Box(fill, 1, 8, edge: edge);

    private static StyleBoxFlat Groove(Color fill, Color edge) => new()
    {
        BgColor = fill,
        BorderColor = edge,
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        ContentMarginTop = 3,
        ContentMarginBottom = 3,
    };

    private static StyleBoxFlat Thin(Color fill) => new()
    {
        BgColor = fill,
        ContentMarginLeft = 4,
        ContentMarginRight = 4,
    };

    public static StyleBoxFlat Box(Color fill, int width, int margin, int bottom = -1, int top = -1, Color? edge = null) => new()
    {
        BgColor = fill,
        BorderColor = edge ?? Frame,
        BorderWidthTop = top >= 0 ? top : width,
        BorderWidthLeft = width,
        BorderWidthRight = width,
        BorderWidthBottom = bottom >= 0 ? bottom : width,
        ContentMarginLeft = margin + 4,
        ContentMarginRight = margin,
        ContentMarginTop = margin * 0.6f,
        ContentMarginBottom = margin * 0.6f,
    };

    /// <summary>What a difficulty tier means, in the words the choice is made in.</summary>
    public static string Describe(DifficultySettings d) => d.Tier switch
    {
        Core.Foundation.Difficulty.Wanderer => L10n.T("For the world and the story."),
        Core.Foundation.Difficulty.Disciple => L10n.T("The game as designed."),
        Core.Foundation.Difficulty.Adept => L10n.T("For players who know the genre."),
        _ => L10n.T("Nightmare. No checkpoints inside the Demon Tower."),
    } + " "
      + L10n.F("Enemies have ×{0:0.##} health and deal ×{1:0.##} damage; {2:0.0#} s to step out of an attack; {3} flask charges;",
          d.EnemyHpMultiplier, d.EnemyDamageMultiplier, d.AoeTelegraphSeconds, d.FlaskCharges) + " "
      + (d.ExperienceLossOnDeath > 0
          ? L10n.F("dying costs {0:P0} of the experience toward the next level.", d.ExperienceLossOnDeath)
          : L10n.T("dying costs nothing."));
}
