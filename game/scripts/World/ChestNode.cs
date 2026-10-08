using System.Collections.Generic;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Game.Input;

namespace Kiln.Game.World;

/// <summary>
/// The chest a final boss leaves behind (the Abyss Throne, 2026-10-08, at your call): it holds
/// every piece of the set, and the player opens it and takes the one they choose — the stat
/// too, for a piece born with one.
/// </summary>
/// <remarks>
/// Opened by walking up to it, or with the interact key while beside it. Shut again with the
/// choice not made, it stays, and opens again; taken from, it is spent and fades away.
/// </remarks>
public partial class ChestNode : Area3D
{
    private const float PieceScale = 1.7f;
    private static readonly Color Glow = new(0.75f, 0.45f, 1f);

    private bool _playerInside;
    private bool _spent;
    private StandardMaterial3D? _beamMaterial;
    private double _time;

    /// <summary>The item ids the player chooses among.</summary>
    public IReadOnlyList<string> Offer { get; private set; } = [];

    public bool Spent => _spent;

    /// <summary>Puts a chest holding <paramref name="offer"/> at a point under <paramref name="parent"/>.</summary>
    public static ChestNode Place(Node parent, IReadOnlyList<string> offer, Vector3 at)
    {
        var chest = new ChestNode { Name = "AbyssChest", Offer = offer };

        parent.AddChild(chest);
        chest.GlobalPosition = at;

        return chest;
    }

    public override void _Ready()
    {
        AddToGroup("chests");

        CollisionLayer = 0;
        CollisionMask = Foundation.Layers.Player;
        Monitoring = true;

        AddChild(new CollisionShape3D { Name = "Range", Shape = new SphereShape3D { Radius = 2.6f }, Position = new Vector3(0, 1f, 0) });

        AddChild(new KitPiece
        {
            Name = "Piece",
            PieceId = "kit_camp_chest",
            Position = new Vector3(0, 0.36f * PieceScale, 0),
            Scale = Vector3.One * PieceScale,
        });

        _beamMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoColor = Glow with { A = 0.18f },
        };

        AddChild(new MeshInstance3D
        {
            Name = "Beam",
            Mesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 1.0f, Height = 8f, CapTop = false, CapBottom = false },
            MaterialOverride = _beamMaterial,
            Position = new Vector3(0, 4f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        AddChild(new OmniLight3D { Name = "Light", LightColor = Glow, LightEnergy = 2.5f, OmniRange = 8f, Position = new Vector3(0, 2f, 0) });

        var plate = new Combat.NamePlate
        {
            Name = "NamePlate",
            Offset = new Vector3(0, 3.2f, 0),
            Rank = Combat.NameRank.Boss,
            Tint = new Color("d9a8ff"),
        };

        AddChild(plate);
        plate.SetText(L10n.T("Abyss chest — choose your reward"));

        BodyEntered += body =>
        {
            if (!body.IsInGroup("player")) return;

            _playerInside = true;
            Open();
        };

        BodyExited += body => { if (body.IsInGroup("player")) _playerInside = false; };

        GD.Print($"[tower] the abyss chest stands on {GetParent()?.GetParent()?.Name}");
        UI.WorldNotice.Show(GetTree(), L10n.T("A chest stands where it fell. Open it and choose your reward."));
    }

    public override void _Process(double delta)
    {
        if (_beamMaterial is null) return;

        _time += delta;
        _beamMaterial.AlbedoColor = _beamMaterial.AlbedoColor with { A = 0.13f + (0.07f * Mathf.Sin((float)_time * 2f)) };
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_playerInside || !@event.IsActionPressed(GameActions.Interact)) return;

        Open();
        GetViewport().SetInputAsHandled();
    }

    private void Open()
    {
        if (_spent) return;

        GetTree().CurrentScene?.GetNodeOrNull<UI.ChestPanel>("Session/ChestPanel")?.Open(this);
    }

    /// <summary>Called by the panel once the reward is taken: the chest is empty, and goes.</summary>
    public void Empty()
    {
        _spent = true;

        var tween = CreateTween();
        tween.TweenProperty(this, "scale", Vector3.One * 0.01f, 0.6).SetDelay(0.3);
        tween.TweenCallback(Callable.From(QueueFree));
    }
}
