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
    [Export] public string ShardId { get; set; } = "shd_mossbound_stone";

    /// <summary>Radius of the corruption zone. Entering it starts the fight.</summary>
    [Export] public float ZoneRadius { get; set; } = 18f;

    /// <summary>Seconds before a broken node returns (doc 02 §3).</summary>
    /// <summary>
    /// Two minutes, since the map themes (2026-09-23): three shards to a map and a short wait
    /// make them the map's recurring fight rather than a once-an-hour landmark.
    /// </summary>
    [Export] public double RespawnSeconds { get; set; } = 120.0;

    /// <summary>
    /// Whether a break is remembered across loads and the stone comes back on a timer.
    /// </summary>
    /// <remarks>
    /// False for a stone a tower floor puts out: the floor is its lifetime, and a stone still
    /// "broken" from the last visit would leave a floor with nothing to break.
    /// </remarks>
    [Export] public bool Persistent { get; set; } = true;

    /// <summary>
    /// Whether walking out of the zone abandons the fight and heals the stone. Off on a tower
    /// floor: the room is the zone, and a race between four stones is walking away from three.
    /// </summary>
    [Export] public bool Abandons { get; set; } = true;

    /// <summary>Whether the adds go straight for the player wherever they land (tower floors).</summary>
    [Export] public bool HuntingAdds { get; set; }

    /// <summary>
    /// Takes a random spot on the map each time it appears (REF-06): the field shards have no
    /// fixed place, so finding one is part of the map and not a route to learn.
    /// </summary>
    [Export] public bool Roams { get; set; }

    /// <summary>How far from the map's centre a roaming shard may stand, in metres.</summary>
    private const float RoamExtent = 62f;

    private bool _awaitingPlace;
    private int _roamAttempts;
    private CollisionShape3D? _collision;
    private bool _bringsBoss;

    /// <summary>Raised once when the stone breaks.</summary>
    public event System.Action? Broken;

    /// <summary>Where adds appear, as a ring around the shard.</summary>
    [Export] public float SpawnRing { get; set; } = 7f;

    private ShardTier? _tier;
    private ShardEncounter? _fight;
    private ShardModifier _modifier;
    private Combatant _self = null!;
    private VisualRoot? _visual;
    private ShardGlow? _glow;
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

        // Veins, orbiting crystals and a light: what makes a rock read as a shard.
        _glow = new ShardGlow { Name = "Glow" };
        AddChild(_glow);
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

        var collision = _collision = new CollisionShape3D
        {
            Name = "Collision",
            Shape = new CylinderShape3D { Radius = 1.2f, Height = 3.2f },
            Position = new Vector3(0, 1.6f, 0),
        };

        AddChild(collision);

        CallDeferred(nameof(Wake));
    }

    /// <summary>
    /// Stands the shard up — unless it was broken not long ago, in this map or before a save,
    /// in which case it stays down for the rest of its timer.
    /// </summary>
    private void Wake()
    {
        if (Persistent && PlayerProfile.ShardRespawns.TryGetValue(Key, out var due) && due > PlayerProfile.PlayTime)
        {
            _broken = true;
            _respawnIn = due - PlayerProfile.PlayTime;
            Show(false);

            GD.Print($"[shard] {ShardId} still broken — back in {_respawnIn:F0} s");
            return;
        }

        Arm();
    }

    /// <summary>Loads the tier, rolls the modifier and readies the node for a fresh fight.</summary>
    private void Arm()
    {
        if (!GameContent.IsLoaded) return;

        PlayerProfile.ShardRespawns.Remove(Key);

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

            // The shard's own look from its definition — a themed stone per map. The modifier
            // no longer repaints the stone; it colours the veins, below, and the name.
            var look = GameContent.Database.Shards.TryGetValue(ShardId, out var shardDef)
                ? shardDef.Visual
                : "mesh_placeholder_monolith";

            _visual.Apply(look, ModifierTint(_modifier), 1.0);
            _glow?.Dress(look, _modifier == ShardModifier.None ? null : new Color(ModifierTint(_modifier)));
        }

        var label = GameContent.Database.Shards.TryGetValue(ShardId, out var def)
            ? GameItems.Localise(def.Name)
            : ShardId;

        // The modifier is part of the name, because it is the thing the player needs to know
        // before deciding whether to walk in.
        _plate.SetText(_modifier == ShardModifier.None ? label : $"{label}  ·  {UI.Words.Of(_modifier)}");
        _plate.Tint = new Color(ModifierTint(_modifier));
        _plate.Visible = true;

        // One fight in twenty on the field calls the map's boss in at phase two.
        _bringsBoss = Persistent
            && GameContent.Database.Shards.TryGetValue(ShardId, out var chanceDef)
            && GameItems.EncounterRng.Chance(chanceDef.BossChance)
            && Kiln.Data.Encounters.ShardCatalogue.ZoneBoss(GameContent.Database, GameWorld.CurrentZoneId) is not null;

        // A roaming stone stays out of sight until it has somewhere to stand.
        if (Roams)
        {
            _awaitingPlace = true;
            _roamAttempts = 0;
            Show(false);
        }
        else
        {
            Show(true);
        }

        GD.Print($"[shard] {ShardId} armed — tier {_tier.Tier}, {_tier.MaxHp:N0} hp, modifier {_modifier}"
            + (_bringsBoss ? ", calls the boss" : ""));

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

        if (_awaitingPlace)
        {
            if (!Roam()) return;

            _awaitingPlace = false;
            Show(true);
        }

        PruneAdds();

        var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
        var inZone = player is not null && GlobalPosition.DistanceTo(player.GlobalPosition) <= ZoneRadius;

        // Walking out abandons the fight. Without this a shard could be whittled down over
        // many visits, which turns the encounter into a chore rather than a fight.
        if (_fight.IsActive && !inZone && Abandons)
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

        if (phase == ShardPhase.Two && _bringsBoss) SummonBoss();

        // The map's own creatures, so the stone belongs to where it stands.
        var roster = new Kiln.Data.Encounters.ShardCatalogue(
            GameContent.Database,
            Kiln.Data.Encounters.ShardCatalogue.ZonePool(GameContent.Database, GameWorld.CurrentZoneId));
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

            if (HuntingAdds) add.Hunt();

            if (entry.IsAnchor)
            {
                _anchor = add;
                // Its own body, larger and gilded: a gold skeleton standing in for a wolf or an
                // orc read as a different creature altogether.
                if (GameContent.Database.Enemies.TryGetValue(entry.EnemyId, out var anchorDef))
                {
                    add.GetNodeOrNull<VisualRoot>("VisualRoot")?.Apply(anchorDef.Visual, "#ffd24f", anchorDef.VisualScale * 1.2);
                }

                // The one add the player has to find in a crowd, so it gets a louder plate
                // and says why it matters.
                add.Plate.Rank = NameRank.Elite;
                add.Plate.Tint = new Color("ffd24f");
                add.Plate.Offset = new Vector3(0, 2.7f, 0);
                add.LabelPlate(L10n.T("ANCHOR"));
            }

            index++;
        }

        GD.Print($"[shard] wave {phase}: {composed.Count} adds"
            + (composed.Any(c => c.IsAnchor) ? " (anchor marked)" : ""));
    }

    /// <summary>
    /// The map's boss, called by the stone (REF-06). Not one of the adds: breaking the stone
    /// does not kill it, it is fought for itself, and it leaves once it gives the fight up.
    /// </summary>
    private void SummonBoss()
    {
        _bringsBoss = false;

        var id = Kiln.Data.Encounters.ShardCatalogue.ZoneBoss(GameContent.Database, GameWorld.CurrentZoneId);

        if (id is null || _enemyScene?.Instantiate() is not EnemyBrain boss) return;

        boss.EnemyId = id;
        boss.Name = $"{Name}_Boss_{Time.GetTicksMsec()}";
        boss.LeavesOnReset = true;

        (GetTree().CurrentScene?.GetNodeOrNull<Node3D>("Enemies") ?? GetParent()).AddChild(boss);
        boss.PlaceAt(GlobalPosition + new Vector3(SpawnRing, 0, 0).Rotated(Vector3.Up, (float)GameItems.EncounterRng.NextDouble(0, Mathf.Tau)));
        boss.Engage();

        UI.WorldNotice.Show(GetTree(), L10n.F("{0} answers the stone's call!", GameItems.Localise(boss.Self.DisplayName)));
        Camera.CameraRig.Kick(Vector3.Up, 1.0f);
        GD.Print($"[shard] {ShardId} called {id}");
    }

    /// <summary>Shows or hides the stone, and takes its body out of the way while hidden.</summary>
    private void Show(bool shown)
    {
        if (_visual is not null) _visual.Visible = shown;
        if (_glow is not null) _glow.Visible = shown;

        _plate.Visible = shown;
        _collision?.SetDeferred(CollisionShape3D.PropertyName.Disabled, !shown);
    }

    /// <summary>
    /// Finds the stone a spot: on walkable ground, clear of anything built or grown there, and
    /// well away from villages, borders, shrines, camps, the other stones and the player.
    /// </summary>
    /// <remarks>
    /// Waits for the navigation mesh, which is only ready a frame or two after the map loads.
    /// A map crowded enough to turn every try down loosens the rules rather than leave the
    /// stone out of the world: first the clearance, then everything but safe ground.
    /// </remarks>
    private bool Roam()
    {
        var map = GetWorld3D().NavigationMap;

        if (NavigationServer3D.MapGetIterationId(map) == 0) return false;

        var attempt = _roamAttempts++;
        var strict = attempt < 6;
        var loose = attempt >= 12;
        var rng = GameItems.EncounterRng;

        for (var i = 0; i < 40; i++)
        {
            var candidate = new Vector3(
                (float)rng.NextDouble(-RoamExtent, RoamExtent),
                0,
                (float)rng.NextDouble(-RoamExtent, RoamExtent));

            var point = NavigationServer3D.MapGetClosestPoint(map, candidate);

            if ((point with { Y = 0 }).DistanceTo(candidate) > 1.5f) continue;
            if (!loose && !FarFromEverything(point)) continue;
            if (loose && GameWorld.IsSafe(point)) continue;
            if (strict && !Clear(point)) continue;

            GlobalPosition = point;
            GD.Print($"[shard] {Name} ({ShardId}) stands at {point.X:F0}, {point.Z:F0}");
            return true;
        }

        return false;
    }

    private bool FarFromEverything(Vector3 point)
    {
        // Its whole zone clear of safe ground, or walking out of the village starts the fight.
        if (GameWorld.IsSafe(point)) return false;

        for (var k = 0; k < 8; k++)
        {
            var edge = point + (new Vector3(ZoneRadius + 4f, 0, 0).Rotated(Vector3.Up, Mathf.Tau * k / 8));
            if (GameWorld.IsSafe(edge)) return false;
        }

        if (Near(point, "zone_gates", 24f) || Near(point, "zone_arrivals", 24f) || Near(point, "shrines", 16f)) return false;

        foreach (var node in GetTree().GetNodesInGroup("shards"))
        {
            // One still looking for its own spot is nowhere yet.
            if (node is ShardNode { _awaitingPlace: false } other && other != this && other.GlobalPosition.DistanceTo(point) < 30f) return false;
        }

        foreach (var node in GetTree().GetNodesInGroup("spawn_fields"))
        {
            if (node is not SpawnFieldNode field) continue;

            var boss = field.Def?.Entries.Any(e => GameContent.Database.Enemies.TryGetValue(e.EnemyId, out var def) && def.Boss) == true;
            var keep = boss ? 28f : (float)(field.Def?.Radius ?? 7.0) + 6f;

            if (field.GlobalPosition.DistanceTo(point) < keep) return false;
        }

        return GetTree().GetFirstNodeInGroup("player") is not Node3D player
            || player.GlobalPosition.DistanceTo(point) >= ZoneRadius + 6f;
    }

    private bool Near(Vector3 point, string group, float distance)
    {
        foreach (var node in GetTree().GetNodesInGroup(group))
        {
            if (node is Node3D marker && marker.GlobalPosition.DistanceTo(point) < distance) return true;
        }

        return false;
    }

    /// <summary>Nothing solid where the stone would stand: no house, rock, tree or wall.</summary>
    private bool Clear(Vector3 point)
    {
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = new SphereShape3D { Radius = 2.2f },
            Transform = new Transform3D(Basis.Identity, point + (Vector3.Up * 2.5f)),
            CollisionMask = Foundation.Layers.World,
            Exclude = [GetRid()],
        };

        return GetWorld3D().DirectSpaceState.IntersectShape(query, 1).Count == 0;
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

    /// <summary>Which shard this is, across scenes and saves.</summary>
    private string Key => $"{GameWorld.CurrentZoneId}:{Name}";

    private void Break()
    {
        Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndShardBreak, GlobalPosition);
        _broken = true;
        _respawnIn = Persistent ? RespawnSeconds : double.MaxValue;

        if (Persistent) PlayerProfile.ShardRespawns[Key] = PlayerProfile.PlayTime + RespawnSeconds;
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

        // The shard finishes being broken before anybody is paid. Rewards reach across into
        // the player's bag and the experience table, and when that threw, the shard was left
        // standing there at zero health with its name still floating over it — beaten, and
        // still on the map. Whether the shard is gone is the shard's own business.
        Show(false);

        EmitSignal(SignalName.EncounterChanged);

        GrantRewards();
        Quests.QuestTracker.Report(GetTree(), ObjectiveType.Shard, ShardId);

        Broken?.Invoke();
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

        // A shard always gives something: its own table (REF-06), whose picks are its
        // guaranteed drops, with the yang in the table rather than multiplied here.
        var rng = GameItems.LootRng;
        var loot = LootRoller.Roll(table, rng);

        if (loot.Yang > 0)
        {
            bag.Bag.AddYang(loot.Yang);
            CombatFeedback.Yang(GlobalPosition + (Vector3.Up * 2.4f), loot.Yang);
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
