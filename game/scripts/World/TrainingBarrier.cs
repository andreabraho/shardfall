using Godot;
using Kiln.Core.Foundation;

namespace Kiln.Game.World;

/// <summary>
/// A shimmering veil across a village gate that holds the player in until the training is done
/// (2026-09-27, at your call: the training is not optional). Walking into it says why and what
/// is left; finishing the last step lifts it.
/// </summary>
/// <remarks>
/// Solid to the player only, and never baked into the navigation mesh: nothing else needs to
/// pass the gate while it stands, and the gate must be open to paths the moment it goes.
/// </remarks>
public partial class TrainingBarrier : StaticBody3D
{
    /// <summary>How wide the gateway is, along the node's X axis.</summary>
    [Export] public float Width { get; set; } = 4.6f;

    private const float Height = 3.8f;

    private StandardMaterial3D? _veil;
    private double _told;
    private double _time;
    private bool _lifting;

    public override void _Ready()
    {
        if (!GameContent.IsLoaded || Quests.Tutorial.IsDone)
        {
            QueueFree();
            return;
        }

        CollisionLayer = Foundation.Layers.World;
        CollisionMask = 0;

        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(Width, Height, 0.6f) },
            Position = new Vector3(0, Height * 0.5f, 0),
        });

        _veil = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoColor = new Color(1f, 0.82f, 0.4f, 0.3f),
        };

        AddChild(new MeshInstance3D
        {
            Name = "Veil",
            Mesh = new QuadMesh { Size = new Vector2(Width, Height) },
            MaterialOverride = _veil,
            Position = new Vector3(0, Height * 0.5f, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        // A little deeper than the wall, so the player is told before they are stopped.
        var warn = new Area3D { Name = "Warn", CollisionLayer = 0, CollisionMask = Foundation.Layers.Player, Monitoring = true };
        warn.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(Width, Height, 3f) },
            Position = new Vector3(0, Height * 0.5f, 0),
        });
        warn.BodyEntered += _ => Tell();
        AddChild(warn);

        Quests.QuestTracker.Changed += OnQuestsChanged;
    }

    public override void _ExitTree() => Quests.QuestTracker.Changed -= OnQuestsChanged;

    public override void _Process(double delta)
    {
        _time += delta;
        _told = System.Math.Max(0, _told - delta);

        if (_veil is null) return;

        // A slow breath of light, so it reads as a spell on the gate and not a pane of glass.
        var alpha = _lifting
            ? Mathf.Max(0f, _veil.AlbedoColor.A - (float)delta * 0.6f)
            : 0.24f + (0.1f * Mathf.Sin((float)_time * 2.2f));

        _veil.AlbedoColor = _veil.AlbedoColor with { A = alpha };

        if (_lifting && alpha <= 0f) QueueFree();
    }

    private void Tell()
    {
        if (_told > 0 || _lifting) return;

        _told = 4;

        var left = PlayerProfile.Quests.Active is { } quest ? Quests.QuestTracker.Name(quest) : "";

        UI.WorldNotice.Show(GetTree(), L10n.F("The gate is sealed until your training is done. Next: {0}", left));
    }

    private void OnQuestsChanged()
    {
        if (_lifting || !Quests.Tutorial.IsDone) return;

        // Open at once, fade out after: the player is not kept waiting for an effect.
        _lifting = true;
        SetDeferred(CollisionObject3D.PropertyName.CollisionLayer, 0);

        foreach (var child in GetChildren())
        {
            if (child is Area3D area) area.SetDeferred(Area3D.PropertyName.Monitoring, false);
        }

        // After the quest's own notice, which names the next quest and must not be overwritten.
        var tree = GetTree();
        tree.CreateTimer(3.8).Timeout += () => UI.WorldNotice.Show(tree, L10n.T("Training complete — the village gates are open."));
    }
}
