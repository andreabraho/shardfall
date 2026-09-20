using System.Collections.Generic;
using System.Linq;
using Godot;
using Kiln.Core.Combat;
using Kiln.Core.Encounters;
using Kiln.Core.Foundation;
using Kiln.Game.Combat;
using Kiln.Game.Items;
using Kiln.Game.Visual;

namespace Kiln.Game.World;

/// <summary>
/// A shard in the world: the corruption zone, the fight, and the payoff (SHD-01/04/05/06/07).
/// </summary>
/// <remarks>
/// The rules live in <see cref="ShardEncounter"/>; this node only gives them a body. It owns
/// the shard's health, spawns what the encounter asks for, draws the pulse, and hands out the
/// rewards. Keeping the decisions on the other side of that line is what let the fight be
/// tested without a running game.
/// </remarks>
public partial class ShardNode : StaticBody3D
{
    [Export] public string ShardId { get; set; } = "shd_hollow_bloom";

    /// <summary>Radius of the corruption zone. Entering it starts the fight.</summary>
    [Export] public float ZoneRadius { get; set; } = 18f;

    /// <summary>Seconds before a broken node returns (doc 02 §3).</summary>
    [Export] public double RespawnSeconds { get; set; } = 240.0;

    /// <summary>Where adds appear, as a ring around the shard.</summary>
    [Export] public float SpawnRing { get; set; } = 7f;

    private ShardTier? _tier;
    private ShardEncounter? _fight;
    private ShardModifier _modifier;
    private Combatant _self = null!;
    private VisualRoot? _visual;
    private TelegraphVisual _telegraph = null!;
    private Node3D _addsRoot = null!;
    private NamePlate _plate = null!;
    private PackedScene? _enemyScene;

    private readonly List<EnemyBrain> _adds = [];
    private EnemyBrain? _anchor;
    private double _respawnIn;
    private bool _broken;

    public ShardPhase Phase => _fight?.Phase ?? ShardPhase.Dormant;

    public ShardModifier Modifier => _modifier;

    public bool IsEngaged => _fight?.IsActive == true;

    public double ReclamationProgress => _fight?.ReclamationProgress ?? 0;

    public bool IsReclaiming => _fight?.IsReclaiming == true;

    public Combatant Core => _self;

    /// <summary>Raised on engage and on break, so the HUD can show and hide the shard frame.</summary>
    [Signal] public delegate void EncounterChangedEventHandler();

    public override void _Ready()
    {
        AddToGroup("shards");

        CollisionLayer = Foundation.Layers.Enemy;
        CollisionMask = 0;

        _self = new Combatant { Name = "Combatant", IsEncounter = true };
        AddChild(_self);

        _visual = GetNodeOrNull<VisualRoot>("VisualRoot");
        _enemyScene = GD.Load<PackedScene>("res://scenes/enemy.tscn");

        _telegraph = new TelegraphVisual { Name = "Telegraph" };
        CallDeferred(Node.MethodName.AddChild, _telegraph);

        _addsRoot = new Node3D { Name = "Adds" };
        CallDeferred(Node.MethodName.AddChild, _addsRoot);

        _plate = new NamePlate
        {
            Name = "NamePlate",
            Rank = NameRank.Boss,
            Offset = new Vector3(0, 3.8f, 0),
        };

        CallDeferred(Node.MethodName.AddChild, _plate);

        var collision = new CollisionShape3D
        {
            Name = "Collision",
            Shape = new CylinderShape3D { Radius = 1.2f, Height = 3.2f },
            Position = new Vector3(0, 1.6f, 0),
        };

        AddChild(collision);

        CallDeferred(nameof(Arm));

        Debug.DebugOverlay.Register("shard", this, () => _fight is null
            ? "none"
            : $"{ShardId} {_fight.Phase} {_self.Health.Fraction:P0} {_modifier}"
              + (_fight.IsReclaiming ? $" reclaim {_fight.ReclamationRemaining:F1}s" : ""));
    }

    /// <summary>Loads the tier, rolls the modifier and readies the node for a fresh fight.</summary>
    private void Arm()
    {
        if (!GameContent.IsLoaded) return;

        _tier = new Kiln.Data.Encounters.ShardCatalogue(GameContent.Database).Tier(ShardId);

        if (_tier is null)
        {
            GD.PushWarning($"ShardNode '{Name}': unknown shard id '{ShardId}'.");
            return;
        }

        // A fresh modifier on every respawn, so farming a node is varied rather than identical.
        _modifier = _tier.RollModifier(GameItems.EncounterRng);
        _fight = new ShardEncounter(_tier, _modifier, GameSession.Difficulty);
        _broken = false;

        _self.Configure(
            new StatBlock
            {
                Level = _tier.Level,
                Family = MonsterFamily.Mystic,
                FlatMaxHp = _tier.MaxHp * GameSession.Difficulty.EnemyHpMultiplier,
                FlatDefense = _tier.Level * 1.5,
                FlatAttackPower = 0,
            },
            "$shard.name");

        _self.Health.Fill();

        if (_visual is not null)
        {
            _visual.Visible = true;
            _visual.Apply("mesh_placeholder_monolith", ModifierTint(_modifier), 1.0);
        }

        var label = GameContent.Database.Shards.TryGetValue(ShardId, out var def)
            ? GameItems.Localise(def.Name)
            : ShardId;

        // The modifier is part of the name, because it is the thing the player needs to know
        // before deciding whether to walk in.
        _plate.SetText(_modifier == ShardModifier.None ? label : $"{label}  ·  {_modifier}");
        _plate.Tint = new Color(ModifierTint(_modifier));
        _plate.Visible = true;

        GD.Print($"[shard] {ShardId} armed — tier {_tier.Tier}, {_tier.MaxHp:N0} hp, modifier {_modifier}");

        EmitSignal(SignalName.EncounterChanged);
    }

