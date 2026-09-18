using Godot;
using Kiln.Game.Visual;

namespace Kiln.Game.Combat;

/// <summary>
/// Minimal enemy AI for the Phase 2 combat slice: idle → chase → attack, with aggro and a
/// leash (CBT-05).
/// </summary>
/// <remarks>
/// A readable state machine on purpose. The behaviour-tree framework arrives with CBT-06/07
/// when five roles need to share behaviour; building it for one melee chaser would be
/// scaffolding with nothing to hold up. The attack timing model here — wind-up, strike,
/// recovery, and no rotation once committed — is the real content, and it carries over.
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

    private NavigationAgent3D _agent = null!;
    private Combatant _combatant = null!;
    private Node3D? _visual;
    private HealthBar3D? _bar;

    private Node3D? _target;
    private Combatant? _targetCombatant;
    private State _state = State.Idle;
    private Vector3 _home;
    private double _timer;
    private double _attackCooldown;
    private Vector3 _committedFacing;

    [Export] public string EnemyId { get; set; } = "mob_corrupted_wolf";

    [Export] public float AggroRadius { get; set; } = 12f;
    [Export] public float LeashRadius { get; set; } = 35f;
    [Export] public float AttackRange { get; set; } = 2.2f;
    [Export] public float MoveSpeed { get; set; } = 4.5f;

    /// <summary>Wind-up before the hit lands. The player's window to react.</summary>
    [Export] public double WindupSeconds { get; set; } = 0.55;

    /// <summary>Recovery after striking — the player's window to punish.</summary>
    [Export] public double RecoverSeconds { get; set; } = 0.8;

    [Export] public double AttackCooldownSeconds { get; set; } = 1.6;

    [Export] public float Gravity { get; set; } = 24f;

    public override void _Ready()
    {
        _agent = GetNode<NavigationAgent3D>("NavigationAgent3D");
        _combatant = GetNode<Combatant>("Combatant");
        _visual = GetNodeOrNull<Node3D>("VisualRoot");
        _bar = GetNodeOrNull<HealthBar3D>("HealthBar3D");

        _home = GlobalPosition;
        _agent.PathDesiredDistance = 0.5f;
        _agent.TargetDesiredDistance = AttackRange * 0.8f;
        _agent.AvoidanceEnabled = false;

        if (GameContent.IsLoaded && GameContent.Database.Enemies.TryGetValue(EnemyId, out var def))
        {
            _combatant.ConfigureFromEnemy(def);

            AggroRadius = (float)def.AggroRadius;
            LeashRadius = (float)def.LeashRadius;
            MoveSpeed = (float)def.Stats.MoveSpeed;

            if (_visual is VisualRoot root && !string.IsNullOrEmpty(def.Visual))
            {
                root.Apply(def.Visual, def.VisualTint, def.VisualScale);
            }

            // Use the enemy's telegraphed ability wind-up when it has one, so the
            // BAL-03 escape guarantee actually reaches the game rather than living
            // only in the validator.
            foreach (var ability in def.Abilities)
            {
                if (ability.Telegraph is not null)
                {
                    WindupSeconds = ability.Windup * GameSession.Difficulty.TelegraphScale;
                    break;
                }
            }
        }
        else
        {
            GD.PushWarning($"EnemyBrain '{Name}': unknown enemy id '{EnemyId}'.");
        }

        _combatant.Damaged += OnDamaged;
        _combatant.Died += OnDied;
        _combatant.HealthChanged += f => _bar?.SetFraction(f);
    }

    private void OnDamaged(int amount, bool critical, bool evaded)
    {
        var at = GlobalPosition + (Vector3.Up * 1.6f);
        CombatFeedback.Number(at, amount, critical, evaded);

        if (!evaded)
        {
            CombatFeedback.HitStop(critical ? 0.075 : 0.04);
        }

        // Being hit pulls an idle enemy into the fight even outside aggro range.
        if (_state == State.Idle) AcquireTarget(force: true);
    }

    private void OnDied()
    {
        _state = State.Dead;
        Velocity = Vector3.Zero;

        if (_bar is not null) _bar.Visible = false;

        // Sink into the ground and remove. A corpse system arrives with loot in Phase 4.
        var tween = CreateTween();
        tween.TweenProperty(this, "position:y", Position.Y - 1.4f, 0.6).SetDelay(0.15);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_state == State.Dead) return;

        _attackCooldown -= delta;

        if (_combatant.Statuses.IsStunned)
        {
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

    private void AcquireTarget(bool force)
    {
        var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
        if (player is null) return;

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

        if (distance <= AttackRange && _attackCooldown <= 0)
        {
            BeginWindup();
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

    private void BeginWindup()
    {
        _state = State.Windup;
        _timer = WindupSeconds;

        if (_target is not null)
        {
            _committedFacing = (_target.GlobalPosition - GlobalPosition) with { Y = 0 };
            if (_committedFacing.LengthSquared() > 0.0001f)
            {
                _committedFacing = _committedFacing.Normalized();
                SnapFacing(_committedFacing);
            }
        }
    }

    private void Strike()
    {
        _state = State.Recover;
        _timer = RecoverSeconds;
        _attackCooldown = AttackCooldownSeconds;

        if (_target is null || _targetCombatant is null || !_targetCombatant.IsAlive) return;

        // Resolve against where the player is NOW, and only inside a forgiving cone in the
        // committed direction. Walking out of the swing must actually work.
        var toTarget = (_target.GlobalPosition - GlobalPosition) with { Y = 0 };
        if (toTarget.Length() > AttackRange + 0.8f) return;
        if (_committedFacing != Vector3.Zero && toTarget.Normalized().Dot(_committedFacing) < 0.35f) return;

        _targetCombatant.TakeAttack(_combatant);
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
