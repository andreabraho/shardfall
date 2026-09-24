using System.Collections.Generic;
using Godot;
using Kiln.Core.Ai;
using Kiln.Core.Combat;
using Kiln.Core.Foundation;
using Kiln.Core.Progression;
using Kiln.Data.Definitions;
using Kiln.Game.Foundation;
using Kiln.Game.Visual;

namespace Kiln.Game.Combat;

/// <summary>
/// An enemy. Movement, attacking and perception live here as primitives; what to do with
/// them is decided by a behaviour tree chosen from the definition's role (CBT-06/07).
/// </summary>
/// <remarks>
/// The split matters: five roles share chase, leash and attack, and differ in a handful of
/// branches. Composing trees from shared primitives is what stops ~55 enemies at v1.0 from
/// becoming fifty-five near-identical state machines.
/// </remarks>
public partial class EnemyBrain : CharacterBody3D
{
    private enum Phase
    {
        Idle,
        Windup,
        Recover,
    }

    private static readonly AbilityDef DefaultMelee = new()
    {
        Id = "abl_default_melee",
        Cooldown = 1.8,
        Windup = 0.55,
        DamageCoef = 1.0,
    };

    private NavigationAgent3D _agent = null!;
    private Node3D? _visual;
    private HealthBar3D? _bar;
    private TelegraphVisual? _telegraph;
    private RoleMarker _marker = null!;
    private NamePlate _plate = null!;

    /// <summary>The floating name, so an encounter can promote one add to elite.</summary>
    public NamePlate Plate => _plate;

    private readonly Dictionary<string, double> _abilityCooldowns = new(System.StringComparer.Ordinal);
    private readonly List<Combatant> _shielded = [];

    private BtNode<EnemyBrain>? _tree;
    private Phase _phase = Phase.Idle;
    private double _phaseTimer;
    private AbilityDef? _activeAbility;
    private Vector3 _committedFacing;
    private Vector3 _telegraphCenter;
    private Vector3 _home;
    private bool _dead;

    private EnemyTraitsDef _traits = new();
    private BossPhaseDef[] _phases = [];
    private int _phaseIndex;
    private double _cooldownScale = 1.0;
    private StatusApplicationDef? _phaseApplies;
    private readonly List<EnemyBrain> _adds = [];

    /// <summary>A boss (REF-05): boss plate and bar, the boss leash, half healing on reset.</summary>
    public bool IsBoss { get; private set; }

    /// <summary>Called in by something else — a shard — and gone once it gives the fight up.</summary>
    public bool LeavesOnReset { get; set; }

    /// <summary>Turns on the player at once, wherever they are, without calling others in.</summary>
    public void Engage() => AcquireTarget(force: true, alert: false);

    /// <summary>A unique boss, shown in the boss bar at the top of the screen.</summary>
    public bool HasBossBar { get; private set; }

    /// <summary>1 until the first phase turns, then 2, 3.</summary>
    public int BossPhase => _phaseIndex + 1;

    /// <summary>How far the player may draw a boss before it gives up: measured from them, not from home.</summary>
    private const float BossLeash = 40f;
    private bool _enraged;
    private double _downedFor;
    private MeshInstance3D? _shieldRing;

    /// <summary>Seconds a creature that rises lies on the ground first.</summary>
    private const double DownedSeconds = 1.8;

    private static readonly Color EnrageColour = new("ff5a3a");
    private static readonly Color RiseColour = new("c9d6ff");
    private bool _basicChosen;
    private bool _retreating;
    private double _retreatBudget;
    private bool _returning;
    private bool _evicted;
    private int _deadFrames;

    [Export] public string EnemyId { get; set; } = "mob_field_rat";

    /// <summary>The colour of every creature's name.</summary>
    public static readonly Color EnemyNameColour = new("ff4a3a");

    /// <summary>0..1 off any chance to stun this creature; 1 is immune (REF-01).</summary>
    public double StunResist =>
        GameContent.IsLoaded && GameContent.Database.Enemies.TryGetValue(EnemyId, out var def) ? def.StunResist : 0;

    [Export] public float AggroRadius { get; set; } = 12f;
    [Export] public float LeashRadius { get; set; } = 35f;
    [Export] public float AttackRange { get; set; } = 2.2f;
    [Export] public float MoveSpeed { get; set; } = 4.5f;
    [Export] public double RecoverSeconds { get; set; } = 0.8;
    [Export] public float MeleeArc { get; set; } = 110f;
    [Export] public float Gravity { get; set; } = 24f;

    /// <summary>Seconds of continuous backing-off allowed before a ranged role must stand and fight.</summary>
    [Export] public double RetreatSeconds { get; set; } = 1.2;

    /// <summary>How far from its spawn a ranged role will back off. Far tighter than the aggro leash.</summary>
    [Export] public float RetreatTether { get; set; } = 10f;

    /// <summary>Seconds of budget regained per second while not retreating.</summary>
    [Export] public double RetreatRefillRate { get; set; } = 0.5;

    public Combatant Self { get; private set; } = null!;
    public EnemyRole Role { get; private set; } = EnemyRole.Bruiser;
    public Node3D? Target { get; private set; }
    public Combatant? TargetCombatant { get; private set; }
    public AbilityDef[] Abilities { get; private set; } = [DefaultMelee];

    private readonly List<AbilityDef> _specials = [];
    private int _lastSpecial = -1;

    /// <summary>
    /// The telegraphed attack to try next: the first one after the last used that is ready,
    /// preferring one already in range.
    /// </summary>
    /// <remarks>
    /// Only the first telegraphed ability used to be read, so a boss's second and third
    /// attacks — Greymane's pounce, the Demon Lord's cataclysm — were written, tuned and never
    /// seen (REF-04). Taking them in turn after the last one used keeps a short-cooldown opener
    /// from crowding out the rest.
    /// </remarks>
    public AbilityDef? SpecialAbility
    {
        get
        {
            if (_specials.Count == 0) return null;

            // Mid-attack, the answer must not change under the wind-up.
            if (_activeAbility is { Telegraph: not null } active) return active;

            AbilityDef? ready = null;

            for (var i = 1; i <= _specials.Count; i++)
            {
                var index = (_lastSpecial + i) % _specials.Count;
                var ability = _specials[index];

                if (!AbilityReady(ability)) continue;
                if (HasLivingTarget && InRangeOf(ability)) return ability;

                ready ??= ability;
            }

            return ready ?? _specials[(_lastSpecial + 1) % _specials.Count];
        }
    }

    /// <summary>First untelegraphed ability, used as the filler swing.</summary>
    public AbilityDef BasicAbility { get; private set; } = DefaultMelee;

