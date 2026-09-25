using Godot;
using Kiln.Core.Foundation;
using Kiln.Game.World;

namespace Kiln.Game.UI;

/// <summary>
/// The minimap, top right (REF-19), in the original's shape: a round window on the ground
/// around the player under a plaque with the map's name, and a zoom either side.
/// </summary>
/// <remarks>
/// The same drawing as the full map (M), centred on the player: the ground walked, what stands
/// on it, the shrines, the villagers, the borders and the quest marker — and, only here, the
/// creatures close by as red dots. North is up, as on the full map; the player's arrow turns.
/// </remarks>
public partial class Minimap : CanvasLayer
{
    public const float Diameter = 190f;

    /// <summary>How far down the screen the minimap reaches: where the quest panel starts.</summary>
    public const float Bottom = 16f + 30f + Diameter + 12f;

    private const float MinRadius = 24f;
    private const float MaxRadius = 80f;

    private static readonly Color Gold = new(0.78f, 0.64f, 0.36f);

    private MapView _view = null!;
    private Label _name = null!;
    private string _zone = "";

    public override void _Ready()
    {
        Layer = 12;

        var root = new Control
        {
            AnchorLeft = 1,
            AnchorRight = 1,
            OffsetLeft = -(Diameter + 24),
            OffsetRight = -16,
            OffsetTop = 16,
            OffsetBottom = Bottom,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        AddChild(root);

        // The plaque with the map's name and its levels.
        var plaque = new PanelContainer
        {
            Position = new Vector2(8, 0),
            Size = new Vector2(Diameter, 26),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        plaque.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.08f, 0.06f, 0.92f),
            BorderColor = Gold,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
        });

        root.AddChild(plaque);

        _name = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ClipText = true,
        };

        _name.AddThemeFontSizeOverride("font_size", 13);
        _name.AddThemeColorOverride("font_color", new Color(0.96f, 0.86f, 0.58f));
        plaque.AddChild(_name);

        // The round window: a disc that clips the map drawn inside it.
        var disc = new Panel
        {
            Position = new Vector2(8, 30),
            Size = new Vector2(Diameter, Diameter),
            ClipChildren = CanvasItem.ClipChildrenMode.Only,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        disc.AddThemeStyleboxOverride("panel", Round(new Color(0.07f, 0.075f, 0.09f), 0, Colors.Transparent));
        root.AddChild(disc);

        _view = new MapView { Follow = true, MouseFilter = Control.MouseFilterEnum.Ignore };
        _view.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        disc.AddChild(_view);

        // The rim over it.
        var rim = new Panel
        {
            Position = disc.Position,
            Size = disc.Size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        rim.AddThemeStyleboxOverride("panel", Round(Colors.Transparent, 3, Gold));
        root.AddChild(rim);

        // Zoom, on the rim's lower corners, as in the original.
        root.AddChild(Zoom("+", new Vector2(25, 30 + 95 + 56), -8f));
        root.AddChild(Zoom("−", new Vector2(Diameter - 30, 30 + 95 + 56), 8f));
    }

    private Button Zoom(string text, Vector2 at, float step)
    {
        var button = new Button
        {
            Text = text,
            Position = at,
            Size = new Vector2(22, 22),
            FocusMode = Control.FocusModeEnum.None,
        };

        button.AddThemeFontSizeOverride("font_size", 13);
        button.AddThemeStyleboxOverride("normal", Round(new Color(0.12f, 0.10f, 0.07f, 0.95f), 1, Gold));
        button.AddThemeStyleboxOverride("hover", Round(new Color(0.22f, 0.18f, 0.10f, 0.95f), 1, Gold));
        button.AddThemeStyleboxOverride("pressed", Round(new Color(0.30f, 0.24f, 0.12f, 0.95f), 1, Gold));

        button.Pressed += () =>
        {
            _view.ViewRadius = Mathf.Clamp(_view.ViewRadius + step, MinRadius, MaxRadius);
            _view.QueueRedraw();
        };

        return button;
    }

    public override void _Process(double delta)
    {
        if (!GameWorld.IsLoaded || GameWorld.CurrentZoneId == _zone) return;

        _zone = GameWorld.CurrentZoneId;

        _name.Text = GameWorld.Graph[_zone] is { } zone
            ? $"{Items.GameItems.Localise(zone.Name)}  ·  {L10n.F("Lv. {0}", zone.Band)}"
            : "";
    }

    private static StyleBoxFlat Round(Color fill, int border, Color edge)
    {
        var radius = (int)(Diameter / 2);

        return new StyleBoxFlat
        {
            BgColor = fill,
            DrawCenter = fill.A > 0,
            BorderColor = edge,
            BorderWidthTop = border,
            BorderWidthBottom = border,
            BorderWidthLeft = border,
            BorderWidthRight = border,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            CornerDetail = 24,
            AntiAliasing = true,
        };
    }
}
