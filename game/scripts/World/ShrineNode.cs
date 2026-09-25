using Godot;
using Kiln.Game.Input;

namespace Kiln.Game.World;

/// <summary>
/// A shrine in the world (WLD-02): the save point, the respawn point, the flask refill and
/// the fast-travel node, all in one object.
/// </summary>
/// <remarks>
/// Standing at one restores the player and makes it the place death sends them back to. That
/// much happens on touch, with no key press, because a player who walks past a shrine and
/// then dies has been punished for missing a prompt rather than for playing badly.
/// <para>
/// The services that cost something — travelling, respeccing — need the interact key, because
/// those are decisions and walking into a rock is not.
/// </para>
/// </remarks>
public partial class ShrineNode : Area3D
{
    private static readonly Color Lit = new("ffd88a");
    private static readonly Color Unlit = new("55606b");

    private Visual.VisualRoot? _visual;
    private Combat.NamePlate? _plate;
    private bool _playerInside;

    [Export] public string ShrineId { get; set; } = "";

    [Export] public float Radius { get; set; } = 3.2f;

    public override void _Ready()
    {
        AddToGroup("shrines");

        CollisionLayer = 0;
        CollisionMask = Foundation.Layers.Player;
        Monitoring = true;

        AddChild(new CollisionShape3D
        {
            Name = "Range",
            Shape = new SphereShape3D { Radius = Radius },
            Position = new Vector3(0, 1.0f, 0),
        });

        _visual = GetNodeOrNull<Visual.VisualRoot>("VisualRoot");

        // The stone stands on the ground whatever height the scene put its visual at: the
        // placeholder was a cylinder centred at half its height, the waystone is a model.
        if (_visual is not null && GameContent.IsLoaded
            && GameContent.Database.Visuals.TryGetValue(_visual.VisualId, out var look))
        {
            _visual.Position = new Vector3(0, (float)look.Height / 2, 0);
        }

        BuildCrystal();

        _plate = new Combat.NamePlate
        {
            Name = "NamePlate",
            Offset = new Vector3(0, 3.9f, 0),
            Rank = Combat.NameRank.Elite,
        };

        AddChild(_plate);
        _plate.SetText(Label());
        Restyle();

        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    private string Label()
    {
        var shrine = GameWorld.IsLoaded ? GameWorld.Graph.Shrine(ShrineId) : null;

        return shrine is null ? ShrineId : Items.GameItems.Localise(shrine.Name);
    }

    /// <summary>
    /// A discovered shrine lights up. It is the only thing in the world that changes colour
    /// permanently, so "I have been here" is readable from across the zone.
    /// </summary>
    private void Restyle()
    {
        var known = GameWorld.IsLoaded && GameWorld.Travel.IsDiscovered(ShrineId);
        var colour = known ? Lit : Unlit;

        if (_plate is not null) _plate.Tint = colour;

        // The stone keeps its own colour; the crystal over it says whether it is known — dull
        // until the player has touched it, gold and glowing after.
        if (_crystalMaterial is not null)
        {
            _crystalMaterial.AlbedoColor = colour;
            _crystalMaterial.Emission = colour;
            _crystalMaterial.EmissionEnergyMultiplier = known ? 1.6f : 0.15f;
        }

        if (_light is not null) _light.Visible = known;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (!body.IsInGroup("player") || !GameWorld.IsLoaded) return;

        _playerInside = true;

        var first = GameWorld.Travel.Discover(ShrineId);

        Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndShrine);

        Restyle();
        Restore(body);

        // A shrine is the save point (WLD-02). Written after the restore, so the save holds
        // the rested character rather than the one who walked up.
        Saving.SaveService.Autosave($"Rested at {ShrineId}");

        GD.Print(first
            ? $"[shrine] discovered {ShrineId}"
            : $"[shrine] rested at {ShrineId}");

        if (GetTree().CurrentScene?.GetNodeOrNull<UI.ShrinePanel>("Session/ShrinePanel") is { } panel)
        {
            panel.Announce(Label(), first);
        }
    }

    private MeshInstance3D? _crystal;
    private StandardMaterial3D? _crystalMaterial;
    private OmniLight3D? _light;
    private double _time;

    /// <summary>Height the crystal floats at, over the top of the stone.</summary>
    private const float CrystalHeight = 3.1f;

    /// <summary>Where a traveller lands, from the stone: beside it, not inside it.</summary>
    public static readonly Vector3 ArrivalOffset = new(0, 0.1f, 2.5f);

    /// <summary>
    /// The waystone's crystal (REF-08): a gem turning slowly over the obelisk, and a light
    /// once the stone is known. The grey cylinder it replaces read as a post, not as a place
    /// to rest, travel from and rethink a build.
    /// </summary>
    private void BuildCrystal()
    {
        _crystalMaterial = new StandardMaterial3D
        {
            AlbedoColor = Unlit,
            EmissionEnabled = true,
            Emission = Unlit,
            Metallic = 0.3f,
            Roughness = 0.25f,
        };

        // Four sides and two rings: a sphere with that few faces is an octahedron — a gem.
        _crystal = new MeshInstance3D
        {
            Name = "Crystal",
            Mesh = new SphereMesh { Radius = 0.32f, Height = 0.9f, RadialSegments = 4, Rings = 2 },
            MaterialOverride = _crystalMaterial,
            Position = new Vector3(0, CrystalHeight, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };

        AddChild(_crystal);

        _light = new OmniLight3D
        {
            Name = "Glow",
            Position = new Vector3(0, CrystalHeight, 0),
            LightColor = Lit,
            LightEnergy = 1.4f,
            OmniRange = 7f,
            Visible = false,
        };

        AddChild(_light);
    }

    public override void _Process(double delta)
    {
        if (_crystal is null) return;

        _time += delta;
        _crystal.Rotation = new Vector3(0, (float)(_time * 0.8), 0);
        _crystal.Position = new Vector3(0, CrystalHeight + (0.12f * Mathf.Sin((float)_time * 1.6f)), 0);
    }

    private void OnBodyExited(Node3D body)
    {
        if (body.IsInGroup("player")) _playerInside = false;
    }

    /// <summary>
    /// Full health and mana. A shrine you have to heal at is not a rest point.
    /// <para>
    /// It does not touch the flask (REF-02): charges are poured from draughts bought at a
    /// merchant, and a shrine that filled it for free would make the draughts pointless
    /// everywhere a shrine is within walking distance — which is everywhere.
    /// </para>
    /// </summary>
    private static void Restore(Node3D player)
    {
        if (player.GetNodeOrNull<Combat.Combatant>("Combatant") is { } combatant)
        {
            combatant.Heal((int)combatant.Health.Max);
            combatant.Mana.Fill();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_playerInside || !@event.IsActionPressed(GameActions.Interact)) return;

        if (GetTree().CurrentScene?.GetNodeOrNull<UI.ShrinePanel>("Session/ShrinePanel") is not { } panel) return;

        panel.Open(ShrineId);
        GetViewport().SetInputAsHandled();
    }
}
