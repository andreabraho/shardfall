using System.Collections.Generic;
using Godot;

namespace Kiln.Game.UI;

/// <summary>
/// Lets a window be moved by holding the mouse on its border or its title band and dragging
/// (REF-19, at your call).
/// </summary>
/// <remarks>
/// The press is taken only near the edge or along the top, never in the middle, so clicking
/// an item, a button or empty space inside a window does what it did before. Where a window
/// was put is remembered for the rest of the session, across maps, by name.
/// </remarks>
public static class PanelMover
{
    /// <summary>How close to the edge a press counts as "on the border", in pixels.</summary>
    public const float Edge = 10f;

    /// <summary>The band along the top that drags too: the title.</summary>
    public const float TitleBand = 34f;

    private static readonly Dictionary<string, Vector2> Placed = new(System.StringComparer.Ordinal);

    /// <summary>Makes <paramref name="window"/> movable. <paramref name="key"/> names it for the session.</summary>
    public static void Attach(Control window, string key)
    {
        var dragging = false;
        var grab = Vector2.Zero;

        if (window.MouseFilter == Control.MouseFilterEnum.Ignore) window.MouseFilter = Control.MouseFilterEnum.Stop;

        // The frames inside a window — its title bar above all — stop the mouse by default, so a
        // press on them never reached the window. Once the window is built, they pass it on.
        Callable.From(() => PassThrough(window)).CallDeferred();

        window.GuiInput += @event =>
        {
            switch (@event)
            {
                case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press when OnBorder(window, press.Position):
                    dragging = true;
                    grab = press.Position;
                    window.AcceptEvent();
                    break;

                case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } when dragging:
                    dragging = false;
                    Placed[key] = window.GlobalPosition;
                    window.AcceptEvent();
                    break;

                case InputEventMouseMotion motion when dragging:
                    window.GlobalPosition = Clamp(window, window.GlobalPosition + motion.Position - grab);
                    window.AcceptEvent();
                    break;
            }
        };

        // Put back where it was left, once the window has its size.
        if (Placed.TryGetValue(key, out var at))
        {
            window.Ready += () => Callable.From(() => window.GlobalPosition = Clamp(window, at)).CallDeferred();

            if (window.IsInsideTree()) Callable.From(() => window.GlobalPosition = Clamp(window, at)).CallDeferred();
        }
    }

    private static void PassThrough(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is PanelContainer { MouseFilter: Control.MouseFilterEnum.Stop } frame) frame.MouseFilter = Control.MouseFilterEnum.Pass;

            PassThrough(child);
        }
    }

    private static bool OnBorder(Control window, Vector2 at)
    {
        var size = window.Size;

        return at.X <= Edge || at.Y <= TitleBand || at.X >= size.X - Edge || at.Y >= size.Y - Edge;
    }

    /// <summary>Keeps at least part of the window on screen, so it can always be taken back.</summary>
    private static Vector2 Clamp(Control window, Vector2 at)
    {
        var screen = window.GetViewportRect().Size;
        var size = window.Size;

        return new Vector2(
            Mathf.Clamp(at.X, 0, Mathf.Max(0, screen.X - size.X)),
            Mathf.Clamp(at.Y, 0, Mathf.Max(0, screen.Y - size.Y)));
    }
}
