using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.Items;
using Kiln.Game.Items;

namespace Kiln.Game.World;

/// <summary>
/// One item lying on the ground, waiting to be picked up.
/// </summary>
/// <remarks>
/// Picked up on purpose, as in the original (REF-09): Z gathers everything close by, and a
/// click on a drop picks it up — from where the player stands when it is in reach, after a
/// walk to it when it is not. Walking over loot used to collect it, and the bag filled with
/// every common sword a camp let fall; now nothing enters it that the player did not ask for.
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

    /// <summary>Seconds on the ground before the drop is gone.</summary>
    [Export] public double Lifetime { get; set; } = 60.0;

    /// <summary>How long before the end it starts blinking.</summary>
    [Export] public double WarningSeconds { get; set; } = 10.0;

    /// <summary>How near a clicked drop has to be to be picked up without a step, in metres.</summary>
    public const float Reach = 2.5f;

    /// <summary>How far round the player Z gathers, in metres.</summary>
    public const float GatherRadius = 4f;

    /// <summary>The group every drop on the ground is in.</summary>
    public const string Group = "loot";

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

        // A rare or better drop is announced: the sound is how the player hears it over a fight.
        if (GameItems.Spec(item.DefId)?.Rarity >= Rarity.Rare) Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndRareDrop, at);

        return drop;
    }

    public override void _Ready()
    {
        _spec = GameItems.Spec(_item.DefId);
        AddToGroup(Group);

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

            // Over the frame drawn behind it when the cursor is on the drop.
            RenderPriority = 3,
            OutlineRenderPriority = 2,
        };

        AddChild(_label);
    }

    // -- The frame under the cursor ----------------------------------------------------------

    private static LootDrop? _hovered;
    private Node3D? _frame;

    /// <summary>
    /// Frames the drop the cursor is on — the one a click would pick up (REF-09) — and takes
    /// the frame off the one it was on before. Null frames nothing.
    /// </summary>
    public static void Hover(LootDrop? drop)
    {
        if (_hovered == drop) return;

        if (_hovered is not null && IsInstanceValid(_hovered)) _hovered.SetFramed(false);

        _hovered = drop;
        drop?.SetFramed(true);
    }

    private void SetFramed(bool on)
    {
        if (on) _frame ??= BuildFrame();

        if (_frame is not null) _frame.Visible = on && _label.Visible;

        // The box glows brighter while it is the one pointed at.
        if (_mesh.MaterialOverride is StandardMaterial3D material) material.EmissionEnergyMultiplier = on ? 1.6f : 0.6f;
    }

    /// <summary>
    /// A plate behind the name in the rarity's colour, with a dark face: a box round the words,
    /// turned to the camera the way the words are.
    /// </summary>
    private Node3D BuildFrame()
    {
        var colour = RarityColour(_spec?.Rarity ?? Rarity.Common);
        var size = new Vector2(_label.GetAabb().Size.X, _label.GetAabb().Size.Y);

        // Before the text has been laid out its box is empty; a guess from its length will do.
        if (size.X < 0.01f) size = new Vector2(Describe().Length * 0.08f, 0.16f);

        var frame = new Node3D { Name = "Frame", Position = _label.Position };

        frame.AddChild(Plate(size + new Vector2(0.16f, 0.1f), colour, 0));
        frame.AddChild(Plate(size + new Vector2(0.1f, 0.05f), new Color(0.05f, 0.05f, 0.07f, 0.82f), 1));

        AddChild(frame);
        return frame;
    }

    private static MeshInstance3D Plate(Vector2 size, Color colour, int priority) => new()
    {
        Mesh = new QuadMesh { Size = size },
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = colour,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            RenderPriority = priority,
        },
    };

    public override void _ExitTree()
    {
        if (_hovered == this) _hovered = null;
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
            if (_frame is not null) _frame.Visible = shown && _hovered == this;
        }

        // A slow bob and spin so a drop reads as an object to collect rather than scenery.
        _mesh.Rotation = new Vector3(0, (float)(_age * 1.6), 0);
        _mesh.Position = new Vector3(0, (float)(Mathf.Sin(_age * 2.2) * 0.06), 0);
    }

    /// <summary>Still on the ground and landed: something a pick-up can take.</summary>
    public bool CanCollect => !IsQueuedForDeletion() && _age >= ArmDelay && _age < Lifetime;

    /// <summary>Into the bag, if there is room. False leaves it where it lies.</summary>
    public bool Collect(PlayerInventory bag)
    {
        if (!CanCollect || !bag.TryPickUp(_item)) return false;

        QueueFree();
        return true;
    }

    /// <summary>Whether it lies within <paramref name="radius"/> of a point, measured flat.</summary>
    public bool Within(Vector3 point, float radius) =>
        new Vector2(GlobalPosition.X - point.X, GlobalPosition.Z - point.Z).Length() <= radius;

    /// <summary>
    /// Z (REF-09): everything lying round the player goes into the bag, nearest first, until the
    /// bag is full. The number picked up.
    /// </summary>
    public static int GatherAround(Node3D player, PlayerInventory bag)
    {
        var near = new System.Collections.Generic.List<LootDrop>();

        foreach (var node in player.GetTree().GetNodesInGroup(Group))
        {
            if (node is LootDrop { CanCollect: true } drop && drop.Within(player.GlobalPosition, GatherRadius)) near.Add(drop);
        }

        near.Sort((a, b) => a.GlobalPosition.DistanceSquaredTo(player.GlobalPosition).CompareTo(b.GlobalPosition.DistanceSquaredTo(player.GlobalPosition)));

        var taken = 0;

        foreach (var drop in near)
        {
            // A full bag says so once, and the rest stays on the ground.
            if (!drop.Collect(bag)) break;

            taken++;
        }

        return taken;
    }

    /// <summary>
    /// The drop under the cursor, if any: its box or its name, whichever the player points at.
    /// The nearest to the camera wins where two overlap.
    /// </summary>
    public static LootDrop? Under(Viewport viewport, Camera3D camera, Vector2 screen)
    {
        LootDrop? best = null;
        var nearest = float.MaxValue;

        foreach (var node in viewport.GetTree().GetNodesInGroup(Group))
        {
            if (node is not LootDrop { CanCollect: true } drop || !drop._label.Visible) continue;

            var hit = drop.Covers(camera, screen, drop._mesh.GlobalPosition, 0.22f, 0.22f)
                || drop.Covers(camera, screen, drop._label.GlobalPosition, drop.LabelHalfWidth(), drop.LabelHalfHeight());

            if (!hit) continue;

            var distance = camera.GlobalPosition.DistanceTo(drop.GlobalPosition);

            if (distance >= nearest) continue;

            nearest = distance;
            best = drop;
        }

        return best;
    }

    private float LabelHalfWidth() => (_label.GetAabb().Size.X / 2) + 0.05f;

    private float LabelHalfHeight() => (_label.GetAabb().Size.Y / 2) + 0.04f;

    /// <summary>
    /// Whether a screen point falls in a rectangle facing the camera round a world point — the
    /// shape a billboard label or a small box shows. A few pixels of slack round it.
    /// </summary>
    private bool Covers(Camera3D camera, Vector2 screen, Vector3 centre, float halfWidth, float halfHeight)
    {
        if (camera.IsPositionBehind(centre)) return false;

        var basis = camera.GlobalTransform.Basis;
        var middle = camera.UnprojectPosition(centre);
        var across = Mathf.Abs(camera.UnprojectPosition(centre + (basis.X * halfWidth)).X - middle.X) + 4;
        var up = Mathf.Abs(camera.UnprojectPosition(centre + (basis.Y * halfHeight)).Y - middle.Y) + 4;

        return Mathf.Abs(screen.X - middle.X) <= across && Mathf.Abs(screen.Y - middle.Y) <= up;
    }
}