    public bool IsDead => _dead;

    /// <summary>How wide the creature counts as for a click: its model's footprint.</summary>
    public float ClickRadius => Mathf.Max(0.5f, (_visual as VisualRoot)?.GroundRadius ?? 0.45f);

    /// <summary>How tall the creature counts as for a click: its model's top.</summary>
    public float ClickHeight => Mathf.Max(1.2f, (_visual as VisualRoot)?.TopHeight ?? 1.7f);

    /// <summary>Experience granted on death, straight from the definition.</summary>
    public int XpReward { get; private set; }

    /// <summary>The table this enemy rolls on death, or null when it drops nothing.</summary>
    public string? DropTableId { get; private set; }

    public override void _Ready()
    {
        _agent = GetNode<NavigationAgent3D>("NavigationAgent3D");
        Self = GetNode<Combatant>("Combatant");
        _visual = GetNodeOrNull<Node3D>("VisualRoot");
        _bar = GetNodeOrNull<HealthBar3D>("HealthBar3D");

        _telegraph = new TelegraphVisual { Name = "Telegraph" };
        CallDeferred(Node.MethodName.AddChild, _telegraph);

        _marker = new RoleMarker { Name = "RoleMarker" };
        _plate = new NamePlate { Name = "NamePlate" };

        // The spawn fields and the population cap both count this group, so every enemy has
        // to be in it however it arrived — placed in the scene, spawned by a field or by a shard.
        AddToGroup("enemies");

        // Correct only for an enemy placed in the scene file. Anything spawned is positioned
        // after AddChild, and AddChild is what runs this — so a spawner must call PlaceAt.
        _home = GlobalPosition;
        _agent.PathDesiredDistance = 0.5f;
        _agent.TargetDesiredDistance = AttackRange * 0.8f;
        _agent.AvoidanceEnabled = false;

        LoadDefinition();
        _tree = RoleTrees.Build(Role);

        // Role is known only after the definition loads, so the marker is configured and
        // parented here rather than alongside the telegraph.
        _marker.Role = Role;
        CallDeferred(Node.MethodName.AddChild, _marker);

        // Tinted to match the role marker: shape, colour and name all say the same thing, so
        // none of them has to be learned on its own.
        // Every creature's name in red, its level before it in green (2026-09-24). The role
        // still shows in the marker at its feet.
        _plate.Tint = EnemyNameColour;

        // A boss wears the boss plate — larger, violet — and is what the boss bar looks for.
        if (IsBoss)
        {
            _plate.Tint = null;
            _plate.Rank = NameRank.Boss;
            AddToGroup("bosses");
        }
        CallDeferred(Node.MethodName.AddChild, _plate);
        _retreatBudget = RetreatSeconds;

        Self.Damaged += OnDamaged;
        Self.Died += OnDied;
        Self.HealthChanged += f => _bar?.SetFraction(f);
        Self.HealthChanged += CheckEnrage;
        Self.HealthChanged += CheckPhases;
        Self.Collapsed += OnCollapsed;
    }

    private void LoadDefinition()
    {
        if (!GameContent.IsLoaded || !GameContent.Database.Enemies.TryGetValue(EnemyId, out var def))
        {
            GD.PushWarning($"EnemyBrain '{Name}': unknown enemy id '{EnemyId}'.");
            return;
        }

        Self.ConfigureFromEnemy(def);

        Role = def.Role;
        AggroRadius = (float)def.AggroRadius;
        LeashRadius = (float)def.LeashRadius;
        MoveSpeed = (float)def.Stats.MoveSpeed;
        XpReward = def.Xp;
        DropTableId = def.DropTable;
        _traits = def.Traits;
        IsBoss = def.Boss;
        HasBossBar = def.BossBar;
        _phases = def.Phases;
        Self.Thorns = _traits.Thorns;
        Self.CheatDeath = _traits.Rise > 0;
        LabelPlate();

        if (def.Abilities.Length > 0) Abilities = def.Abilities;

        foreach (var ability in Abilities)
        {
            if (ability.Telegraph is not null)
            {
                _specials.Add(ability);
                continue;
            }

            // Skip zero-damage utility abilities: a support caster whose "basic attack"
            // is its buff would stand there dealing nothing.
            if (ability.DamageCoef > 0 && !_basicChosen)
            {
                BasicAbility = ability;
                _basicChosen = true;
            }
        }

        if (_visual is VisualRoot root && !string.IsNullOrEmpty(def.Visual))
        {
            root.Apply(def.Visual, def.VisualTint, def.VisualScale);
            
            // Over the model, however tall it is. At a fixed height a big creature — a queen, a
            // colossus — stood with its own name inside its body.
            var top = root.TopHeight;
            if (_bar is not null) _bar.Offset = new Vector3(0, Mathf.Max(2.1f, top + 0.3f), 0);
            _plate.Offset = new Vector3(0, Mathf.Max(2.4f, top + 0.62f), 0);
        }
    }

    /// <summary>
    /// Writes the floating label: the level first, then the name.
    /// </summary>
    /// <remarks>
    /// Level first because it is the part the player acts on. A name tells them what they are
    /// looking at; the number tells them whether to walk towards it, and in a world banded by
    /// level that decision is made at a glance from across the field.
    /// <para>
    /// The one place that composes it, so a creature promoted by an encounter does not end up
    /// as the only thing in the world whose level is not shown.
    /// </para>
    /// </remarks>
    public void LabelPlate(string? suffix = null)
    {
        var name = Items.GameItems.NameOfEnemy(EnemyId);

        _plate.SetText(suffix is null ? name : $"{name}  ·  {suffix}", Kiln.Core.Foundation.L10n.F("Lv. {0}", Self.Stats.Level));
    }

    // -- Signals ------------------------------------------------------------

    private void OnDamaged(int amount, bool critical, bool evaded)
    {
        CombatFeedback.Number(GlobalPosition + (Vector3.Up * 1.6f), amount, critical, evaded);

        if (!evaded) Audio.AudioDirector.Play(critical ? Kiln.Data.Ids.Sounds.SndHitCrit : Kiln.Data.Ids.Sounds.SndHit, GlobalPosition);

        if (!evaded)
        {
            // A flinch where it stands (REF-01): no frame freeze, no knockback, and the creature
            // goes on acting. The recoil points away from whoever is hitting it.
            (_visual as VisualRoot)?.Flash();

            var away = GetTree().GetFirstNodeInGroup("player") is Node3D player
                ? GlobalPosition - player.GlobalPosition
                : Vector3.Zero;

            (_visual as VisualRoot)?.Flinch(away);
        }

        // Being hit pulls an enemy into the fight from outside aggro range — but not while
        // it is walking home, or chasing and poking a leashing enemy restarts the fight.
        if (Target is null) AcquireTarget(force: true);
    }

