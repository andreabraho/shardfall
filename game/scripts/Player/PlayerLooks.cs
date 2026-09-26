using Godot;
using Kiln.Game.Items;

namespace Kiln.Game.Player;

/// <summary>
/// Puts the wardrobe on the player (REF-23): the sword and armour skins and the aura through
/// the worn-gear look, and the companion at the player's side.
/// </summary>
/// <remarks>
/// Listens to the wardrobe, so a look won or changed shows at once, and builds everything
/// again when the player comes into a map — the companion lives in the map, not on the player,
/// so that it walks on its own rather than being carried.
/// </remarks>
public partial class PlayerLooks : Node
{
    private Node3D _player = null!;
    private Companion? _companion;

    public override void _Ready()
    {
        _player = GetParent<Node3D>();
        PlayerProfile.Wardrobe.Changed += Refresh;

        // Deferred: the inventory and the model are made in the same frame as this.
        Callable.From(Refresh).CallDeferred();
    }

    public override void _ExitTree()
    {
        PlayerProfile.Wardrobe.Changed -= Refresh;

        if (_companion is not null && IsInstanceValid(_companion)) _companion.QueueFree();
    }

    private void Refresh()
    {
        if (!IsInsideTree()) return;

        _player.GetNodeOrNull<PlayerInventory>("PlayerInventory")?.ShowGear();

        RefreshCompanion();
    }

    private void RefreshCompanion()
    {
        var look = Cosmetics.Worn(Cosmetics.Companion);

        if (_companion is not null && IsInstanceValid(_companion))
        {
            if (look is not null && _companion.CosmeticId == look.Id) return;

            _companion.QueueFree();
            _companion = null;
        }

        if (look is null || GetTree().CurrentScene is not Node3D scene) return;

        _companion = Companion.Make(look);
        scene.AddChild(_companion);
    }
}