    /// <summary>Colour tells the player which modifier they are walking into, before the fight starts.</summary>
    private static string ModifierTint(ShardModifier modifier) => modifier switch
    {
        ShardModifier.Frenzied => "#d06a4f",
        ShardModifier.Warded => "#4f8fd0",
        ShardModifier.Venomous => "#6fbf5a",
        ShardModifier.Twin => "#c98fd0",
        _ => "#7a4fd0",
    };

    public override void _PhysicsProcess(double delta)
    {
        if (_broken)
        {
            TickRespawn(delta);
            return;
        }

        if (_fight is null || _tier is null) return;

        PruneAdds();

        var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
        var inZone = player is not null && GlobalPosition.DistanceTo(player.GlobalPosition) <= ZoneRadius;

        // Walking out abandons the fight. Without this a shard could be whittled down over
        // many visits, which turns the encounter into a chore rather than a fight.
        if (_fight.IsActive && !inZone)
        {
            Disengage();
            return;
        }

        // Warded: the shard shrugs off damage while its adds live, so clearing them is the
        // prerequisite for hurting it rather than a suggestion.
        _self.IncomingDamageMultiplier = IncomingMultiplier;

        var world = new ShardWorldState(_adds.Count, _anchor is { IsDead: false }, inZone);

        foreach (var evt in _fight.Tick(delta, _self.Health.Fraction, world))
        {
            Handle(evt);
        }
    }

    private void Handle(ShardEvent evt)
    {
        switch (evt.Kind)
        {
            case ShardEventKind.Engaged:
                GD.Print($"[shard] {ShardId} engaged ({_modifier})");
                EmitSignal(SignalName.EncounterChanged);
                break;

            case ShardEventKind.SpawnWave:
                SpawnWave(evt.Phase);
                break;

            case ShardEventKind.PulseTelegraph:
                _telegraph.Begin(
                    TelegraphShape.Circle, GlobalPosition, Vector3.Forward,
                    (float)_tier!.PulseRadius, 360f, evt.Duration);
                break;

            case ShardEventKind.PulseStrike:
                Pulse();
                break;

            case ShardEventKind.ReclamationStarted:
                GD.Print($"[shard] reclamation started — kill the anchor within {evt.Duration:F0}s");
                break;

            case ShardEventKind.ReclamationCompleted:
                _self.Heal((int)(_self.Stats.MaxHp * evt.Duration));
                CombatFeedback.Number(GlobalPosition + (Vector3.Up * 3.2f), (int)(evt.Duration * 100), true, false);
                GD.Print("[shard] reclamation completed — the shard healed");
                break;

            case ShardEventKind.ReclamationInterrupted:
                GD.Print("[shard] reclamation interrupted");
                break;

            case ShardEventKind.Broken:
                Break();
                break;
        }
    }

    // ------------------------------------------------------------------ waves

    private void SpawnWave(ShardPhase phase)
    {
        if (_tier?.WaveFor(phase) is not { } wave || _enemyScene is null || !GameContent.IsLoaded) return;

        var roster = new Kiln.Data.Encounters.ShardCatalogue(GameContent.Database);
        var composed = WaveComposer.Compose(wave, roster, _tier.Level, GameItems.EncounterRng);
        var index = 0;

        foreach (var entry in composed)
        {
            var angle = Mathf.Tau * index / Mathf.Max(1, composed.Count);
            var offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * SpawnRing;

            if (_enemyScene.Instantiate() is not EnemyBrain add) continue;

            add.EnemyId = entry.EnemyId;
            add.Name = $"Add_{phase}_{index}";

            // Adds belong to the fight, not to the world: they are cleared when the player
            // leaves and when the shard breaks.
            add.MoveSpeed *= (float)_fight!.Effects.AddSpeedMultiplier;

            _addsRoot.AddChild(add);
            add.PlaceAt(GlobalPosition + offset);

            _adds.Add(add);

            if (entry.IsAnchor)
            {
                _anchor = add;
                add.GetNodeOrNull<VisualRoot>("VisualRoot")?.Apply("mesh_placeholder_humanoid", "#ffd24f", 1.15);

                // The one add the player has to find in a crowd, so it gets a louder plate
                // and says why it matters.
                add.Plate.Rank = NameRank.Elite;
                add.Plate.Tint = new Color("ffd24f");
                add.Plate.Offset = new Vector3(0, 2.7f, 0);
                add.LabelPlate("ANCHOR");
            }

            index++;
        }

        GD.Print($"[shard] wave {phase}: {composed.Count} adds"
            + (composed.Any(c => c.IsAnchor) ? " (anchor marked)" : ""));
    }