    /// <summary>Enrages once, when health first drops past the trait's line.</summary>
    private void CheckEnrage(float fraction)
    {
        if (_enraged || _traits.EnrageBelow <= 0 || fraction > _traits.EnrageBelow || !Self.IsAlive) return;

        Enrage();
    }

    private void Enrage()
    {
        if (_enraged) return;

        _enraged = true;
        MoveSpeed *= (float)_traits.EnrageSpeed;
        Self.OutgoingDamageMultiplier = _traits.EnrageDamage;

        (_visual as VisualRoot)?.Flash(0.4);
        CombatFeedback.Callout(GlobalPosition + (Vector3.Up * 2.2f), L10n.T("Enraged!"), EnrageColour);
    }

    /// <summary>Turns every phase whose threshold health has fallen past, in order (REF-05).</summary>
    private void CheckPhases(float fraction)
    {
        while (_phaseIndex < _phases.Length && fraction <= _phases[_phaseIndex].At && Self.IsAlive)
        {
            BeginPhase(_phases[_phaseIndex]);
            _phaseIndex++;
        }
    }

    /// <summary>
    /// One phase's effects, and the announcement of it: a notice across the screen and a
    /// shake, so the change is read as the fight turning rather than as the boss glitching.
    /// </summary>
    private void BeginPhase(BossPhaseDef phase)
    {
        var name = Self.DisplayName.Length > 0 ? Items.GameItems.Localise(Self.DisplayName) : EnemyId;
        string? notice = null;

        if (phase.CooldownScale < 1)
        {
            _cooldownScale *= phase.CooldownScale;
            notice = L10n.F("{0} grows desperate!", name);
        }

        if (phase.Applies is not null) _phaseApplies = phase.Applies;

        if (phase.Enrage)
        {
            Enrage();
            notice = L10n.F("{0} flies into a rage!", name);
        }

        if (phase.Adds is { Length: > 0 } adds && phase.AddCount > 0)
        {
            CallAdds(adds, phase.AddCount);
            notice = L10n.F("{0} calls for help!", name);
        }

        if (phase.Teleport)
        {
            Blink();
            notice = L10n.F("{0} vanishes!", name);
        }

        UI.WorldNotice.Show(GetTree(), notice ?? L10n.F("{0} changes its tactics!", name));
        Camera.CameraRig.Kick(Vector3.Up, 0.8f);
        (_visual as VisualRoot)?.Flash(0.3);
    }

    /// <summary>Brings creatures to the boss's side, already fighting.</summary>
    private void CallAdds(string enemyId, int count)
    {
        var scene = GD.Load<PackedScene>("res://scenes/enemy.tscn");

        for (var i = 0; i < count; i++)
        {
            if (scene.Instantiate() is not EnemyBrain add) continue;

            add.EnemyId = enemyId;
            add.Name = $"{Name}_add_{Time.GetTicksMsec()}_{i}";
            GetParent().AddChild(add);

            var angle = Mathf.Tau * i / count;
            add.PlaceAt(GlobalPosition + (new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 3.5f));

            // A boss hunting the length of a tower floor calls hunters; one in a field calls
            // creatures that fight here and leash here.
            if (LeashRadius >= 9999f) add.Hunt();
            else add.AcquireTarget(force: true, alert: false);

            _adds.Add(add);
        }
    }

    /// <summary>Vanishes and reappears some way off from the player, on walkable ground.</summary>
    private void Blink()
    {
        if (Target is null) return;

        var away = (GlobalPosition - Target.GlobalPosition) with { Y = 0 };

        if (away.LengthSquared() < 0.01f) away = Vector3.Forward;

        var turn = (float)GD.RandRange(-1.2, 1.2);
        var dest = Target.GlobalPosition + (away.Normalized().Rotated(Vector3.Up, turn) * 11f);

        dest = NavigationServer3D.MapGetClosestPoint(GetWorld3D().NavigationMap, dest);

        CancelAbility();
        AoeVisual.Circle(GlobalPosition, 1.6f, hostile: true);
        GlobalPosition = dest;
        Velocity = Vector3.Zero;
        AoeVisual.Circle(dest, 1.6f, hostile: true);
    }

    /// <summary>Knocked down by what would have killed it: lie there, then get back up.</summary>
    private void OnCollapsed()
    {
        _downedFor = DownedSeconds;
        CancelAbility();
        ReleaseShields();
        Velocity = Vector3.Zero;

        _visual?.CreateTween().TweenProperty(_visual, "rotation:x", 1.35f, 0.25);
    }

    private void TickDowned(double delta)
    {
        _downedFor -= delta;

        if (_downedFor > 0) return;

        Self.StandUp(_traits.Rise);

        _visual?.CreateTween().TweenProperty(_visual, "rotation:x", 0f, 0.35);
        CombatFeedback.Callout(GlobalPosition + (Vector3.Up * 2.2f), L10n.T("Rises again!"), RiseColour);
    }

    private void OnDied()
    {
        _dead = true;
        Velocity = Vector3.Zero;
        _telegraph?.Cancel();
        ReleaseShields();
        AwardExperience();
        DropLoot();
        Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndEnemyDeath, GlobalPosition);

        // Last, after the loot: a quest completing pays out into the bag, and if that ever
        // threw it must not take the drop down with it — a death that half-happened is how
        // the Hollow Bloom once stayed on the map with its loot uncollectable.
        Quests.QuestTracker.Report(GetTree(), Kiln.Core.Foundation.ObjectiveType.Kill, EnemyId);

        if (_bar is not null) _bar.Visible = false;

        // Immune to hit-stop. The freeze drives Engine.TimeScale to a ten-thousandth, and a
        // death animation on scaled time stretches with it — so every swing the player lands
        // while a body is sinking holds that body on the field a little longer. Fighting a
        // camp means landing a lot of swings, which is how a corpse ends up standing there
        // with an empty health bar looking like a creature that refused to die.
        // The model's own death first, where it has one, a moment lying there, then the body
        // sinks away. Without the clip it sinks at once, as before.
        var fall = (_visual as VisualRoot)?.Die() ?? 0;
        var hold = fall > 0 ? System.Math.Min(fall, 1.6) + 0.35 : 0.15;

