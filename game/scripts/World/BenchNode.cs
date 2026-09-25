using Godot;
using Kiln.Core.Foundation;
using Kiln.Game.Input;

namespace Kiln.Game.World;

/// <summary>
/// An upgrade bench standing in the world (FR-7.14).
/// </summary>
/// <remarks>
/// The bench panel already opens from anywhere on a key, and that stays true — this does not
/// gate it. What this adds is the *place*: the requirement is that a bench stands immediately
/// after every boss floor, because the reward for a boss is a decision, and a decision means
/// most at the moment the player has just found out which piece of their kit is holding them
/// back. A keybind cannot be somewhere. This can.
/// </remarks>
public partial class BenchNode : Area3D
{
    private Combat.NamePlate? _plate;
    private bool _playerInside;

    [Export] public float Radius { get; set; } = 3.0f;

    private bool _gift;

    /// <summary>
    /// One free upgrade waiting here — the tower smith's (the original's smith after its boss
    /// floor). Given by the tower on floors that carry it, taken by the first upgrade asked for.
    /// </summary>
    public bool HasGift => _gift;

    /// <summary>Puts a free upgrade on the bench, and says so over it.</summary>
    public void Grant()
    {
        if (_gift) return;

        _gift = true;
        GD.Print($"[tower] the smith's free upgrade waits at {GetParent()?.Name}");
        Label();
    }

    /// <summary>Takes the free upgrade away — used, or left behind with the floor.</summary>
    public void Revoke()
    {
        _gift = false;
        Label();
    }

    private void Label()
    {
        if (_plate is not null)
        {
            _plate.SetText(_gift ? L10n.T("Tower smith — one free upgrade") : L10n.T("Workbench"));
            _plate.Tint = _gift ? new Color("ffd36b") : new Color("c9b08a");

            // Read from across the floor while the gift waits: a boss-sized name, higher up.
            _plate.Rank = _gift ? Combat.NameRank.Boss : Combat.NameRank.Elite;
            _plate.Offset = new Vector3(0, _gift ? 4.2f : 2.2f, 0);
        }

        // The anvil stands larger while it is the smith's (2026-09-25), under a column of light.
        if (GetNodeOrNull<Node3D>("Piece") is { } piece) piece.Scale = Vector3.One * (_gift ? SmithScale : 1f);

        if (_gift) _beacon ??= BuildBeacon();
        if (_beacon is not null) _beacon.Visible = _gift;
    }

    /// <summary>How much larger the anvil stands while the smith's gift waits on it.</summary>
    private const float SmithScale = 1.8f;

    private Node3D? _beacon;
    private StandardMaterial3D? _beamMaterial;
    private double _time;

    /// <summary>
    /// What marks the smith's bench across the floor: a column of gold light rising from it,
    /// a ring on the ground round it, and the light they give off.
    /// </summary>
    private Node3D BuildBeacon()
    {
        var gold = new Color("ffd36b");
        var beacon = new Node3D { Name = "Beacon" };

        _beamMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoColor = gold with { A = 0.18f },
        };

        beacon.AddChild(new MeshInstance3D
        {
            Name = "Beam",
            Mesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.9f, Height = 8f, CapTop = false, CapBottom = false },
            MaterialOverride = _beamMaterial,
            Position = new Vector3(0, 4f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        beacon.AddChild(new MeshInstance3D
        {
            Name = "Ring",
            Mesh = new TorusMesh { InnerRadius = 1.55f, OuterRadius = 1.75f },
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = gold,
                EmissionEnabled = true,
                Emission = gold,
            },
            Position = new Vector3(0, 0.05f, 0),
            Scale = new Vector3(1, 0.15f, 1),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        beacon.AddChild(new OmniLight3D
        {
            Name = "Glow",
            LightColor = gold,
            LightEnergy = 2.5f,
            OmniRange = 9f,
            Position = new Vector3(0, 2f, 0),
        });

        AddChild(beacon);
        return beacon;
    }

    public override void _Process(double delta)
    {
        if (_beamMaterial is null || _beacon?.Visible != true) return;

        // The column breathes, so it reads as light and catches the eye.
        _time += delta;
        _beamMaterial.AlbedoColor = _beamMaterial.AlbedoColor with { A = 0.13f + (0.07f * Mathf.Sin((float)_time * 2f)) };
    }

    public override void _Ready()
    {
        AddToGroup("benches");

        CollisionLayer = 0;
        CollisionMask = Foundation.Layers.Player;
        Monitoring = true;

        AddChild(new CollisionShape3D
        {
            Name = "Range",
            Shape = new SphereShape3D { Radius = Radius },
            Position = new Vector3(0, 1.0f, 0),
        });

        _plate = new Combat.NamePlate
        {
            Name = "NamePlate",
            Offset = new Vector3(0, 2.2f, 0),
            Rank = Combat.NameRank.Elite,
            Tint = new Color("c9b08a"),
        };

        AddChild(_plate);
        _plate.SetText(L10n.T("Workbench"));

        BodyEntered += body => { if (body.IsInGroup("player")) _playerInside = true; };
        BodyExited += body => { if (body.IsInGroup("player")) _playerInside = false; };
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_playerInside || !@event.IsActionPressed(GameActions.Interact)) return;

        if (GetTree().CurrentScene?.GetNodeOrNull<UI.WorkbenchPanel>("Session/WorkbenchPanel") is not { } panel)
        {
            return;
        }

        panel.Open(this);
        GetViewport().SetInputAsHandled();
    }
}
