using Godot;
using Kiln.Core.Combat;

namespace Kiln.Game.UI.Menus;

/// <summary>The look shared by the main menu and the pause menu, so the two cannot drift.</summary>
public static class MenuStyle
{
    public static readonly Color Gold = new(0.96f, 0.86f, 0.58f);
    public static readonly Color Text = new(0.88f, 0.86f, 0.8f);
    public static readonly Color Dim = new(0.6f, 0.6f, 0.58f);
    public static readonly Color Heading = new(0.78f, 0.82f, 0.90f);

    public static StyleBoxFlat Panel(float alpha = 0.97f) => new()
    {
        BgColor = new Color(0.07f, 0.08f, 0.10f, alpha),
        BorderColor = new Color(0.30f, 0.28f, 0.24f),
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        CornerRadiusTopLeft = 4,
        CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4,
        CornerRadiusBottomRight = 4,
        ContentMarginLeft = 24,
        ContentMarginRight = 24,
        ContentMarginTop = 20,
        ContentMarginBottom = 20,
    };

    public static Button Big(string text, System.Action pressed)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(260, 46),
            Alignment = HorizontalAlignment.Left,
        };

        button.AddThemeFontSizeOverride("font_size", 18);
        button.Pressed += pressed;

        return button;
    }

    public static Button Small(string text, System.Action pressed)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(110, 32) };

        button.AddThemeFontSizeOverride("font_size", 14);
        button.Pressed += pressed;

        return button;
    }

    public static Label Label(string text, int size, Color colour, bool wrap = false)
    {
        var label = new Label { Text = text };

        if (wrap) label.AutowrapMode = TextServer.AutowrapMode.WordSmart;

        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", colour);

        return label;
    }

    /// <summary>What a difficulty tier means, in the words the choice is made in.</summary>
    public static string Describe(DifficultySettings d) => d.Tier switch
    {
        Core.Foundation.Difficulty.Wanderer => "For the world and the story. ",
        Core.Foundation.Difficulty.Disciple => "The game as designed. ",
        Core.Foundation.Difficulty.Adept => "For players who know the genre. ",
        _ => "Nightmare. No checkpoints inside the catacombs. ",
    } + $"Enemies have ×{d.EnemyHpMultiplier:0.##} health and deal ×{d.EnemyDamageMultiplier:0.##} damage; "
      + $"{d.AoeTelegraphSeconds:0.0#} s to step out of an attack; {d.FlaskCharges} flask charges; "
      + (d.YangLossOnDeath > 0 ? $"dying costs {d.YangLossOnDeath:P0} of carried yang." : "dying costs nothing.");
}