        var tween = CreateTween().SetIgnoreTimeScale();
        tween.TweenProperty(this, "position:y", Position.Y - 1.4f, 0.6).SetDelay(hold);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    /// <summary>
    /// Takes a body off the field even if its death animation never finishes.
    /// </summary>
    /// <remarks>
    /// "A creature the player killed leaves" is a promise; a tween completing is an
    /// implementation detail, and the promise should not depend on it. Reported rather than
    /// silently swept up, because a corpse that needed this is evidence of something else
    /// going wrong and the log line is the only place that survives to say so.
    /// <para>
    /// Counted in physics frames rather than seconds on purpose: hit-stop drives
    /// <c>Engine.TimeScale</c> to a ten-thousandth, so a watchdog measured in scaled seconds
    /// would stall in exactly the situation it exists to catch.
    /// </para>
    /// </remarks>
    private void WatchCorpse()
    {
        // Generous: a death clip, a moment on the ground and the sink come to under three
        // seconds, so five and a half at sixty ticks.
        const int GraceFrames = 330;

        if (++_deadFrames < GraceFrames) return;

        GD.PushWarning($"[combat] '{EnemyId}' was still on the field {GraceFrames} ticks after dying. Removing it.");
        QueueFree();
    }

    /// <summary>Hands the kill reward to the player.</summary>
    private void AwardExperience()
    {
        if (XpReward <= 0) return;

        var player = GetTree().GetFirstNodeInGroup("player");
        player?.GetNodeOrNull<Player.PlayerCharacter>("PlayerCharacter")?.AwardKill(Self.Stats.Level, XpReward);
    }

    /// <summary>
    /// Rolls the drop table (ITM-03). Yang goes straight into the purse — walking over coins
    /// is busywork — while items land on the ground so the player sees what fell and can
    /// decide whether it is worth the walk.
    /// </summary>
    private void DropLoot()
    {
        if (DropTableId is null || !GameContent.IsLoaded || !Items.GameItems.IsLoaded) return;
        if (!GameContent.Database.DropTables.TryGetValue(DropTableId, out var table)) return;

        var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
        var bag = player?.GetNodeOrNull<Items.PlayerInventory>("PlayerInventory");

        // Half the yang and half each item's chance from something far below the player.
        var playerLevel = player?.GetNodeOrNull<Combatant>("Combatant")?.Stats.Level ?? 1;
        var share = Kiln.Core.Progression.ExperienceTable.LootShare(playerLevel, Self.Stats.Level);

        var rng = Items.GameItems.LootRng;
        var loot = Items.LootRoller.Roll(table, rng, share);

        if (loot.Yang > 0 && bag is not null)
        {
            bag.Bag.AddYang(loot.Yang);
            CombatFeedback.Yang(GlobalPosition + (Vector3.Up * 1.2f), loot.Yang);
        }

        foreach (var item in loot.Items)
        {
            World.LootDrop.Spawn(GetParent(), item, GlobalPosition, rng);
        }
    }

    // -- Main loop ----------------------------------------------------------

    public override void _PhysicsProcess(double delta)
    {
        if (_dead)
        {
            WatchCorpse();
            return;
        }

        TickCooldowns(delta);

        if (Self.Downed)
        {
            TickDowned(delta);
            Brake(delta);
            ApplyGravity(delta);
            MoveAndSlide();
            return;
        }

        // Checked before the stun branch and before the tree: a creature in the air has no
        // opinion about any of it, and separation would only fight the throw.
        if (_shoveFor > 0)
        {
            CarryShove(delta);
            return;
        }

        if (Self.Statuses.IsStunned)
        {
            // A stun cancels a wind-up, or the player is warned of an attack that never lands.
            CancelAbility();
            Brake(delta);
            Separate(delta);
            ApplyGravity(delta);
            MoveAndSlide();
            return;
        }

        RefreshTarget();

        _retreating = false;
        _tree?.Tick(this, delta);

        // The budget only refills while standing its ground, so an archer that keeps being
        // chased keeps fighting instead of edging away a little more every second.
        if (!_retreating)
        {
            _retreatBudget = System.Math.Min(_retreatBudget + (delta * RetreatRefillRate), RetreatSeconds);
        }

        Separate(delta);
        ApplyGravity(delta);
        MoveAndSlide();
    }

    private void TickCooldowns(double delta)
    {
        if (_abilityCooldowns.Count == 0) return;

        foreach (var id in new List<string>(_abilityCooldowns.Keys))
        {
            if (_abilityCooldowns[id] > 0) _abilityCooldowns[id] -= delta;
        }
    }

    /// <summary>
    /// Puts a spawned creature down and fixes the point it leashes back to.
    /// </summary>
    /// <remarks>
    /// Both in one call, because they were two and drifted apart. <c>_Ready</c> takes the
    /// home from the node's position, but <c>_Ready</c> runs inside <c>AddChild</c> — before
    /// the spawner has moved the node anywhere. Every spawned creature therefore leashed to
    /// the scene origin, and with nothing to fight walked there and stood in the village
    /// square, which in a village map is exactly where the player is standing.
    /// </remarks>
    public void PlaceAt(Vector3 at)
    {
        GlobalPosition = at;
        _home = at;

        if (World.GameWorld.IsLoaded && World.GameWorld.IsSafe(at))
        {
            GD.PushWarning($"[spawn] '{EnemyId}' was placed on safe ground at {at}. It will walk back out.");
        }
    }

    /// <summary>
    /// Sends the creature straight at the player from wherever it stands, and never lets it go
    /// home (the Demon Tower, 2026-09-23).
    /// </summary>
    /// <remarks>
    /// A tower floor spawns its waves at the walls, and with field aggro radii most of them
    /// stood where they landed while the player fought in the middle of the room — the floor
    /// was quiet when it was meant to be under siege. A hunter's aggro and leash reach across
    /// any room, so it comes, and keeps coming.
    /// </remarks>
    public void Hunt()
    {
        AggroRadius = 9999f;
        LeashRadius = 9999f;
        AcquireTarget(force: true);
    }

    private void RefreshTarget()
    {
        // Whatever brought it here — a camp placed too close, a shove, a future bug — nothing
        // hostile stands in the village. The scene audit stops this at build time; this makes
        // it self-correcting at run time, because the promise is worth more than the diagnosis.
        if (!_returning && World.GameWorld.IsSafe(GlobalPosition))
        {
            // Reported rather than quietly corrected. Silent self-healing here is how the
            // first version of this hid a broken home position for a day: everything looked
            // fine except to the person playing it.
            if (!_evicted)
            {
                _evicted = true;
                GD.PushWarning(
                    $"[safe] '{EnemyId}' was standing in a safe region at {GlobalPosition}, "
                    + $"home {_home}. Sending it back.");
            }

            BeginReturn();
            return;
        }

        // Crossing the leash gives up for good, rather than being re-evaluated each frame.
        // Without the latch the enemy oscillates on the boundary: step out, turn for home,
        // step back inside, charge again.
        if (!_returning && Target is not null && !WithinLeash)
        {
            BeginReturn();
            return;
        }

        // Reaching safe ground ends the chase, wherever the leash happens to be (WLD-12). The
        // village gate is the promise; making the player also outrun the tether to collect on
        // it would mean the gate does not quite work, which is worse than no gate at all.
        if (!_returning && Target is not null && World.GameWorld.IsSafe(Target.GlobalPosition))
        {
            BeginReturn();
            return;
        }

        if (_returning || TargetCombatant is { IsAlive: true }) return;

        Target = null;
        TargetCombatant = null;
        AcquireTarget(force: false);
    }

