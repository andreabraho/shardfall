using Godot;
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
    }

    public override void _Process(double delta)
    {
        if (_cooldown > 0) _cooldown -= delta;
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
            return "There is no way through here.";
        }

        if (level < exit.RequiredLevel)
        {
            return $"The road ahead is beyond you. Return at level {exit.RequiredLevel}.";
        }

        if (exit.RequiredQuest is not null && !PlayerProfile.CompletedQuests.Contains(exit.RequiredQuest))
        {
            return "The way is barred. Something here is unfinished.";
        }

        if (GameWorld.Graph[ToZone]?.Scene is not { Length: > 0 })
        {
            return "The road runs on, but nothing has been built along it yet.";
        }

        return null;
    }
}
