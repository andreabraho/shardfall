using Godot;
using Kiln.Core.Foundation;

namespace Kiln.Game.UI;

/// <summary>
/// The gold coin drawn before every sum of gan (REF-12).
/// </summary>
/// <remarks>
/// Painted once, pixel by pixel, rather than drawn from a file: a gold disc with a darker rim,
/// an inner ring and a highlight towards the top left — small enough to read at the size of a
/// line of text, and nothing to license.
/// </remarks>
public static class Coin
{
    private const int Side = 64;

    private static ImageTexture? _texture;

    public static Texture2D Texture => _texture ??= Paint();

    /// <summary>A coin and a sum beside it: "(coin) 12,000 gan".</summary>
    public static HBoxContainer Amount(long gan, int fontSize, Color colour)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 4);

        row.AddChild(new TextureRect
        {
            Texture = Texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(fontSize + 3, fontSize + 3),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });

        var label = new Label { Text = L10n.F("{0:N0} gan", gan), VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", colour);
        row.AddChild(label);

        return row;
    }

    private static ImageTexture Paint()
    {
        var image = Image.CreateEmpty(Side, Side, false, Image.Format.Rgba8);
        var centre = new Vector2((Side - 1) / 2f, (Side - 1) / 2f);
        var radius = (Side / 2f) - 2f;

        var face = new Color("f2c14e");
        var bright = new Color("fff0a8");
        var rim = new Color("a8741a");
        var ring = new Color("c9922a");

        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                var at = new Vector2(x, y);
                var d = at.DistanceTo(centre);

                if (d > radius + 1) continue;

                // Lit from the top left: brighter there, deeper towards the bottom right.
                var light = Mathf.Clamp(1f - (at.DistanceTo(centre + new Vector2(-radius * 0.45f, -radius * 0.45f)) / (radius * 1.8f)), 0f, 1f);
                var colour = face.Lerp(bright, light * 0.7f);

                if (d > radius - 4) colour = rim;
                else if (Mathf.Abs(d - (radius * 0.62f)) < 1.6f) colour = ring;

                // A soft edge, so the disc does not look cut out of paper.
                colour.A = Mathf.Clamp(radius + 1 - d, 0f, 1f);

                image.SetPixel(x, y, colour);
            }
        }

        return ImageTexture.CreateFromImage(image);
    }
}
