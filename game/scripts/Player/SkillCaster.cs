using System.Collections.Generic;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.Progression;
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
/// Skills must be learned before they can be cast, and learning is gated on level. Mastery
/// accrues by casting and resolves through <see cref="ResolvedSkill"/>, so nothing ever uses
/// a skill.s base numbers once it has ranked up.
/// </remarks>
public partial class SkillCaster : Node
{
    /// <summary>Cone spread for cone-targeted skills.</summary>
    private const float ConeAngleDegrees = 100f;

    private readonly Dictionary<string, double> _cooldowns = new(System.StringComparer.Ordinal);
    private readonly Dictionary<string, double> _cooldownTotals = new(System.StringComparer.Ordinal);

    private PlayerMotor _motor = null!;
    private Combatant _self = null!;
    private Camera3D? _camera;
    private SkillBook? _book;
    private CharacterProgression? _progression;

    /// <summary>Hotbar slots 1-6, by skill id.</summary>
    [Export]
    /// <remarks>
    /// Ordered so the early slots are usable at the starting level and the later ones light
    /// up as the player levels — the hotbar itself shows progression.
    /// </remarks>
    public string[] Hotbar { get; set; } =
    [
        "skl_heavy_strike",
        "skl_cleave",
        "skl_shield_bash",
        "skl_whirlwind",
        "skl_ground_slam",
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

        var character = GetParent().GetNodeOrNull<PlayerCharacter>("PlayerCharacter");
        _book = character?.Skills;
        _progression = character?.Progression;
    }

    public double CooldownRemaining(string skillId) =>
        _cooldowns.TryGetValue(skillId, out var remaining) ? System.Math.Max(0, remaining) : 0;

    /// <summary>
    /// The length of the cooldown now running, as it was when cast.
    /// </summary>
    /// <remarks>
    /// Recorded at the cast rather than read back from the skill, because mastery shortens
    /// cooldowns and a rank reached mid-cooldown would otherwise make the sweep jump.
    /// </remarks>
    public double CooldownTotal(string skillId) =>
        _cooldownTotals.TryGetValue(skillId, out var total) ? total : 1;

    public bool IsLearned(string skillId) => _book?.IsUnlocked(skillId) == true;

    public MasteryRank RankOf(string skillId) => _book?.RankOf(skillId) ?? MasteryRank.Normal;

    /// <summary>Whether the player has the mana for it right now, at its current rank.</summary>
    public bool Affordable(string skillId) =>
        _book is not null
        && GameContent.IsLoaded
        && GameContent.Database.Skills.TryGetValue(skillId, out var def)
        && _self.Mana.CanAfford(ResolvedSkill.For(def, _book.RankOf(skillId)).ManaCost);

    /// <summary>Skill currently being aimed, if any. Ground areas aim before they commit.</summary>
    private string? _aiming;

    public override void _Process(double delta)
    {
        foreach (var id in new List<string>(_cooldowns.Keys))
        {
            if (_cooldowns[id] > 0) _cooldowns[id] -= delta;
        }

        ProcessPulses(delta);
        UpdateAiming();
    }

    private void UpdateAiming()
    {
        if (_aiming is null)
        {
            AoeVisual.HidePreview();
            return;
        }

        if (!GameContent.IsLoaded || !GameContent.Database.Skills.TryGetValue(_aiming, out var skill))
        {
            _aiming = null;
            return;
        }

        var origin = _motor.GlobalPosition;
        var point = GroundPoint(origin, AimDirection(origin), (float)skill.Radius);

        AoeVisual.ShowPreview(point, (float)skill.Radius);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // A panel has the player's attention; firing a skill behind it is never intended.
        if (UI.UiState.ModalOpen) return;

        for (var slot = 0; slot < SlotActions.Length && slot < Hotbar.Length; slot++)
        {
            var id = Hotbar[slot];
            if (string.IsNullOrEmpty(id)) continue;

            if (@event.IsActionPressed(SlotActions[slot]))
            {
                // Ground-targeted skills aim while held and fire on release, so the area is
                // visible before committing. Everything else fires immediately.
                if (IsGroundTargeted(id) && CanCast(id))
                {
                    _aiming = id;
                }
                else
                {
                    TryCast(id);
                }

                GetViewport().SetInputAsHandled();
                return;
            }

            if (@event.IsActionReleased(SlotActions[slot]) && _aiming == id)
            {
                _aiming = null;
                AoeVisual.HidePreview();
                TryCast(id);
                GetViewport().SetInputAsHandled();
                return;
            }
        }
    }

    private bool IsGroundTargeted(string skillId) =>
        GameContent.IsLoaded
        && GameContent.Database.Skills.TryGetValue(skillId, out var skill)
        && skill.Targeting == SkillTargeting.GroundAoe;

    private bool CanCast(string skillId) =>
        _self.IsAlive
        && !_self.Statuses.IsStunned
        && CooldownRemaining(skillId) <= 0
        && GameContent.IsLoaded
        && GameContent.Database.Skills.TryGetValue(skillId, out var skill)
        && _self.Mana.CanAfford(skill.ManaCost);

