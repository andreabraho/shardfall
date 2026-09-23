using Godot;

namespace Kiln.Game.UI;

/// <summary>
/// The card that appears above a hotbar slot while the mouse rests on it.
/// </summary>
/// <remarks>
/// Its own panel rather than the engine's tooltip. A tooltip follows the cursor, appears after
/// a delay and is drawn wherever it fits — which, for a bar along the bottom of the screen,
/// means over the bar itself or off the edge. This sits directly above the slot being pointed
/// at, immediately, so reading what a key does never covers the keys next to it.
/// </remarks>
public partial class SkillTip : PanelContainer
{
    /// <summary>Gap between the card and the slot it belongs to.</summary>
    private const float Clearance = 10f;

    private Label _text = null!;

    public override void _Ready()
    {
        // It is a label on a panel, not a target: it must never eat a click meant for the bar.
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        ZIndex = 10;

        AddThemeStyleboxOverride("panel", SkillText.Panel());

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        AddChild(margin);

        _text = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(280, 0),
        };

        _text.AddThemeFontSizeOverride("font_size", 13);
        margin.AddChild(_text);
    }

    /// <summary>Shows the card centred above <paramref name="slot"/>.</summary>
    public void Show(string text, Control slot)
    {
        if (text.Length == 0)
        {
            Hide();
            return;
        }

        _text.Text = text;
        Visible = true;

        // Deferred: the card has just been given new text, and its size is only known once
        // the container has laid it out. Placed on the same frame it would be positioned
        // from the size the previous skill's text happened to need.
        CallDeferred(nameof(Place), slot);
    }

    private void Place(Control slot)
    {
        if (!IsInstanceValid(slot) || !Visible) return;

        var rect = slot.GetGlobalRect();
        var size = Size;
        var viewport = GetViewportRect().Size;

        var x = rect.Position.X + ((rect.Size.X - size.X) / 2f);
        var y = rect.Position.Y - size.Y - Clearance;

        // Kept on screen: the slots at the ends of the bar would otherwise push the card
        // half out of the window.
        GlobalPosition = new Vector2(
            Mathf.Clamp(x, 8f, Mathf.Max(8f, viewport.X - size.X - 8f)),
            Mathf.Max(8f, y));
    }
}
