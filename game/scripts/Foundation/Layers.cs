namespace Kiln.Game.Foundation;

/// <summary>
/// Physics layer bit masks, mirroring the names in project.godot.
/// Kept in one place because raycast masks are the kind of magic number that silently
/// breaks click targeting when a layer is renumbered.
/// </summary>
public static class Layers
{
    public const uint World = 1 << 0;
    public const uint Player = 1 << 1;
    public const uint Enemy = 1 << 2;
    public const uint PlayerHitbox = 1 << 3;
    public const uint EnemyHitbox = 1 << 4;
    public const uint Interactable = 1 << 5;
    public const uint GroundClick = 1 << 6;
    public const uint Telegraph = 1 << 7;

    /// <summary>What a move/attack click is allowed to hit.</summary>
    public const uint ClickTargets = World | Enemy | Interactable | GroundClick;
}
