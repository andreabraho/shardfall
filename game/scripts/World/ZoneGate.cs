using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.World;

namespace Kiln.Game.World;

/// <summary>
/// The way out of a map (WLD-01). Stepping into it moves the player to the neighbouring zone,
/// if the exit is open to them.
/// </summary>
/// <remarks>
/// Which zones connect, and what each crossing costs in levels or finished quests, is content
/// in <c>game/data/zones/act1.json</c> and already validated. This node adds the one thing the
/// data cannot hold: where on the ground the border is. It names a destination and nothing
/// else, so a gate cannot invent a route the world graph does not have.
/// </remarks>
public partial class ZoneGate : Area3D
{
    private double _cooldown;
    private Combat.NamePlate? _label;

    /// <summary>The zone on the other side. Must be a declared exit of the zone this scene is.</summary>
    [Export] public string ToZone { get; set; } = "";

    /// <summary>Radius of the crossing. Wide enough to be hard to slip past at a run.</summary>
    [Export] public float Radius { get; set; } = 4.0f;

    public override void _Ready()
    {
        if (!GameWorld.IsLoaded)
        {
            GD.PushError($"[gate] gate to '{ToZone}' loaded before GameWorld.Load() ran.");
            return;
        }

        AddToGroup("zone_gates");

        CollisionLayer = 0;
        CollisionMask = Foundation.Layers.Player;
        Monitoring = true;

        AddChild(new CollisionShape3D
        {
            Name = "Crossing",
            Shape = new SphereShape3D { Radius = Radius },
        });

        BodyEntered += OnBodyEntered;
        BuildLabel();
    }

    /// <summary>
    /// The sign over the border, shown only while the reveal key is held.
    /// </summary>
    /// <remarks>
    /// Held rather than permanent because a label that is always on is scenery the player
    /// stops reading, and a map with four borders would carry four permanent captions. Ranked
    /// as a boss plate on purpose: that rank is the one that neither fades with distance nor
    /// hides behind terrain, which is exactly what a border needs — the whole use of the key
    /// is working out which way to go from where you are standing, not from beside the gate.
    /// </remarks>
    private void BuildLabel()
    {
        var target = GameWorld.Graph[ToZone];

        _label = new Combat.NamePlate
        {
            Name = "Sign",
            Offset = new Vector3(0, 4.2f, 0),
            Rank = Combat.NameRank.Boss,
            Tint = new Color(0.62f, 0.82f, 1f),
            Visible = false,
        };

        AddChild(_label);
    }

    /// <summary>
    /// Fills the sign once, on the first frame.
    /// </summary>
    /// <remarks>
    /// Not in <c>_Ready</c>: a node is ready before its parent, so at that point the current
    /// zone is still the one the player just left, and the requirement would be read from the
    /// wrong side of the border.
    /// </remarks>
    private void FillLabel()
    {
        var target = GameWorld.Graph[ToZone];

        _label!.SetText(target is null
            ? ToZone
            : Items.GameItems.Localise(target.Name) + Requirement(target));
    }

    /// <summary>What the border asks of the player, or nothing when it is simply open.</summary>
    private string Requirement(Kiln.Core.World.Zone target)
    {
        var notes = "";

        if (GameWorld.Graph[GameWorld.CurrentZoneId] is { } here)
        {
            foreach (var exit in here.Exits)
            {
                if (exit.To != ToZone) continue;

                if (exit.RequiredLevel > 0) notes += "  ·  " + L10n.F("level {0}", exit.RequiredLevel);
                if (exit.RequiredQuest is not null) notes += "  ·  " + L10n.T("barred");
            }
        }

        // Added alongside a level requirement rather than instead of it. A border that asks
        // for level fourteen and then turns out to lead nowhere is two facts, and the second
        // is the one worth knowing before walking there.
        if (target.Scene.Length == 0) notes += "  ·  " + L10n.T("unbuilt");

        return notes;
    }

    public override void _Process(double delta)
    {
        if (_cooldown > 0) _cooldown -= delta;

        if (_label is null) return;

        if (_label.Text.Length == 0) FillLabel();

        _label.Visible = Godot.Input.IsActionPressed(Kiln.Game.Input.GameActions.RevealLabels);
    }

    private void OnBodyEntered(Node3D body)
    {
        // Arriving through a gate puts the player next to the one facing back. Without the
        // pause they would be sent straight home again by the scene they just left.
        if (_cooldown > 0 || !body.IsInGroup("player")) return;

        var character = body.GetNodeOrNull<Player.PlayerCharacter>("PlayerCharacter");

        if (character is null) return;

        var level = character.Progression.Level;
        var refusal = Check(level);

        if (refusal is not null)
        {
            ZoneTransition.Announce(GetTree(), refusal);
            _cooldown = 3.0;
            return;
        }

        character.CarryOut();
        ZoneTransition.Begin(GetTree(), GameWorld.CurrentZoneId, ToZone);
    }

    /// <summary>
    /// Why the player may not pass, or null when they may.
    /// </summary>
    /// <remarks>
    /// The gate asks the world graph rather than carrying its own requirement, so a level gate
    /// raised in data cannot be left behind by a scene nobody remembered to open.
    /// </remarks>
    private string? Check(int level)
    {
        var zone = GameWorld.Graph[GameWorld.CurrentZoneId];

        if (zone is null) return null;

        ZoneExit? exit = null;

        foreach (var candidate in zone.Exits)
        {
            if (candidate.To == ToZone) exit = candidate;
        }

        if (exit is null)
        {
            GD.PushError($"[gate] '{GameWorld.CurrentZoneId}' has no exit to '{ToZone}'.");
            return L10n.T("There is no way through here.");
        }

        if (level < exit.RequiredLevel)
        {
            return L10n.F("The road ahead is beyond you. Return at level {0}.", exit.RequiredLevel);
        }

        if (exit.RequiredQuest is not null && !PlayerProfile.Quests.IsComplete(exit.RequiredQuest))
        {
            return L10n.T("The way is barred. Something here is unfinished.");
        }

        if (GameWorld.Graph[ToZone]?.Scene is not { Length: > 0 })
        {
            return L10n.T("The road runs on, but nothing has been built along it yet.");
        }

        return null;
    }
}
