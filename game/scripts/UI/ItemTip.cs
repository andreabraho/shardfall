using Godot;

namespace Kiln.Game.UI;

/// <summary>
/// The card that describes the item under the cursor (REF-10): shown at once, in colour, and
/// kept beside the cursor and on the screen.
/// </summary>
/// <remarks>
/// Godot's own tooltips wait before appearing and print plain text; a player reading gear
/// in the middle of a fight has neither the time nor the patience for either.
/// </remarks>
public partial class ItemTip : PanelContainer
{
    private const float Width = 300f;
    private static readonly Vector2 FromCursor = new(18, 16);

    private RichTextLabel _text = null!;

    public override void _Ready()
    {
        TopLevel = true;
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(Width, 0);

        AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.06f, 0.08f, 0.97f),
            BorderColor = new Color(0.32f, 0.35f, 0.42f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 9,
            ContentMarginBottom = 9,
        });

        _text = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(Width - 24, 0),
            MouseFilter = MouseFilterEnum.Ignore,
        };

        _text.AddThemeFontSizeOverride("normal_font_size", 13);
        _text.AddThemeFontSizeOverride("bold_font_size", 13);
        AddChild(_text);
    }

    /// <summary>Shows a description beside the cursor.</summary>
    public void Say(string bbcode)
    {
        _text.Text = bbcode;
        Visible = true;

        // Laid out again at the text's height, not the last card's.
        Size = new Vector2(Width, 0);
        Place();
    }

    public void Clear() => Visible = false;

    public override void _Process(double delta)
    {
        if (Visible) Place();
    }

    /// <summary>Beside the cursor; flipped to the other side where the screen runs out.</summary>
    private void Place()
    {
        var mouse = GetViewport().GetMousePosition();
        var screen = GetViewportRect().Size;
        var at = mouse + FromCursor;

        if (at.X + Size.X > screen.X) at.X = mouse.X - FromCursor.X - Size.X;
        if (at.Y + Size.Y > screen.Y) at.Y = Mathf.Max(0, screen.Y - Size.Y);

        Position = at;
    }
}
