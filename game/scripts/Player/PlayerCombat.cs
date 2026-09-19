using Godot;
using Kiln.Game.Combat;

namespace Kiln.Game.Player;

/// <summary>
/// Click-to-attack (CBT-03, FR-1.2): click an enemy to close distance and auto-attack until
/// it dies, leaves range, or you give another order.
/// <para>
/// Faithful to the original's scheme (D3) — the player commits to a target rather than
/// aiming each swing.
/// </para>
/// </summary>
public partial class PlayerCombat : Node
{
    private PlayerMotor _motor = null!;
    private Combatant _self = null!;
    private Combatant? _target;
    private TargetRing? _ring;
    private double _swingCooldown;

    /// <summary>Melee reach. Slightly generous so chasing a moving target is not fiddly.</summary>
    [Export] public float AttackRange { get; set; } = 2.4f;

    /// <summary>
    /// Basic attacks cleave into every enemy in an arc, not just the target.
    /// <para>
    /// This is why two-handed weapons are worth using against a pack, and it is what makes
    /// pulling groups — and therefore shard encounters — work at all. Without it the whole
    /// game becomes single-target whack-a-mole.
    /// </para>
    /// </summary>
    [Export] public bool CleaveEnabled { get; set; } = true;

    /// <summary>Total arc of the cleave, centred on the target direction.</summary>
    [Export] public float CleaveAngle { get; set; } = 120f;

    /// <summary>Reach of the cleave, slightly beyond single-target range.</summary>
    [Export] public float CleaveRange { get; set; } = 3.0f;

    /// <summary>
    /// Damage secondary targets take, relative to the primary.
    /// <para>
    /// 1.0 — full damage to everything in the arc, as in the original. This is a deliberate
    /// call: falloff made two-handed crowd clearing feel weak, and full cleave is a large
    /// part of why the source game's pulls are satisfying. It does mean area damage scales
    /// hard with pack size, which is a balance lever to watch in the shard encounters rather
    /// than a reason to tax the basic attack.
    /// </para>
    /// </summary>
    [Export] public float CleaveFalloff { get; set; } = 1.0f;

    public Combatant? Target => _target;

    [Signal] public delegate void TargetChangedEventHandler();

    public override void _Ready()
    {
        _motor = GetParent<PlayerMotor>();
        _self = GetParent().GetNode<Combatant>("Combatant");

        Debug.DebugOverlay.Register("combat", () =>
            _target is { IsAlive: true }
                ? $"target={_target.DisplayName} hp={_target.Health}"
                : "no target");
    }

    /// <summary>Called by PlayerController when the click landed on an enemy.</summary>
    public void CommandAttack(Combatant enemy)
    {
        if (!enemy.IsAlive) return;

        SetTargetBarForced(false);
        _target = enemy;
        SetTargetBarForced(true);

        _ring ??= GetTree().Root.FindChild("TargetRing", recursive: true, owned: false) as TargetRing;
        _ring?.Follow(enemy.Body);

        EmitSignal(SignalName.TargetChanged);
    }

    /// <summary>Clears the target — any plain move order abandons the attack.</summary>
    public void ClearTarget()
    {
        if (_target is null) return;

        SetTargetBarForced(false);
        _target = null;
        _ring?.Follow(null);

        EmitSignal(SignalName.TargetChanged);
    }

    /// <summary>
    /// Keeps the target's bar on screen even at full health, so selecting an untouched
    /// enemy still shows which one is selected.
    /// </summary>
    private void SetTargetBarForced(bool forced)
    {
        if (_target is null || !IsInstanceValid(_target)) return;

        var bar = _target.Body.GetNodeOrNull<HealthBar3D>("HealthBar3D");
        if (bar is null) return;

        bar.ForceVisible = forced;
        bar.Refresh();
    }

    public override void _PhysicsProcess(double delta)
    {
        _swingCooldown -= delta;

        if (!_self.IsAlive)
        {
            ClearTarget();
            return;
        }

        if (_target is null) return;

        if (!_target.IsAlive || !GodotObject.IsInstanceValid(_target))
        {
            ClearTarget();
            _motor.Stop();
            return;
        }

        var targetBody = _target.Body;
        var distance = _motor.GlobalPosition.DistanceTo(targetBody.GlobalPosition);

        if (distance > AttackRange)
        {
            // Walk into range. Re-issued each frame so a moving target is followed.
            _motor.CommandMoveTo(targetBody.GlobalPosition);
            return;
        }

        // In range: stop and swing on the attack-speed cadence.
        if (_motor.HasActiveOrder) _motor.Stop();

        if (_swingCooldown > 0) return;

        _swingCooldown = 1.0 / Math.Max(0.1, _self.Stats.AttacksPerSecond);
        Swing(targetBody.GlobalPosition);
    }

    private void Swing(Vector3 targetPosition)
    {
        if (_target is null) return;

        // The primary target always takes a full hit, even if the arc maths would miss it —
        // the player explicitly selected it and a whiff would read as a bug.
        _target.TakeAttack(_self);

        var origin = _motor.GlobalPosition;
        var forward = (targetPosition - origin) with { Y = 0 };
        if (forward.LengthSquared() < 0.0001f) return;

        forward = forward.Normalized();

        // The swing itself, shown on every attack rather than only on a multi-hit. Against a
        // single enemy the attack was otherwise invisible — the only feedback was the
        // victim's flash — so a fight read as two figures standing still trading numbers.
        _motor.GetNodeOrNull<Visual.VisualRoot>("VisualRoot")?.Lunge(forward);
        AoeVisual.Cone(origin, forward, CleaveRange, CleaveAngle);

        if (!CleaveEnabled) return;

        foreach (var other in AreaQuery.Cone(_motor, origin, forward, CleaveRange, CleaveAngle))
        {
            if (other == _target || !other.IsAlive) continue;

            other.TakeAttack(_self, weaponCoef: CleaveFalloff);
        }
    }
}