    private void BeginReturn()
    {
        _returning = true;
        Target = null;
        TargetCombatant = null;
        CancelAbility();
        ReleaseShields();
    }

    private void AcquireTarget(bool force, bool alert = true)
    {
        // Deaf while walking home. Otherwise chasing a leashing enemy re-aggros it a metre
        // outside its tether and it simply turns round again.
        if (_returning) return;

        if (GetTree().GetFirstNodeInGroup("player") is not Node3D player) return;

        if (!force && GlobalPosition.DistanceTo(player.GlobalPosition) > AggroRadius) return;

        // A passive creature only fights back: the small animals of the Hollow graze until hit.
        if (!force && _traits.Passive) return;

        // Either side of the line ends it: a forced acquisition from inside the village, and
        // a creature that has somehow ended up standing in one.
        if (World.GameWorld.IsSafe(player.GlobalPosition)) return;
        if (World.GameWorld.IsSafe(GlobalPosition)) return;

        var combatant = player.GetNodeOrNull<Combatant>("Combatant");

        // Every creature attacks, however far the player out-levels it (2026-09-24). Trivially
        // low-level creatures used to ignore the player (FR-2.7) so a cleared zone was quiet to
        // walk back through; in play it read as the world not noticing you. They still die to
        // a single blow, and they pay nothing.

        Target = player;
        TargetCombatant = combatant;

        if (alert) CallForHelp();
    }

    /// <summary>
    /// Pulls the creatures standing nearby into the fight (REF-04). Without it a camp came
    /// one at a time to whoever hit it first, which is not how a pack behaves and made every
    /// camp safe to pick apart from its edge. Twelve metres by default. One call, no chain: those who answer do not
    /// call again, or one wolf would bring the whole field.
    /// </summary>
    private void CallForHelp()
    {
        if (_traits.CallRadius <= 0) return;

        foreach (var ally in Allies((float)_traits.CallRadius))
        {
            if (ally.GetParent() is EnemyBrain { Target: null, _dead: false, _returning: false } other)
            {
                other.AcquireTarget(force: true, alert: false);
            }
        }
    }

    // -- Conditions the trees read -----------------------------------------

    public bool HasLivingTarget => TargetCombatant is { IsAlive: true };

    /// <summary>True while walking back to spawn. The trees must not fight during this.</summary>
    public bool IsReturning => _returning;

    /// <summary>Nothing to fight, or the leash has been crossed.</summary>
    public bool ShouldDisengage => _returning || !HasLivingTarget;

    public float DistanceToTarget =>
        Target is null ? float.MaxValue : GlobalPosition.DistanceTo(Target.GlobalPosition);

    /// <remarks>
    /// A boss measures from the player, not from home (REF-05): with a home leash, stepping
    /// back past it reset the fight and healed the boss, and that was the whole strategy.
    /// </remarks>
    public bool WithinLeash => IsBoss
        ? Target is null || GlobalPosition.DistanceTo(Target.GlobalPosition) <= BossLeash
        : GlobalPosition.DistanceTo(_home) <= LeashRadius;

    public bool AbilityReady(AbilityDef? ability) =>
        ability is not null && (!_abilityCooldowns.TryGetValue(ability.Id, out var cd) || cd <= 0);

    public float RangeOf(AbilityDef ability)
    {
        if (ability.Range > 0) return (float)ability.Range;

        return ability.Telegraph is { } tel ? (float)tel.Radius : AttackRange;
    }

    /// <summary>True when the ability is used from beyond melee, so it needs a projectile.</summary>
    private bool IsRanged(AbilityDef ability) => RangeOf(ability) > AttackRange + 0.5f;

    public bool InRangeOf(AbilityDef? ability) =>
        ability is not null && DistanceToTarget <= RangeOf(ability);

    /// <summary>Living allies within the radius, excluding this one.</summary>
    public List<Combatant> Allies(float radius)
    {
        var found = AreaQuery.Sphere(this, GlobalPosition, radius, Layers.Enemy);
        found.Remove(Self);
        return found;
    }

    // -- Actions the trees call --------------------------------------------

    /// <summary>Walks toward the target. Always Running: chasing has no natural end.</summary>
    /// <summary>
    /// Where a flanker heads: a point beside the player, swung round from the way it is coming
    /// in, so it closes from the side rather than head on. Near enough, it goes straight in.
    /// </summary>
    private Vector3 FlankPoint()
    {
        var target = Target!.GlobalPosition;
        var from = (GlobalPosition - target) with { Y = 0 };

        if (from.Length() < AttackRange + 2.5f || from.LengthSquared() < 0.0001f) return target;

        // Each flanker keeps to one side, so a pair of them splits rather than stacking.
        var side = (GetInstanceId() & 1) == 0 ? 1f : -1f;

        return target + (from.Normalized().Rotated(Vector3.Up, side * 1.3f) * (AttackRange + 1.5f));
    }

    public BtStatus Chase(double delta)
    {
        if (Target is null) return BtStatus.Failure;

        _agent.TargetPosition = _traits.Flank ? FlankPoint() : Target.GlobalPosition;

        if (_agent.IsNavigationFinished())
        {
            Brake(delta);
            return BtStatus.Running;
        }

        Steer((_agent.GetNextPathPosition() - GlobalPosition) with { Y = 0 }, delta);
        return BtStatus.Running;
    }