    private void TryCast(string skillId)
    {
        if (string.IsNullOrEmpty(skillId) || !_self.IsAlive) return;

        if (!GameContent.IsLoaded || !GameContent.Database.Skills.TryGetValue(skillId, out var def))
        {
            GD.PushWarning($"SkillCaster: unknown skill '{skillId}'.");
            return;
        }

        if (_book is null || !_book.IsUnlocked(skillId))
        {
            GD.Print($"[skill] {skillId} not learned (unlocks at level {def.UnlockLevel})");
            return;
        }

        var skill = ResolvedSkill.For(def, _book.RankOf(skillId));

        if (CooldownRemaining(skillId) > 0) return;

        if (!_self.Mana.CanAfford(skill.ManaCost))
        {
            GD.Print($"[skill] not enough mana for {skillId} ({_self.Mana})");
            return;
        }

        if (_self.Statuses.IsStunned) return;

        _self.Mana.TrySpend(skill.ManaCost);
        Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndSkill, _motor.GlobalPosition);
        _cooldowns[skillId] = skill.Cooldown;
        _cooldownTotals[skillId] = System.Math.Max(0.01, skill.Cooldown);

        // Mastery is earned by casting, so it is recorded on the cast rather than on a hit —
        // otherwise a skill used to reposition or to break a shard would never improve.
        if (_book.RecordUse(skillId) is { } promoted)
        {
            GD.Print($"[skill] {skillId} reached {promoted}");
            Combat.CombatFeedback.Number(
                _motor.GlobalPosition + (Vector3.Up * 2.4f), (int)promoted, critical: true, evaded: false);
        }

        Execute(skill);
    }

    /// <summary>
    /// Learns every skill the current level allows, spending skill points.
    /// <para>
    /// Automatic for now. The real choice — which tree to invest in — needs the skill
    /// screen that arrives in Phase 8; until then, gating on level is what matters, so the
    /// player is not casting Ground Slam at level 10.
    /// </para>
    /// </summary>
    public void LearnAvailable(int level)
    {
        if (_book is null || _progression is null || !GameContent.IsLoaded) return;

        foreach (var def in GameContent.Database.Skills.Values)
        {
            if (def.Class != CharacterClass.Warrior) continue;
            if (def.UnlockLevel > level || _book.IsUnlocked(def.Id)) continue;
            if (!_progression.SpendSkillPoint()) return;

            _book.Unlock(def.Id);
            GD.Print($"[skill] learned {def.Id}");
        }
    }

    /// <summary>A multi-hit skill still owing pulses.</summary>
    private sealed class Pulse
    {
        public required ResolvedSkill Skill { get; init; }
        public required Vector3 Center { get; init; }
        public required Vector3 Aim { get; init; }
        public required bool FollowsCaster { get; init; }
        public int Remaining { get; set; }
        public double Timer { get; set; }
    }

    /// <summary>
    /// Gap between hits of a multi-hit skill.
    /// <para>
    /// Applying every hit on one frame made Whirlwind's three hits land on top of each
    /// other — identical numbers at the same spot in the same instant, so it read as a
    /// single hit. Spreading them out is what makes a multi-hit skill legible, and it is
    /// also what a spin should look like.
    /// </para>
    /// </summary>
    private const double PulseInterval = 0.17;

    private readonly List<Pulse> _pulses = [];

    private void Execute(ResolvedSkill skill)
    {
        var origin = _motor.GlobalPosition;
        var aim = AimDirection(origin);

        // Ground areas are placed once, at cast. Self-centred areas follow the caster, so a
        // spin keeps hitting what is around you as you drift.
        var center = skill.Targeting == SkillTargeting.GroundAoe
            ? GroundPoint(origin, aim, (float)skill.Radius)
            : origin;

        var pulse = new Pulse
        {
            Skill = skill,
            Center = center,
            Aim = aim,
            FollowsCaster = skill.Targeting is SkillTargeting.SelfAoe or SkillTargeting.Cone,
            Remaining = System.Math.Max(1, skill.Hits),
            Timer = 0,
        };

        FirePulse(pulse);

        if (pulse.Remaining > 0)
        {
            pulse.Timer = PulseInterval;
            _pulses.Add(pulse);
        }
    }

    private void FirePulse(Pulse pulse)
    {
        pulse.Remaining--;

        var skill = pulse.Skill;
        var center = pulse.FollowsCaster ? _motor.GlobalPosition : pulse.Center;

        List<Combatant> targets;

        switch (skill.Targeting)
        {
            case SkillTargeting.SelfAoe:
                targets = AreaQuery.Sphere(_motor, center, (float)skill.Radius);
                AoeVisual.Circle(center, (float)skill.Radius);
                break;

            case SkillTargeting.Cone:
                targets = AreaQuery.Cone(_motor, center, pulse.Aim, (float)skill.Radius, ConeAngleDegrees);
                AoeVisual.Cone(center, pulse.Aim, (float)skill.Radius, ConeAngleDegrees);
                break;

            case SkillTargeting.GroundAoe:
                targets = AreaQuery.Sphere(_motor, center, (float)skill.Radius);
                AoeVisual.Circle(center, (float)skill.Radius);
                break;

            case SkillTargeting.SingleTarget:
                var single = GetParent().GetNodeOrNull<PlayerCombat>("PlayerCombat")?.Target;
                targets = single is { IsAlive: true } ? [single] : [];
                break;

            default:
                targets = [];
                break;
        }

        if (targets.Count == 0) return;

        foreach (var target in targets)
        {
            if (!target.IsAlive) continue;
            target.TakeAttack(_self, skillCoef: skill.DamageCoef);
        }

        // One hit-stop per pulse, not per target — otherwise an area attack into a pack
        // freezes the game solid.
        CombatFeedback.HitStop(0.05);
    }

    private void ProcessPulses(double delta)
    {
        for (var i = _pulses.Count - 1; i >= 0; i--)
        {
            var pulse = _pulses[i];
            pulse.Timer -= delta;

            if (pulse.Timer > 0) continue;

            FirePulse(pulse);

            if (pulse.Remaining <= 0)
            {
                _pulses.RemoveAt(i);
            }
            else
            {
                pulse.Timer += PulseInterval;
            }
        }
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
