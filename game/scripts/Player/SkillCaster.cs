using System.Collections.Generic;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Data.Definitions;
using Kiln.Game.Combat;
using Kiln.Game.Foundation;
using Kiln.Game.Input;

namespace Kiln.Game.Player;

/// <summary>
/// Hotbar skills, driven entirely by the skill definitions in <c>game/data/skills/</c>
/// (doc 06 §7): radius, targeting, damage coefficient, hits, mana and cooldown all come
/// from data, so tuning a skill never touches code.
/// </summary>
/// <remarks>
/// Unlock levels are not enforced yet — every bound skill is castable in this slice so area
/// attacks can be tested. Gating arrives with the skill trees in Phase 3 (PRG-03/04).
/// </remarks>
public partial class SkillCaster : Node
{
    /// <summary>Cone spread for cone-targeted skills.</summary>
    private const float ConeAngleDegrees = 100f;

    private readonly Dictionary<string, double> _cooldowns = new(System.StringComparer.Ordinal);

    private PlayerMotor _motor = null!;
    private Combatant _self = null!;
    private Camera3D? _camera;

    /// <summary>Hotbar slots 1-6, by skill id.</summary>
    [Export]
    public string[] Hotbar { get; set; } =
    [
        "skl_cleave",
        "skl_whirlwind",
        "skl_ground_slam",
        "skl_heavy_strike",
        "",
        "",
    ];

    private static readonly string[] SlotActions =
    [
        GameActions.Skill1, GameActions.Skill2, GameActions.Skill3,
        GameActions.Skill4, GameActions.Skill5, GameActions.Skill6,
    ];

    public override void _Ready()
    {
        _motor = GetParent<PlayerMotor>();
        _self = GetParent().GetNode<Combatant>("Combatant");

        Debug.DebugOverlay.Register("skills", () =>
        {
            var parts = new List<string>();
            for (var i = 0; i < Hotbar.Length; i++)
            {
                if (string.IsNullOrEmpty(Hotbar[i])) continue;

                var cd = CooldownRemaining(Hotbar[i]);
                parts.Add(cd > 0 ? $"{i + 1}:{cd:F1}s" : $"{i + 1}:ready");
            }

            return string.Join("  ", parts);
        });
    }

    public double CooldownRemaining(string skillId) =>
        _cooldowns.TryGetValue(skillId, out var remaining) ? System.Math.Max(0, remaining) : 0;

    public override void _Process(double delta)
    {
        foreach (var id in new List<string>(_cooldowns.Keys))
        {
            if (_cooldowns[id] > 0) _cooldowns[id] -= delta;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        for (var slot = 0; slot < SlotActions.Length && slot < Hotbar.Length; slot++)
        {
            if (!@event.IsActionPressed(SlotActions[slot])) continue;

            TryCast(Hotbar[slot]);
            GetViewport().SetInputAsHandled();
            return;
        }
    }

    private void TryCast(string skillId)
    {
        if (string.IsNullOrEmpty(skillId) || !_self.IsAlive) return;

        if (!GameContent.IsLoaded || !GameContent.Database.Skills.TryGetValue(skillId, out var skill))
        {
            GD.PushWarning($"SkillCaster: unknown skill '{skillId}'.");
            return;
        }

        if (CooldownRemaining(skillId) > 0) return;

        if (!_self.Mana.CanAfford(skill.ManaCost))
        {
            GD.Print($"[skill] not enough mana for {skillId} ({_self.Mana})");
            return;
        }

        if (_self.Statuses.IsStunned) return;

        _self.Mana.TrySpend(skill.ManaCost);
        _cooldowns[skillId] = skill.Cooldown;

        Execute(skill);
    }

    private void Execute(SkillDef skill)
    {
        var origin = _motor.GlobalPosition;
        var aim = AimDirection(origin);

        List<Combatant> targets;

        switch (skill.Targeting)
        {
            case SkillTargeting.SelfAoe:
                targets = AreaQuery.Sphere(_motor, origin, (float)skill.Radius);
                AoeVisual.Circle(origin, (float)skill.Radius);
                break;

            case SkillTargeting.Cone:
                targets = AreaQuery.Cone(_motor, origin, aim, (float)skill.Radius, ConeAngleDegrees);
                AoeVisual.Cone(origin, aim, (float)skill.Radius, ConeAngleDegrees);
                break;

            case SkillTargeting.GroundAoe:
                var point = GroundPoint(origin, aim, (float)skill.Radius);
                targets = AreaQuery.Sphere(_motor, point, (float)skill.Radius);
                AoeVisual.Circle(point, (float)skill.Radius);
                break;

            case SkillTargeting.SingleTarget:
                var single = GetParent().GetNodeOrNull<PlayerCombat>("PlayerCombat")?.Target;
                targets = single is { IsAlive: true } ? [single] : [];
                break;

            default:
                targets = [];
                break;
        }

        if (targets.Count == 0)
        {
            GD.Print($"[skill] {skill.Id} hit nothing");
            return;
        }

        var hits = System.Math.Max(1, skill.Hits);

        foreach (var target in targets)
        {
            for (var h = 0; h < hits; h++)
            {
                if (!target.IsAlive) break;
                target.TakeAttack(_self, skillCoef: skill.DamageCoef);
            }
        }

        // One hit-stop for the whole swing, not one per target — otherwise an area attack
        // into a pack freezes the game solid.
        CombatFeedback.HitStop(0.06);
    }

    /// <summary>Aim comes from the cursor, so cones and ground areas are placed deliberately.</summary>
    private Vector3 AimDirection(Vector3 origin)
    {
        _camera = GetViewport().GetCamera3D() ?? _camera;
        if (_camera is null) return Vector3.Forward;

        var mouse = GetViewport().GetMousePosition();
        var from = _camera.ProjectRayOrigin(mouse);
        var to = from + (_camera.ProjectRayNormal(mouse) * 1000f);

        var query = PhysicsRayQueryParameters3D.Create(from, to, Layers.World | Layers.Enemy);
        query.CollideWithAreas = false;

        var hit = _motor.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return Vector3.Forward;

        var direction = (hit["position"].AsVector3() - origin) with { Y = 0 };
        return direction.LengthSquared() > 0.0001f ? direction.Normalized() : Vector3.Forward;
    }

    /// <summary>Ground target under the cursor, clamped to the skill's cast range.</summary>
    private Vector3 GroundPoint(Vector3 origin, Vector3 aim, float maxRange)
    {
        _camera = GetViewport().GetCamera3D() ?? _camera;
        if (_camera is null) return origin;

        var mouse = GetViewport().GetMousePosition();
        var from = _camera.ProjectRayOrigin(mouse);
        var to = from + (_camera.ProjectRayNormal(mouse) * 1000f);

        var query = PhysicsRayQueryParameters3D.Create(from, to, Layers.World);
        query.CollideWithAreas = false;

        var hit = _motor.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return origin + (aim * maxRange);

        var point = hit["position"].AsVector3();
        var offset = (point - origin) with { Y = 0 };

        // Cast range is twice the radius: far enough to place it meaningfully, close
        // enough that it stays a melee-range tool.
        var range = maxRange * 2f;
        if (offset.Length() > range) offset = offset.Normalized() * range;

        return origin + offset;
    }
}
