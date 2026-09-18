using System.Collections.Generic;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Data.Definitions;
using Kiln.Game.Foundation;
using Kiln.Game.Visual;

namespace Kiln.Game.Combat;

/// <summary>
/// Enemy AI for the Phase 2 combat slice: idle → chase → wind-up → strike → recover, with
/// aggro, a leash, and per-ability cooldowns (CBT-04/05/08).
/// </summary>
/// <remarks>
/// A readable state machine on purpose. The behaviour-tree framework arrives with CBT-06/07
/// when five roles need to share behaviour; building it for one melee chaser would be
/// scaffolding with nothing to hold up. The attack timing model here — commit on wind-up,
/// resolve against the telegraphed shape — is the real content, and it carries over.
/// </remarks>
public partial class EnemyBrain : CharacterBody3D
{
    private enum State
    {
        Idle,
        Chase,
        Windup,
        Recover,
        Dead,
    }

    /// <summary>Fallback when a definition has no abilities at all.</summary>
    private static readonly AbilityDef DefaultMelee = new()
    {
        Id = "abl_default_melee",
        Cooldown = 1.8,
        Windup = 0.55,
        DamageCoef = 1.0,
    };

    private NavigationAgent3D _agent = null!;
    private Combatant _combatant = null!;
    private Node3D? _visual;
    private HealthBar3D? _bar;
    private TelegraphVisual? _telegraph;

    private readonly Dictionary<string, double> _abilityCooldowns = new(System.StringComparer.Ordinal);
    private AbilityDef[] _abilities = [DefaultMelee];

    private Node3D? _target;
    private Combatant? _targetCombatant;
    private State _state = State.Idle;
    private Vector3 _home;
    private double _timer;
    private Vector3 _committedFacing;
    private AbilityDef? _current;

    [Export] public string EnemyId { get; set; } = "mob_corrupted_wolf";

    [Export] public float AggroRadius { get; set; } = 12f;
    [Export] public float LeashRadius { get; set; } = 35f;

    /// <summary>Reach of an untelegraphed melee swing.</summary>
    [Export] public float AttackRange { get; set; } = 2.2f;

    [Export] public float MoveSpeed { get; set; } = 4.5f;

    /// <summary>Recovery after striking — the player's window to punish.</summary>
    [Export] public double RecoverSeconds { get; set; } = 0.8;

    /// <summary>Spread of an untelegraphed melee swing, in degrees.</summary>
    [Export] public float MeleeArc { get; set; } = 110f;

    [Export] public float Gravity { get; set; } = 24f;

    public override void _Ready()
    {
        _agent = GetNode<NavigationAgent3D>("NavigationAgent3D");
        _combatant = GetNode<Combatant>("Combatant");
        _visual = GetNodeOrNull<Node3D>("VisualRoot");
        _bar = GetNodeOrNull<HealthBar3D>("HealthBar3D");

        _telegraph = new TelegraphVisual { Name = "Telegraph" };
        AddChild(_telegraph);

        _home = GlobalPosition;
        _agent.PathDesiredDistance = 0.5f;
        _agent.TargetDesiredDistance = AttackRange * 0.8f;
        _agent.AvoidanceEnabled = false;

        LoadDefinition();

        _combatant.Damaged += OnDamaged;
        _combatant.Died += OnDied;
        _combatant.HealthChanged += f => _bar?.SetFraction(f);
    }

    private void LoadDefinition()
    {
        if (!GameContent.IsLoaded || !GameContent.Database.Enemies.TryGetValue(EnemyId, out var def))
        {
            GD.PushWarning($"EnemyBrain '{Name}': unknown enemy id '{EnemyId}'.");
            return;
        }

        _combatant.ConfigureFromEnemy(def);

        AggroRadius = (float)def.AggroRadius;
        LeashRadius = (float)def.LeashRadius;
        MoveSpeed = (float)def.Stats.MoveSpeed;

        if (def.Abilities.Length > 0) _abilities = def.Abilities;

        if (_visual is VisualRoot root && !string.IsNullOrEmpty(def.Visual))
        {
            root.Apply(def.Visual, def.VisualTint, def.VisualScale);
        }
    }

    private void OnDamaged(int amount, bool critical, bool evaded)
    {
        var at = GlobalPosition + (Vector3.Up * 1.6f);
        CombatFeedback.Number(at, amount, critical, evaded);

        if (!evaded) CombatFeedback.HitStop(critical ? 0.075 : 0.04);

        if (_state == State.Idle) AcquireTarget(force: true);
    }

    private void OnDied()
    {
        _state = State.Dead;
        Velocity = Vector3.Zero;
        _telegraph?.Cancel();

        if (_bar is not null) _bar.Visible = false;

        var tween = CreateTween();
        tween.TweenProperty(this, "position:y", Position.Y - 1.4f, 0.6).SetDelay(0.15);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_state == State.Dead) return;

        TickCooldowns(delta);

        if (_combatant.Statuses.IsStunned)
        {
            // A stun interrupts a wind-up: the telegraph must go, or the player is warned
            // of an attack that will never arrive.
            if (_state == State.Windup)
            {
                _telegraph?.Cancel();
                _state = State.Chase;
            }

            Velocity = Velocity with { X = 0, Z = 0 };
            MoveAndSlide();
            return;
        }