    /// <summary>
    /// Backs away from the target, but only so far.
    /// <para>
    /// Ranged roles are slower than the player, so "retreat while too close" never resolves
    /// while being chased — the enemy simply walks backwards to the edge of the map. Two
    /// limits stop that: a time budget per attempt, and a tether to where it started. When
    /// either runs out this fails and the tree falls through to attacking, so closing the
    /// distance is rewarded with the archer forced to stand and fight.
    /// </para>
    /// </summary>
    public BtStatus Retreat(double delta)
    {
        if (Target is null) return BtStatus.Failure;

        if (_retreatBudget <= 0) return BtStatus.Failure;

        if (GlobalPosition.DistanceTo(_home) > RetreatTether)
        {
            _retreatBudget = 0;
            Brake(delta);
            return BtStatus.Failure;
        }

        var away = (GlobalPosition - Target.GlobalPosition) with { Y = 0 };
        if (away.LengthSquared() < 0.0001f) return BtStatus.Failure;

        _retreating = true;
        _retreatBudget -= delta;

        Steer(away, delta);
        return BtStatus.Running;
    }

    public BtStatus Idle(double delta)
    {
        Brake(delta);
        return BtStatus.Running;
    }

    /// <summary>Walks back to where it started, and resets on arrival.</summary>
    public BtStatus ReturnHome(double delta)
    {
        if (GlobalPosition.DistanceTo(_home) < 1.0f)
        {
            Brake(delta);
            ArriveHome();
            return BtStatus.Success;
        }

        _agent.TargetPosition = _home;

        // GetNextPathPosition is what makes the agent compute the path, so asking
        // IsNavigationFinished before it reports "arrived" for a path that does not exist
        // yet — and since the answer never changes, the creature stands still for good.
        // That is how one wandered into the village and stayed there: nothing was chasing it
        // and nothing could bring it home.
        var step = (_agent.GetNextPathPosition() - GlobalPosition) with { Y = 0 };

        if (_agent.IsNavigationFinished() || step.LengthSquared() < 0.0001f)
        {
            Brake(delta);
            ArriveHome();
            return BtStatus.Success;
        }

        Steer(step, delta);
        return BtStatus.Running;
    }

    /// <summary>
    /// Back at the spawn point: drop the leash and restore.
    /// <para>
    /// Healing to full is not generosity — without it, pulling an enemy past its tether and
    /// walking away is free damage, and the whole encounter can be whittled down by
    /// repeating it. Resetting makes leashing a failed pull rather than a tactic.
    /// </para>
    /// </summary>
    private void ArriveHome()
    {
        if (!_returning) return;

        _returning = false;
        _retreatBudget = RetreatSeconds;

        Self.Statuses.Clear();

        // A boss gets back only half of what it lost: drawing it off still costs the player,
        // but it no longer undoes the fight.
        var missing = Self.Stats.MaxHp - Self.Health.Current;
        Self.Heal((int)System.Math.Ceiling(IsBoss ? missing / 2 : missing));

        // Its helpers go with the fight they were called to.
        foreach (var add in _adds)
        {
            if (IsInstanceValid(add) && !add.IsDead) add.QueueFree();
        }

        _adds.Clear();

        if (LeavesOnReset) QueueFree();
    }

    /// <summary>
    /// Runs an ability through wind-up, strike and recovery. Running until finished, so a
    /// tree branch owns the enemy for the whole commitment.
    /// </summary>
    public BtStatus UseAbility(AbilityDef? ability, double delta)
    {
        if (ability is null || !HasLivingTarget) return BtStatus.Failure;

        switch (_phase)
        {
            case Phase.Idle:
                if (!AbilityReady(ability) || !InRangeOf(ability)) return BtStatus.Failure;
                BeginWindup(ability);
                return BtStatus.Running;

            case Phase.Windup:
                Brake(delta);
                _phaseTimer -= delta;

                if (_phaseTimer > 0) return BtStatus.Running;

                Strike();
                return BtStatus.Running;

            case Phase.Recover:
                Brake(delta);
                _phaseTimer -= delta;

                if (_phaseTimer > 0) return BtStatus.Running;

                _phase = Phase.Idle;
                return BtStatus.Success;

            default:
                return BtStatus.Failure;
        }
    }

    /// <summary>
    /// True while a wind-up or recovery is in progress. Trees must let a committed attack
    /// finish rather than abandoning it — commitment is what makes the wind-up a real
    /// opening for the player (FR-3.3).
    /// </summary>
    public bool IsCommitted => _phase != Phase.Idle;

    public void CancelAbility()
    {
        if (_phase == Phase.Idle) return;

        // An abandoned wind-up still locks the ability out briefly. Without this, a branch
        // that keeps starting and dropping an attack re-places its telegraph every frame,
        // which reads as the warning circle chasing the player around.
        if (_activeAbility is not null)
        {
            _abilityCooldowns[_activeAbility.Id] =
                System.Math.Min(_activeAbility.Cooldown, CancelLockoutSeconds);
        }

        _telegraph?.Cancel();
        (_visual as VisualRoot)?.CancelWindUp();
        _phase = Phase.Idle;
        _activeAbility = null;
    }

    private const double CancelLockoutSeconds = 2.0;

    private void BeginWindup(AbilityDef ability)
    {
        _activeAbility = ability;
        _phase = Phase.Windup;

        // Difficulty stretches the reaction window (doc 02 §1); the content validator
        // guarantees it stays escapable at every tier.
        _phaseTimer = ability.Windup * GameSession.Difficulty.TelegraphScale;

        // Only a telegraphed attack warns out loud: that is the one the player must walk out of.
        if (ability.Telegraph is not null) Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndTelegraph, GlobalPosition);

        // And the body shows it too: a creature winding up rears back rather than idling.
        if (ability.Telegraph is not null) (_visual as VisualRoot)?.WindUp(_phaseTimer);

        if (Target is not null)
        {
            _committedFacing = (Target.GlobalPosition - GlobalPosition) with { Y = 0 };

            if (_committedFacing.LengthSquared() > 0.0001f)
            {
                _committedFacing = _committedFacing.Normalized();
                SnapFacing(_committedFacing);
            }
        }

        // A placed attack is committed to where the player stood at wind-up, not where they
        // end up. That is what makes walking out of it work.
        _telegraphCenter = ability.Placement == "target" && Target is not null
            ? Target.GlobalPosition
            : GlobalPosition;

