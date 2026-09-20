using Godot;

namespace Kiln.Game.UI;

/// <summary>
/// A line of text the world says to the player: why a gate refused them, what a border was.
/// </summary>
/// <remarks>
/// Its own widget rather than a message pushed through the shrine panel or the bag. Those
/// each belong to a thing the player opened; this belongs to the world, appears without being
/// asked for, and has to work in a scene where neither of them happens to be present.
/// </remarks>
public partial class WorldNotice : CanvasLayer
{
    private const double HoldSeconds = 3.5;
    private const string NodeName = "WorldNotice";

    private Label _label = null!;
    private double _showing;

    public override void _Ready()
    {
        Layer = 90;

        _label = new Label
        {
            Name = "Text",
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1, 1, 1, 0),
        };

        _label.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _label.AnchorLeft = 0;
        _label.AnchorRight = 1;
        _label.OffsetTop = 96;
        _label.AddThemeColorOverride("font_color", new Color(0.93f, 0.86f, 0.68f));
        _label.AddThemeFontSizeOverride("font_size", 18);

        AddChild(_label);
    }

    public override void _Process(double delta)
    {
        if (_showing <= 0) return;

        _showing -= delta;

        // Held at full opacity, then faded over the last second: a line that starts fading
        // immediately is one the player has to hurry to read.
        _label.Modulate = new Color(1, 1, 1, (float)Mathf.Clamp(_showing, 0, 1));
    }

    public void Say(string message)
    {
        _label.Text = message;
        _showing = HoldSeconds;
        _label.Modulate = new Color(1, 1, 1, 1);
    }

    /// <summary>
    /// Says something through the current scene's notice, adding one if the scene has none.
    /// </summary>
    /// <remarks>
    /// Created on demand so a zone scene needs no particular node to be able to speak — a map
    /// that silently swallowed the reason a gate turned the player away would be worse than
    /// one with no gate at all.
    /// </remarks>
    public static void Show(SceneTree tree, string message)
    {
        if (tree.CurrentScene is not { } scene) return;

        if (scene.GetNodeOrNull<WorldNotice>(NodeName) is { } existing)
        {
            existing.Say(message);
            return;
        }

        var notice = new WorldNotice { Name = NodeName };
        scene.AddChild(notice);
        notice.Say(message);
    }
}