        switch (_state)
        {
            case State.Idle:
                AcquireTarget(force: false);
                Brake(delta);
                break;

            case State.Chase:
                Chase(delta);
                break;

            case State.Windup:
                // Committed: no rotation, no movement (FR-3.3). This is what makes the
                // wind-up a real opening rather than a tracking laser.
                Brake(delta);
                _timer -= delta;
                if (_timer <= 0) Strike();
                break;

            case State.Recover:
                Brake(delta);
                _timer -= delta;
                if (_timer <= 0) _state = State.Chase;
                break;
        }

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

    private bool Ready(AbilityDef ability) =>
        !_abilityCooldowns.TryGetValue(ability.Id, out var cd) || cd <= 0;

    /// <summary>Effective reach: a telegraphed ability reaches as far as its shape.</summary>
    private float RangeOf(AbilityDef ability) =>
        ability.Telegraph is { } tel ? (float)tel.Radius : AttackRange;

    /// <summary>
    /// Picks what to cast. Telegraphed abilities are preferred when available, because they
    /// are the interesting ones — the basic swing is the filler between them.
    /// </summary>
    private AbilityDef? ChooseAbility(float distance)
    {
        AbilityDef? fallback = null;

        foreach (var ability in _abilities)
        {
            if (!Ready(ability) || distance > RangeOf(ability)) continue;

            if (ability.Telegraph is not null) return ability;
            fallback ??= ability;
        }

        return fallback;
    }

    private void AcquireTarget(bool force)
    {
        if (GetTree().GetFirstNodeInGroup("player") is not Node3D player) return;

        var distance = GlobalPosition.DistanceTo(player.GlobalPosition);
        if (!force && distance > AggroRadius) return;

        _target = player;
        _targetCombatant = player.GetNodeOrNull<Combatant>("Combatant");
        _state = State.Chase;
    }

    private void Chase(double delta)
    {
        if (_target is null || _targetCombatant is null || !_targetCombatant.IsAlive)
        {
            _state = State.Idle;
            _target = null;
            return;
        }

        // Leash on distance from home, not from the player, so an enemy cannot be dragged
        // across the map indefinitely.
        if (GlobalPosition.DistanceTo(_home) > LeashRadius)
        {
            _target = null;
            _state = State.Idle;
            _agent.TargetPosition = _home;
            return;
        }

        var distance = GlobalPosition.DistanceTo(_target.GlobalPosition);
        var ability = ChooseAbility(distance);

        if (ability is not null)
        {
            BeginWindup(ability);
            return;
        }

        _agent.TargetPosition = _target.GlobalPosition;

        if (_agent.IsNavigationFinished())
        {
            Brake(delta);
            return;
        }

        var next = _agent.GetNextPathPosition();
        var direction = (next - GlobalPosition) with { Y = 0 };

        if (direction.LengthSquared() > 0.0001f)
        {
            direction = direction.Normalized();
            var speed = (float)(MoveSpeed * _combatant.Statuses.MoveSpeedMultiplier);
            Velocity = Velocity with { X = direction.X * speed, Z = direction.Z * speed };
            Face(direction, delta);
        }
    }

    private void BeginWindup(AbilityDef ability)
    {
        _current = ability;
        _state = State.Windup;

        // Difficulty stretches or compresses the reaction window (doc 02 §1). The content
        // validator guarantees this stays long enough to walk out of at every tier.
        _timer = ability.Windup * GameSession.Difficulty.TelegraphScale;

        if (_target is not null)
        {
            _committedFacing = (_target.GlobalPosition - GlobalPosition) with { Y = 0 };

            if (_committedFacing.LengthSquared() > 0.0001f)
            {
                _committedFacing = _committedFacing.Normalized();
                SnapFacing(_committedFacing);
            }
        }

        if (ability.Telegraph is { } tel && _telegraph is not null)
        {
            _telegraph.Begin(
                tel.Shape,
                GlobalPosition,
                _committedFacing,
                (float)tel.Radius,
                (float)(tel.Angle > 0 ? tel.Angle : 90),
                _timer);
        }
    }

    private void Strike()
    {
        var ability = _current ?? DefaultMelee;

        _state = State.Recover;
        _timer = RecoverSeconds;
        _abilityCooldowns[ability.Id] = ability.Cooldown;
        _telegraph?.Cancel();
        _current = null;

        if (_targetCombatant is null || !_targetCombatant.IsAlive) return;

        // Resolve against the telegraphed shape, evaluated now. Walking out has to work —
        // that is the entire skill expression the design rests on (doc 02 §2.1).
        List<Combatant> hits;

        if (ability.Telegraph is { } tel)
        {
            hits = tel.Shape == TelegraphShape.Cone
                ? AreaQuery.Cone(this, GlobalPosition, _committedFacing, (float)tel.Radius,
                    (float)(tel.Angle > 0 ? tel.Angle : 90), Layers.Player)
                : AreaQuery.Sphere(this, GlobalPosition, (float)tel.Radius, Layers.Player);

            AoeVisual.Circle(GlobalPosition, (float)tel.Radius, hostile: true);
        }
        else
        {
            hits = AreaQuery.Cone(this, GlobalPosition, _committedFacing, AttackRange + 0.6f, MeleeArc, Layers.Player);
        }

        foreach (var victim in hits)
        {
            victim.TakeAttack(_combatant, skillCoef: ability.DamageCoef);
        }
    }

    private void Brake(double delta)
    {
        var horizontal = Velocity with { Y = 0 };
        horizontal = horizontal.MoveToward(Vector3.Zero, 40f * (float)delta);
        Velocity = Velocity with { X = horizontal.X, Z = horizontal.Z };
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
