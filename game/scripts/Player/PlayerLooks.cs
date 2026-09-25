using Godot;
using Kiln.Game.Items;

namespace Kiln.Game.Player;

/// <summary>
/// Puts the wardrobe on the player (REF-23): the sword and armour skins through the worn-gear
/// look, the aura round the feet, and the companion at the player's side.
/// </summary>
/// <remarks>
/// Listens to the wardrobe, so a look won or changed shows at once, and builds everything
/// again when the player comes into a map — the companion lives in the map, not on the player,
/// so that it walks on its own rather than being carried.
/// </remarks>
public partial class PlayerLooks : Node
{
    private Node3D _player = null!;
    private Node3D? _aura;
    private string _auraId = "";
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

        RefreshAura();
        RefreshCompanion();
    }

    private void RefreshAura()
    {
        var look = Cosmetics.Worn(Cosmetics.Aura);
        var id = look?.Id ?? "";

        if (id == _auraId && (_aura is not null || look is null)) return;

        _auraId = id;
        _aura?.QueueFree();
        _aura = null;

        if (look is null) return;

        var colour = new Color(look.Color);
        var aura = new Node3D { Name = "Aura" };

        if (Visual.SkinParticles.Build(look.Particles, colour, Vector3.Zero, aura: true) is { } particles)
        {
            particles.Position = new Vector3(0, 0.08f, 0);
            aura.AddChild(particles);
        }

        // A little light of its colour on the ground and the legs.
        aura.AddChild(new OmniLight3D
        {
            Position = new Vector3(0, 0.6f, 0),
            LightColor = colour,
            LightEnergy = 0.6f,
            OmniRange = 2.6f,
            ShadowEnabled = false,
        });

        _player.AddChild(aura);
        _aura = aura;
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