    private void PruneAdds() => _adds.RemoveAll(a => !IsInstanceValid(a) || a.IsDead);

    private void ClearAdds()
    {
        foreach (var add in _adds.Where(IsInstanceValid)) add.QueueFree();

        _adds.Clear();
        _anchor = null;
    }

    // ------------------------------------------------------------------ pulse

    private void Pulse()
    {
        if (_tier is null) return;

        AoeVisual.Circle(GlobalPosition, (float)_tier.PulseRadius, hostile: true);
        Camera.CameraRig.Kick(Vector3.Up, 0.35f);

        foreach (var victim in AreaQuery.Sphere(this, GlobalPosition, (float)_tier.PulseRadius, Foundation.Layers.Player))
        {
            victim.TakeAttack(_self, skillCoef: _tier.PulseDamageCoef);

            if (_fight?.Effects.PoisonPulse == true)
            {
                victim.Statuses.Apply(new StatusEffect
                {
                    Kind = StatusKind.Poison,
                    Magnitude = _tier.Level * 0.6,
                    Duration = 5.0,
                });
            }
        }
    }

    /// <summary>Damage taken by the shard, after the Warded modifier has had its say.</summary>
    public double IncomingMultiplier => 1.0 - (_fight?.DamageReduction(_adds.Count) ?? 0);

    // ------------------------------------------------------------------ break

    private void Break()
    {
        _broken = true;
        _respawnIn = RespawnSeconds;
        _telegraph.Cancel();

        GD.Print($"[shard] {ShardId} broken");

        // The shockwave clears what is left, so the fight ends on the break rather than on a
        // mop-up. Winning should feel like winning.
        foreach (var add in _adds.Where(IsInstanceValid))
        {
            add.Self.ApplyDamage((int)System.Math.Ceiling(add.Self.Health.Current), critical: true);
        }

        ClearAdds();
        AoeVisual.Circle(GlobalPosition, ZoneRadius * 0.5f, hostile: false);
        Camera.CameraRig.Kick(Vector3.Up, 1.0f);

        GrantRewards();

        if (_visual is not null) _visual.Visible = false;

        _plate.Visible = false;

        EmitSignal(SignalName.EncounterChanged);
    }

    private void GrantRewards()
    {
        if (!GameContent.IsLoaded || !GameItems.IsLoaded || _tier is null) return;
        if (GetTree().GetFirstNodeInGroup("player") is not Node3D player) return;

        var bag = player.GetNodeOrNull<PlayerInventory>("PlayerInventory");
        var character = player.GetNodeOrNull<Player.PlayerCharacter>("PlayerCharacter");

        character?.AwardKill(_tier.Level, ExperienceForTier());

        if (bag is null) return;

        var table = _tier.DropTable is not null
            ? GameContent.Database.DropTables.GetValueOrDefault(_tier.DropTable)
            : null;

        // A shard always gives something. The burst is the payoff for a fight the player
        // could not walk away from.
        var rng = GameItems.LootRng;

        for (var i = 0; i < 3; i++)
        {
            if (LootRoller.RollOne(table, rng) is { } item) LootDrop.Spawn(GetParent(), item, GlobalPosition, rng);
        }

        var loot = LootRoller.Roll(table, rng);

        if (loot.Yang > 0)
        {
            var yang = loot.Yang * 6;
            bag.Bag.AddYang(yang);
            CombatFeedback.Yang(GlobalPosition + (Vector3.Up * 2.4f), yang);
        }

        foreach (var item in loot.Items) LootDrop.Spawn(GetParent(), item, GlobalPosition, rng);

        var essence = EssenceForTier();

        if (essence > 0 && bag.Bag.TryGrant("mat_shard_essence", essence))
        {
            GD.Print($"[shard] +{essence} shard essence");
        }
    }

    private int ExperienceForTier() =>
        (int)Kiln.Core.Progression.ExperienceTable.ShardXp(_tier!.Tier, _tier.Level);

    private int EssenceForTier() =>
        GameContent.Database.Shards.TryGetValue(ShardId, out var def) ? def.Essence : 1;

    private void TickRespawn(double delta)
    {
        _respawnIn -= delta;

        if (_respawnIn > 0) return;

        // Never respawn under the player's feet.
        if (GetTree().GetFirstNodeInGroup("player") is Node3D player
            && GlobalPosition.DistanceTo(player.GlobalPosition) < ZoneRadius)
        {
            _respawnIn = 5;
            return;
        }

        Arm();
    }

    /// <summary>Player left: reset the fight and clear its adds.</summary>
    private void Disengage()
    {
        GD.Print($"[shard] {ShardId} reset — the player left the zone");

        ClearAdds();
        _telegraph.Cancel();
        _fight?.Reset();
        _self.Health.Fill();

        EmitSignal(SignalName.EncounterChanged);
    }
}
