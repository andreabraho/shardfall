using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.Items;
using Kiln.Game.Items;

namespace Kiln.Game.World;

/// <summary>
/// One item lying on the ground, waiting to be walked over.
/// </summary>
/// <remarks>
/// Auto-pickup on proximity rather than a click, because click-to-move already owns the left
/// mouse button and a game where looting fights with moving is a game where looting feels
/// bad. The rarity filter of FR-5.8 lands with the settings screen; until then everything is
/// collected, which is the right default while bags are large and vendors do not exist.
/// <para>
/// The drop is labelled, coloured by rarity, and lies there for a minute. It used to stay
/// forever, on the reasoning that nothing expiring serves a single-player game — but a farmed
/// camp then leaves a carpet of junk nobody wanted, and the one drop worth picking up is lost
/// among forty that were not. It blinks for its last ten seconds, so it never vanishes while
/// the player is looking at it without having said so first.
/// </para>
/// </remarks>
public partial class LootDrop : Area3D
{
    private ItemInstance _item = null!;
    private ItemSpec? _spec;
    private Label3D _label = null!;
    private MeshInstance3D _mesh = null!;
    private double _age;

    /// <summary>Seconds before the drop can be collected, so it visibly lands first.</summary>
    [Export] public double ArmDelay { get; set; } = 0.35;

    [Export] public float PickupRadius { get; set; } = 1.6f;

    /// <summary>Seconds on the ground before the drop is gone.</summary>
    [Export] public double Lifetime { get; set; } = 60.0;

    /// <summary>How long before the end it starts blinking.</summary>
    [Export] public double WarningSeconds { get; set; } = 10.0;

    /// <summary>
    /// Set for an item the player threw away themselves: it will not be collected again
    /// until they have stepped out of pickup range at least once.
    /// </summary>
    /// <remarks>
    /// Without this, dropping something is a no-op — auto-pickup is proximity-based and the
    /// player is standing on the spot, so the item returns to the bag on the next frame and
    /// the feature looks broken.
    /// </remarks>
    public bool RequiresStepAway { get; set; }

    private bool _steppedAway = true;

    public ItemInstance Item => _item;

    /// <summary>
    /// A deliberate, single drop at a known spot — the player emptying their bag.
    /// </summary>
    /// <remarks>
    /// Separate from the rolled overload because the scatter there needs an RNG, and running
    /// the loot stream for a UI action would mean two players with the same seed no longer
    /// got the same drops. Loot has to stay reproducible from a seed alone.
    /// </remarks>
    public static LootDrop Place(Node parent, ItemInstance item, Vector3 at)
    {
        var drop = new LootDrop
        {
            Name = $"Loot_{item.Uid}",
            _item = item,
            RequiresStepAway = true,
            _steppedAway = false,
        };

        parent.CallDeferred(Node.MethodName.AddChild, drop);
        drop.SetDeferred(Node3D.PropertyName.GlobalPosition, at + (Vector3.Up * 0.25f));

        return drop;
    }

    public static LootDrop Spawn(Node parent, ItemInstance item, Vector3 at, DeterministicRng rng)
    {
        var drop = new LootDrop
        {
            Name = $"Loot_{item.Uid}",
            _item = item,
        };

        // Scattered so a burst of drops does not stack into one unreadable pile.
        var angle = rng.NextDouble(0, Mathf.Tau);
        var distance = rng.NextDouble(0.3, 1.1);
        var offset = new Vector3((float)(Mathf.Cos(angle) * distance), 0, (float)(Mathf.Sin(angle) * distance));

        parent.CallDeferred(Node.MethodName.AddChild, drop);
        drop.SetDeferred(Node3D.PropertyName.GlobalPosition, at + offset + (Vector3.Up * 0.25f));

        return drop;
    }

    public override void _Ready()
    {
        _spec = GameItems.Spec(_item.DefId);

        CollisionLayer = 0;
        CollisionMask = 0;
        Monitoring = false;

        var colour = RarityColour(_spec?.Rarity ?? Rarity.Common);

        _mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.28f, 0.28f, 0.28f) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = colour,
                EmissionEnabled = true,
                Emission = colour,
                EmissionEnergyMultiplier = 0.6f,
            },
        };

        AddChild(_mesh);

        _label = new Label3D
        {
            Text = Describe(),
            Modulate = colour,
            FontSize = 24,
            OutlineSize = 8,
            Position = new Vector3(0, 0.55f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,

            // Scales with distance, like the damage numbers. FixedSize would hold the same
            // screen size at every zoom, so pulling the camera out would fill the view with
            // loot names — the same problem the damage numbers had.
            FixedSize = false,
            PixelSize = 0.006f,
        };

        AddChild(_label);
    }

    private string Describe()
    {
        var name = GameItems.NameOf(_item);

        return _item.Count > 1 ? L10n.F("{0} x{1}", name, _item.Count) : name;
    }

    public static Color RarityColour(Rarity rarity) => rarity switch
    {
        Rarity.Fine => new Color("6fd36f"),
        Rarity.Rare => new Color("5aa9ff"),
        Rarity.Epic => new Color("c47bff"),
        Rarity.Relic => new Color("ffb347"),
        _ => new Color("d8d8d8"),
    };

    public override void _Process(double delta)
    {
        _age += delta;

        if (_age >= Lifetime)
        {
            QueueFree();
            return;
        }

        // Blinks faster as it runs out. Visibility rather than fading, because a label half as
        // bright reads as a worse item, and the rarity colour is the one thing it must not lose.
        var left = Lifetime - _age;

        if (left < WarningSeconds)
        {
            var rate = left < 3 ? 8.0 : 3.0;
            var shown = Mathf.Sin(_age * rate * Mathf.Tau) > -0.3;

            _mesh.Visible = shown;
            _label.Visible = shown;
        }

        // A slow bob and spin so a drop reads as an object to collect rather than scenery.
        _mesh.Rotation = new Vector3(0, (float)(_age * 1.6), 0);
        _mesh.Position = new Vector3(0, (float)(Mathf.Sin(_age * 2.2) * 0.06), 0);

        if (_age < ArmDelay) return;

        var player = GetTree().GetFirstNodeInGroup("player") as Node3D;

        if (player is null) return;

        var distance = GlobalPosition.DistanceTo(player.GlobalPosition);

        if (RequiresStepAway && !_steppedAway)
        {
            // Armed the moment the player walks off it, so one step is all it takes to change
            // their mind — and standing still is all it takes not to.
            if (distance > PickupRadius) _steppedAway = true;

            return;
        }

        if (distance > PickupRadius) return;

        var bag = player.GetNodeOrNull<PlayerInventory>("PlayerInventory");

        if (bag is null || !bag.TryPickUp(_item)) return;

        QueueFree();
    }
}
