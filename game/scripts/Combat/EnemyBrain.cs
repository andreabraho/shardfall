using System.Collections.Generic;
using Godot;
using Kiln.Core.Ai;
using Kiln.Core.Foundation;
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
    private bool _basicChosen;

    [Export] public string EnemyId { get; set; } = "mob_corrupted_wolf";

    [Export] public float AggroRadius { get; set; } = 12f;
    [Export] public float LeashRadius { get; set; } = 35f;
    [Export] public float AttackRange { get; set; } = 2.2f;
    [Export] public float MoveSpeed { get; set; } = 4.5f;
    [Export] public double RecoverSeconds { get; set; } = 0.8;
    [Export] public float MeleeArc { get; set; } = 110f;
    [Export] public float Gravity { get; set; } = 24f;

    public Combatant Self { get; private set; } = null!;
    public EnemyRole Role { get; private set; } = EnemyRole.Bruiser;
    public Node3D? Target { get; private set; }
    public Combatant? TargetCombatant { get; private set; }
    public AbilityDef[] Abilities { get; private set; } = [DefaultMelee];

    /// <summary>First ability with a telegraph — the interesting one.</summary>
    public AbilityDef? SpecialAbility { get; private set; }

    /// <summary>First untelegraphed ability, used as the filler swing.</summary>
    public AbilityDef BasicAbility { get; private set; } = DefaultMelee;

    public bool IsDead => _dead;

    public override void _Ready()
    {
        _agent = GetNode<NavigationAgent3D>("NavigationAgent3D");
        Self = GetNode<Combatant>("Combatant");
        _visual = GetNodeOrNull<Node3D>("VisualRoot");
        _bar = GetNodeOrNull<HealthBar3D>("HealthBar3D");

        _telegraph = new TelegraphVisual { Name = "Telegraph" };
        CallDeferred(Node.MethodName.AddChild, _telegraph);

        _home = GlobalPosition;
        _agent.PathDesiredDistance = 0.5f;
        _agent.TargetDesiredDistance = AttackRange * 0.8f;
        _agent.AvoidanceEnabled = false;

        LoadDefinition();
        _tree = RoleTrees.Build(Role);

        Self.Damaged += OnDamaged;
        Self.Died += OnDied;
        Self.HealthChanged += f => _bar?.SetFraction(f);
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

        if (def.Abilities.Length > 0) Abilities = def.Abilities;

        foreach (var ability in Abilities)
        {
            if (ability.Telegraph is not null)
            {
                SpecialAbility ??= ability;
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
        }
    }

    // -- Signals ------------------------------------------------------------

    private void OnDamaged(int amount, bool critical, bool evaded)
    {
        CombatFeedback.Number(GlobalPosition + (Vector3.Up * 1.6f), amount, critical, evaded);

        if (!evaded) CombatFeedback.HitStop(critical ? 0.075 : 0.04);

        // Being hit pulls an enemy into the fight from outside aggro range.
        if (Target is null) AcquireTarget(force: true);
    }

    private void OnDied()
    {
        _dead = true;
        Velocity = Vector3.Zero;
        _telegraph?.Cancel();
        ReleaseShields();

        if (_bar is not null) _bar.Visible = false;

        var tween = CreateTween();
        tween.TweenProperty(this, "position:y", Position.Y - 1.4f, 0.6).SetDelay(0.15);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    // -- Main loop ----------------------------------------------------------

    public override void _PhysicsProcess(double delta)
    {
        if (_dead) return;

        TickCooldowns(delta);

        if (Self.Statuses.IsStunned)
        {
            // A stun cancels a wind-up, or the player is warned of an attack that never lands.
            CancelAbility();
            Brake(delta);
            ApplyGravity(delta);
            MoveAndSlide();
            return;
        }

        RefreshTarget();
        _tree?.Tick(this, delta);

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

    private void RefreshTarget()
    {
        if (TargetCombatant is { IsAlive: true }) return;

        Target = null;
        TargetCombatant = null;
        AcquireTarget(force: false);
    }

    private void AcquireTarget(bool force)
    {
        if (GetTree().GetFirstNodeInGroup("player") is not Node3D player) return;

        if (!force && GlobalPosition.DistanceTo(player.GlobalPosition) > AggroRadius) return;

        Target = player;
        TargetCombatant = player.GetNodeOrNull<Combatant>("Combatant");
    }

    // -- Conditions the trees read -----------------------------------------

    public bool HasLivingTarget => TargetCombatant is { IsAlive: true };

    public float DistanceToTarget =>
        Target is null ? float.MaxValue : GlobalPosition.DistanceTo(Target.GlobalPosition);

    public bool WithinLeash => GlobalPosition.DistanceTo(_home) <= LeashRadius;

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
    public BtStatus Chase(double delta)
    {
        if (Target is null) return BtStatus.Failure;

        _agent.TargetPosition = Target.GlobalPosition;

        if (_agent.IsNavigationFinished())
        {
            Brake(delta);
            return BtStatus.Running;
        }

        Steer((_agent.GetNextPathPosition() - GlobalPosition) with { Y = 0 }, delta);
        return BtStatus.Running;
    }

    /// <summary>
    /// Backs away from the target. How ranged roles keep their distance instead of walking
    /// into melee, which is what makes them a distinct threat rather than a slower bruiser.
    /// </summary>
    public BtStatus Retreat(double delta)
    {
        if (Target is null) return BtStatus.Failure;

        var away = (GlobalPosition - Target.GlobalPosition) with { Y = 0 };
        if (away.LengthSquared() < 0.0001f) return BtStatus.Failure;

        // Stop backing up at the leash, or a kiter walks itself out of the encounter.
        if (!WithinLeash)
        {
            Brake(delta);
            return BtStatus.Failure;
        }

        Steer(away, delta);
        return BtStatus.Running;
    }

    public BtStatus Idle(double delta)
    {
        Brake(delta);
        return BtStatus.Running;
    }

    /// <summary>Walks home after losing the target.</summary>
    public BtStatus ReturnHome(double delta)
    {
        if (GlobalPosition.DistanceTo(_home) < 1.0f)
        {
            Brake(delta);
            return BtStatus.Success;
        }

        _agent.TargetPosition = _home;

        if (_agent.IsNavigationFinished())
        {
            Brake(delta);
            return BtStatus.Success;
        }

        Steer((_agent.GetNextPathPosition() - GlobalPosition) with { Y = 0 }, delta);
        return BtStatus.Running;
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

    public void CancelAbility()
    {
        if (_phase == Phase.Idle) return;

        _telegraph?.Cancel();
        _phase = Phase.Idle;
        _activeAbility = null;
    }

    private void BeginWindup(AbilityDef ability)
    {
        _activeAbility = ability;
        _phase = Phase.Windup;

        // Difficulty stretches the reaction window (doc 02 §1); the content validator
        // guarantees it stays escapable at every tier.
        _phaseTimer = ability.Windup * GameSession.Difficulty.TelegraphScale;

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
        _abilityCooldowns[ability.Id] = ability.Cooldown;
        _telegraph?.Cancel();
        _activeAbility = null;

        if (TargetCombatant is null || !TargetCombatant.IsAlive) return;

        // An untelegraphed attack from beyond melee launches a shot instead of resolving
        // instantly, so the player can still break line of sight or step aside.
        if (ability.Telegraph is null && IsRanged(ability))
        {
            Projectile.Spawn(
                GetParent(),
                Self,
                GlobalPosition + (Vector3.Up * 1.2f),
                TargetCombatant.Body.GlobalPosition + (Vector3.Up * 1.0f),
                ability.DamageCoef,
                Layers.Player);

            return;
        }

        List<Combatant> hits;

        if (ability.Telegraph is { } tel)
        {
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

        foreach (var victim in hits)
        {
            victim.TakeAttack(Self, skillCoef: ability.DamageCoef);
        }
    }

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
                if (IsInstanceValid(ally)) ally.IncomingDamageMultiplier = 1.0;
                _shielded.RemoveAt(i);
            }
        }

        foreach (var ally in nearby)
        {
            ally.IncomingDamageMultiplier = ShieldMultiplier;

            if (!_shielded.Contains(ally)) _shielded.Add(ally);
        }

        return nearby.Count > 0 ? BtStatus.Success : BtStatus.Failure;
    }

    public const double ShieldMultiplier = 0.55;

    private void ReleaseShields()
    {
        foreach (var ally in _shielded)
        {
            if (IsInstanceValid(ally)) ally.IncomingDamageMultiplier = 1.0;
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
