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

        _plate = new Combat.NamePlate
        {
            Name = "NamePlate",
            Offset = new Vector3(0, 2.4f, 0),
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

        if (_visual is not null) _visual.Apply(_visual.VisualId, "#" + colour.ToHtml(false), _visual.VisualScale);
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

        if (GetTree().CurrentScene?.GetNodeOrNull<UI.ShrinePanel>("ShrinePanel") is { } panel)
        {
            panel.Announce(Label(), first);
        }
    }

    private void OnBodyExited(Node3D body)
    {
        if (body.IsInGroup("player")) _playerInside = false;
    }

    /// <summary>Full health and a full flask. A shrine you have to heal at is not a rest point.</summary>
    private static void Restore(Node3D player)
    {
        if (player.GetNodeOrNull<Combat.Combatant>("Combatant") is { } combatant)
        {
            combatant.Heal((int)combatant.Health.Max);
        }

        player.GetNodeOrNull<Player.HealthFlask>("HealthFlask")?.Refill();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_playerInside || !@event.IsActionPressed(GameActions.Interact)) return;

        if (GetTree().CurrentScene?.GetNodeOrNull<UI.ShrinePanel>("ShrinePanel") is not { } panel) return;

        panel.Open(ShrineId);
        GetViewport().SetInputAsHandled();
    }
}