        if (ability.Telegraph is { } tel && _telegraph is not null)
        {
            _telegraph.Begin(
                tel.Shape,
                _telegraphCenter,
                _committedFacing,
                (float)tel.Radius,
                (float)(tel.Angle > 0 ? tel.Angle : 90),
                _phaseTimer);
        }
    }

    private void Strike()
    {
        var ability = _activeAbility ?? DefaultMelee;

        _phase = Phase.Recover;
        _phaseTimer = RecoverSeconds;
        _abilityCooldowns[ability.Id] = ability.Cooldown * _cooldownScale;
        _telegraph?.Cancel();
        _activeAbility = null;

        if (_specials.IndexOf(ability) is >= 0 and var used) _lastSpecial = used;

        if (TargetCombatant is null || !TargetCombatant.IsAlive) return;

        // An untelegraphed attack from beyond melee launches a shot instead of resolving
        // instantly, so the player can still break line of sight or step aside.
        if (ability.Telegraph is null && IsRanged(ability))
        {
            (_visual as VisualRoot)?.Strike(_committedFacing, shot: true);

            Projectile.Spawn(
                GetParent(),
                Self,
                GlobalPosition + (Vector3.Up * 1.2f),
                TargetCombatant.Body.GlobalPosition + (Vector3.Up * 1.0f),
                ability.DamageCoef,
                Layers.Player,
                ability.Applies);

            return;
        }

        List<Combatant> hits;

        if (ability.Telegraph is { } tel)
        {
            // A placed attack thrown at the player is cast; one centred on itself is a blow.
            (_visual as VisualRoot)?.Strike(_committedFacing, shot: ability.Placement == "target");

            hits = tel.Shape == TelegraphShape.Cone
                ? AreaQuery.Cone(this, _telegraphCenter, _committedFacing, (float)tel.Radius,
                    (float)(tel.Angle > 0 ? tel.Angle : 90), Layers.Player)
                : AreaQuery.Sphere(this, _telegraphCenter, (float)tel.Radius, Layers.Player);

            AoeVisual.Circle(_telegraphCenter, (float)tel.Radius, hostile: true);
        }
        else
        {
            hits = AreaQuery.Cone(this, GlobalPosition, _committedFacing, AttackRange + 0.6f, MeleeArc, Layers.Player);
        }

        // Same reason the player lunges: an untelegraphed melee swing had no visual at all,
        // so taking damage read as happening for no reason. Telegraphed abilities already
        // have their ground decal and do not need it.
        if (ability.Telegraph is null) (_visual as VisualRoot)?.Lunge(_committedFacing, 0.3f);

        foreach (var victim in hits)
        {
            victim.TakeAttack(Self, skillCoef: ability.DamageCoef);
            ApplyStatus(ability, victim);

            if (_phaseApplies is not null) StatusApplication.Try(_phaseApplies, victim);
        }
    }

    /// <summary>Inflicts the ability's status, if it has one and the roll lands.</summary>
    private static void ApplyStatus(AbilityDef ability, Combatant victim) =>
        StatusApplication.Try(ability.Applies, victim);

    // -- Role-specific actions ---------------------------------------------

    /// <summary>
    /// Shielder aura: allies nearby take reduced damage while this one lives.
    /// <para>
    /// An aura rather than a timed buff on purpose — it makes the counterplay legible.
    /// Kill the shielder and the protection ends immediately, which is exactly the
    /// kill-priority decision the role exists to create (doc 02 §2.4).
    /// </para>
    /// </summary>
    public BtStatus MaintainShield(float radius, double delta)
    {
        var nearby = Allies(radius);

        for (var i = _shielded.Count - 1; i >= 0; i--)
        {
            var ally = _shielded[i];

            if (!IsInstanceValid(ally) || !nearby.Contains(ally))
            {
                if (IsInstanceValid(ally)) Shield(ally, false);
                _shielded.RemoveAt(i);
            }
        }

        foreach (var ally in nearby)
        {
            if (!_shielded.Contains(ally))
            {
                Shield(ally, true);
                _shielded.Add(ally);
            }
        }

        return nearby.Count > 0 ? BtStatus.Success : BtStatus.Failure;
    }

    public const double ShieldMultiplier = 0.55;

    /// <summary>Covers or uncovers an ally, and shows it: a blue ring at its feet.</summary>
    private static void Shield(Combatant ally, bool on)
    {
        ally.IncomingDamageMultiplier = on ? ShieldMultiplier : 1.0;

        if (ally.GetParent() is EnemyBrain brain) brain.ShowShieldRing(on);
    }

    /// <summary>
    /// The mark of a shielder's cover. The damage cut was invisible before, so a creature that
    /// took half as much read as a bug rather than a reason to kill the shielder first.
    /// </summary>
    private void ShowShieldRing(bool on)
    {
        if (!on)
        {
            _shieldRing?.QueueFree();
            _shieldRing = null;
            return;
        }

        if (_shieldRing is not null) return;

        _shieldRing = new MeshInstance3D
        {
            Name = "ShieldRing",
            Mesh = new TorusMesh { InnerRadius = 0.85f, OuterRadius = 1.0f, RingSegments = 40 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.45f, 0.75f, 1f, 0.7f),
                EmissionEnabled = true,
                Emission = new Color(0.3f, 0.6f, 1f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = Vector3.Up * 0.12f,
        };

        AddChild(_shieldRing);
    }

    private void ReleaseShields()
    {
        foreach (var ally in _shielded)
        {
            if (IsInstanceValid(ally)) Shield(ally, false);
        }

        _shielded.Clear();
    }

    /// <summary>Mender: heals the most wounded ally in radius.</summary>
    public BtStatus HealLowestAlly(float radius, double fraction)
    {
        Combatant? worst = null;

        foreach (var ally in Allies(radius))
        {
            if (ally.Health.IsFull) continue;
            if (worst is null || ally.Health.Fraction < worst.Health.Fraction) worst = ally;
        }

        if (worst is null) return BtStatus.Failure;

        var amount = (int)System.Math.Round(worst.Stats.MaxHp * fraction);
        worst.Heal(amount);

        // Shown in the friendly colour so the player can see the heal land and learn to
        // kill the mender first.
        AoeVisual.Circle(worst.Body.GlobalPosition, 1.4f);
        CombatFeedback.Number(worst.Body.GlobalPosition + (Vector3.Up * 1.8f), amount, false, false);

        return BtStatus.Success;
    }

    /// <summary>Bomber: detonate and die. A one-shot threat that must be pulled away from the pack.</summary>
    public BtStatus Detonate(double delta)
    {
        var ability = SpecialAbility;
        if (ability is null) return BtStatus.Failure;

        var status = UseAbility(ability, delta);

        if (status == BtStatus.Success)
        {
            Self.ApplyDamage((int)System.Math.Ceiling(Self.Health.Current));
        }

        return status;
    }

    // -- Movement helpers ---------------------------------------------------

    private void Steer(Vector3 direction, double delta)
    {
        if (direction.LengthSquared() < 0.0001f)
        {
            Brake(delta);
            return;
        }

        direction = direction.Normalized();
        var speed = (float)(MoveSpeed * Self.Statuses.MoveSpeedMultiplier);

        Velocity = Velocity with { X = direction.X * speed, Z = direction.Z * speed };
        Face(direction, delta);
    }

    private void Brake(double delta)
    {
        var horizontal = (Velocity with { Y = 0 }).MoveToward(Vector3.Zero, 40f * (float)delta);
        Velocity = Velocity with { X = horizontal.X, Z = horizontal.Z };
    }

    // -- Being thrown (CBT-17) ----------------------------------------------

    /// <summary>How fast a shove bleeds off, in metres per second per second.</summary>
    [Export] public float ShoveDrag { get; set; } = 14f;

    private Vector3 _shove;
    private double _shoveFor;

    /// <summary>True while this creature is being thrown and has no say in where it goes.</summary>
    public bool Shoved => _shoveFor > 0;

    /// <summary>
    /// Throws this creature directly away from a point.
    /// </summary>
    /// <remarks>
    /// The wind-up is cancelled with it. A creature that was thrown across the clearing and
    /// landed still swinging at where the player used to be is the version of this that
    /// makes the sweep feel like it did nothing.
    /// <para>
    /// Bosses are shards, which are not brains and so cannot be shoved at all. When a boss
    /// arrives that is a brain (WLD-10), it wants a flag here rather than an exception at
    /// the call site — whether a thing can be thrown is a property of the thing.
    /// </para>
    /// </remarks>
    public void Shove(Vector3 from, float speed, double seconds)
    {
        if (_dead) return;

        var away = (GlobalPosition - from) with { Y = 0 };

        // Thrown from exactly underfoot has no direction; send it the way it is facing, so
        // a creature standing on the player still goes somewhere rather than nowhere.
        _shove = away.LengthSquared() < 0.0001f
            ? (-GlobalBasis.Z with { Y = 0 }).Normalized() * speed
            : away.Normalized() * speed;

        _shoveFor = seconds;

        CancelAbility();
        _phase = Phase.Idle;
    }

    /// <summary>Carries a thrown creature, in place of anything it would rather be doing.</summary>
    private void CarryShove(double delta)
    {
        _shoveFor -= delta;

        Velocity = Velocity with { X = _shove.X, Z = _shove.Z };
        _shove = _shove.MoveToward(Vector3.Zero, ShoveDrag * (float)delta);

        ApplyGravity(delta);
        MoveAndSlide();
    }

    // -- Crowding (CBT-16) --------------------------------------------------

    /// <summary>Physics ticks between recounts of the neighbours. Twenty hertz is plenty.</summary>
    private const int SeparationInterval = 3;

    /// <summary>
    /// How close creatures tolerate each other before pushing apart, in metres.
    /// </summary>
    /// <remarks>
    /// Comfortably inside melee reach, so a creature pressed against its neighbours can still
    /// close on the player: separation that held a pack further apart than it could strike
    /// from would leave it milling about just out of range.
    /// </remarks>
    [Export] public float SeparationRadius { get; set; } = 1.1f;

    /// <summary>The hardest a crowded creature is pushed, in metres per second.</summary>
    [Export] public float SeparationPush { get; set; } = 2.0f;

    /// <summary>
    /// How much of that push the player is worth, against a creature's own kind.
    /// </summary>
    /// <remarks>
    /// Low on purpose. At full strength a walking player bulldozes a pack around the field —
    /// a fight where the enemies can be herded by running at them is not a fight. At a twelfth the
    /// player still works their way out of anything standing inside them, but a creature being
    /// walked at holds its ground.
    /// </remarks>
    [Export] public float PlayerPushShare { get; set; } = 0.08f;

    private Vector3 _separation;
    private int _separationTick;

    /// <summary>
    /// Eases creatures out of each other instead of colliding (CBT-16).
    /// </summary>
    /// <remarks>
    /// Bodies used to collide outright, which made a pack queue single-file down a corridor
    /// and shove each other off ledges. Removing the collision entirely is the other bad
    /// answer: six creatures converge to the same point and read as one. A push that grows
    /// as they close sits between the two — they overlap, but only so far, and a pile always
    /// unpacks itself.
    /// <para>
    /// Applied after the behaviour tree has set the velocity for this tick, because
    /// <see cref="Steer"/> assigns rather than accumulates and anything added earlier would
    /// be overwritten. It is a drift on top of whatever the creature decided to do, never a
    /// replacement for it.
    /// </para>
    /// </remarks>
    private void Separate(double delta)
    {
        if (--_separationTick <= 0)
        {
            _separationTick = SeparationInterval;
            _separation = CrowdPush();
        }

        if (_separation == Vector3.Zero) return;

        Velocity = Velocity with
        {
            X = Velocity.X + _separation.X,
            Z = Velocity.Z + _separation.Z,
        };
    }

    private Vector3 CrowdPush()
    {
        // The player counts as a neighbour, but barely: creatures do not collide with them
        // either, so without this a whole pack stands inside the player's own capsule — and
        // with it at full strength the player shoves the pack around by walking.
        var crowd = AreaQuery.Sphere(this, GlobalPosition, SeparationRadius,
            Foundation.Layers.Enemy | Foundation.Layers.Player);

        var push = Vector3.Zero;

        foreach (var other in crowd)
        {
            if (other == Self) continue;

            var away = (GlobalPosition - other.Body.GlobalPosition) with { Y = 0 };
            var distance = away.Length();

            if (distance >= SeparationRadius) continue;

            var weight = other.Body.IsInGroup("player") ? PlayerPushShare : 1f;

            if (distance < 0.05f)
            {
                // Exactly on top of each other gives no direction to push along. Derived
                // from the instance id rather than random, so two stacked creatures pick
                // their own way out instead of both choosing the same one every frame.
                var angle = (GetInstanceId() % 360) * Mathf.Pi / 180f;

                push += new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * weight;
                continue;
            }

            // Nothing at the edge of the radius, everything at the centre of it.
            push += away / distance * (1f - (distance / SeparationRadius)) * weight;
        }

        return push == Vector3.Zero ? Vector3.Zero : push.LimitLength(1f) * SeparationPush;
    }

    private void ApplyGravity(double delta)
    {
        Velocity = Velocity with { Y = IsOnFloor() ? 0f : Velocity.Y - (Gravity * (float)delta) };
    }

    private void Face(Vector3 direction, double delta)
    {
        if (_visual is null) return;

        var targetYaw = Mathf.Atan2(-direction.X, -direction.Z);
        _visual.Rotation = _visual.Rotation with
        {
            Y = Mathf.LerpAngle(_visual.Rotation.Y, targetYaw, 12f * (float)delta),
        };
    }

    private void SnapFacing(Vector3 direction)
    {
        if (_visual is null) return;

        _visual.Rotation = _visual.Rotation with { Y = Mathf.Atan2(-direction.X, -direction.Z) };
    }
}
