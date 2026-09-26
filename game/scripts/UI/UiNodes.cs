using Godot;

namespace Kiln.Game.UI;

/// <summary>Small helpers every panel needs.</summary>
public static class UiNodes
{
    /// <summary>
    /// Empties a container before it is filled again: each child is taken out at once and
    /// freed later.
    /// </summary>
    /// <remarks>
    /// Freeing alone leaves the old children in place until the end of the frame, so for that
    /// frame old and new stand together and the window measures itself twice as tall — and a
    /// container grows with its content but never shrinks back (the character window and the
    /// wardrobe jumped on screen this way, 2026-09-26).
    /// </remarks>
    public static void Clear(Node container)
    {
        foreach (var child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }
}
