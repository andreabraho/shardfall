using System.Collections.Generic;
using Godot;
using Kiln.Core.Combat;
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
/// Skills must be learned before they can be cast, with a skill point from the skill screen. Mastery
/// accrues by casting and resolves through <see cref="ResolvedSkill"/>, so nothing ever uses
/// a skill.s base numbers once it has ranked up.
/// </remarks>
public partial class SkillCaster : Node
{
    /// <summary>The skill as the player has it: mastery rank and points invested (REF-03).</summary>
    public ResolvedSkill Resolve(SkillDef def) =>
        ResolvedSkill.For(def, _book?.RankOf(def.Id) ?? MasteryRank.Normal, _book?.PointsIn(def.Id) ?? 1);

    private readonly Dictionary<string, double> _cooldowns = new(System.StringComparer.Ordinal);
    private readonly Dictionary<string, double> _cooldownTotals = new(System.StringComparer.Ordinal);

    private PlayerMotor _motor = null!;
    private Combatant _self = null!;
    private Camera3D? _camera;
    /// <summary>
    /// The book and the points, read from the profile every time rather than kept.
    /// </summary>
    /// <remarks>
    /// A reference taken at _Ready belongs to whichever profile existed then. Loading a save,
    /// or starting a new game, replaces the profile's objects — and a caster still holding the
    /// old ones reads a book nobody writes to any more: skills that will not cast and points
    /// that will not spend, while the screen, which reads the profile, says they are there.
    /// </remarks>
    private SkillBook? _book => PlayerProfile.Skills;

    private CharacterProgression? _progression => PlayerProfile.Progression;

    /// <summary>
    /// Which skill sits on each numbered key. The player arranges it from the skill screen;
    /// it lives on the profile so it survives the scene change at every zone border.
    /// </summary>
    public string[] Hotbar => PlayerProfile.Hotbar;

    /// <summary>How many numbered keys there are.</summary>
    public static int Slots => SlotActions.Length;

    [Signal] public delegate void HotbarChangedEventHandler();

    /// <summary>
    /// Puts a skill on a key (REF-03).
    /// </summary>
    /// <remarks>
    /// A skill can only be in one place, so assigning one that is already on another key
    /// swaps the two rather than leaving a duplicate: two keys casting the same thing is
    /// never what was meant, and is one of those states a player has to undo by hand.
    /// </remarks>
    public void Assign(int slot, string skillId)
    {
        if (slot < 0 || slot >= Hotbar.Length) return;

        var bar = Hotbar;
        var existing = System.Array.IndexOf(bar, skillId);

        if (skillId.Length > 0 && existing >= 0 && existing != slot)
        {
            bar[existing] = bar[slot];
        }

        bar[slot] = skillId;

        EmitSignal(SignalName.HotbarChanged);
    }

    /// <summary>The key a skill sits on, or -1 when it is not on the bar.</summary>
    public int SlotOf(string skillId) => System.Array.IndexOf(Hotbar, skillId);

    private static readonly string[] SlotActions =
    [
        GameActions.Skill1, GameActions.Skill2, GameActions.Skill3,
        GameActions.Skill4, GameActions.Skill5, GameActions.Skill6, GameActions.Skill7,
    ];

    public override void _Ready()
    {
        _motor = GetParent<PlayerMotor>();
        _self = GetParent().GetNode<Combatant>("Combatant");

        // One aura per buff, so both can be worn at once. They belong to the skills rather
        // than to the scene, so they are built here instead of being placed in player.tscn.
        foreach (var kind in new[] { StatusKind.Empower, StatusKind.Fortify })
        {
            _motor.CallDeferred(Node.MethodName.AddChild,
                new Combat.AuraVisual { Name = $"Aura{kind}", Kind = kind });
        }
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
        && _self.Mana.CanAfford(Resolve(def).ManaCost);

    /// <summary>Skill currently being aimed, if any. Ground areas aim before they commit.</summary>
    private string? _aiming;

    /// <summary>
    /// Seconds until the skill being cast has finished (REF-22). Another skill waits for it:
    /// pressing several keys at once used to fire every one of them together, each swinging
    /// over the last.
    /// </summary>
    private double _castingFor;

    /// <summary>The skill pressed while another was playing, fired as soon as it ends.</summary>
    private string? _queued;

    private double _queuedFor;

    /// <summary>How long a skill pressed too early waits to be cast before it is forgotten.</summary>
    private const double QueueSeconds = 0.6;

    /// <summary>True while a skill is being cast.</summary>
    public bool IsCasting => _castingFor > 0;

    public override void _Process(double delta)
    {
        foreach (var id in new List<string>(_cooldowns.Keys))
        {
            if (_cooldowns[id] > 0) _cooldowns[id] -= delta;
        }

        if (_castingFor > 0) _castingFor -= delta;

        // The next skill, pressed during the last one, goes the moment it can.
        if (_queued is not null)
        {
            _queuedFor -= delta;

            if (_queuedFor <= 0) _queued = null;
            else if (_castingFor <= 0)
            {
                var next = _queued;
                _queued = null;
                TryCast(next);
            }
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
            GD.Print($"[skill] {skillId} not learned — spend a point on it with K");
            return;
        }

        var skill = Resolve(def);

        if (CooldownRemaining(skillId) > 0) return;

        // One skill at a time: pressed during another, it waits its turn (only the last
        // pressed is kept, and only briefly).
        if (_castingFor > 0)
        {
            _queued = skillId;
            _queuedFor = QueueSeconds;
            return;
        }

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

    /// <summary>Why <see cref="CanInvest"/> said no, in words, for the console.</summary>
    public string WhyNot(string skillId) =>
        _book is null || _progression is null ? "no character"
        : _progression.UnspentSkillPoints <= 0 ? "no skill points left"
        : !GameContent.IsLoaded || !GameContent.Database.Skills.ContainsKey(skillId) ? "unknown skill"
        : _book.IsFullyInvested(skillId) ? $"already at {SkillBook.MaxPoints} points"
        : "allowed";

    /// <summary>
    /// Whether a point can go into this skill now (REF-03): the player has one and the skill
    /// is not already full. Level does not come into it — every skill is open from the start,
    /// and what a character can do is decided by where the points went, not by waiting.
    /// </summary>
    public bool CanInvest(string skillId) =>
        _book is not null
        && _progression is not null
        && _progression.UnspentSkillPoints > 0
        && GameContent.IsLoaded
        && GameContent.Database.Skills.ContainsKey(skillId)
        && !_book.IsFullyInvested(skillId);

    /// <summary>
    /// Spends one skill point on a skill. The first point learns it, the seventh masters it.
    /// </summary>
    /// <remarks>
    /// Nothing is learned automatically any more. Which five of the eight a character ends up
    /// good at is the build, and a game that hands out every skill at its level has no build
    /// to speak of.
    /// </remarks>
    public bool Invest(string skillId)
    {
        if (!CanInvest(skillId) || _book is null || _progression is null)
        {
            GD.Print($"[skill] no point spent on {skillId}: {WhyNot(skillId)}");
            return false;
        }
        if (!_book.Invest(skillId)) return false;

        _progression.SpendSkillPoint();

        GD.Print($"[skill] {skillId} at {_book.PointsIn(skillId)}/{SkillBook.MaxPoints} points, {_book.RankOf(skillId)}");

        return true;
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

        /// <summary>Which hit this is, from zero.</summary>
        public int Index => Math.Max(1, Skill.Hits) - Remaining - 1;
    }

    private readonly List<Pulse> _pulses = [];

    private void Execute(ResolvedSkill skill)
    {
        var origin = _motor.GlobalPosition;

        _castingFor = skill.Targeting == SkillTargeting.Self ? SelfCastHold : CastHold(skill);

        // A skill that only touches its caster has nothing to aim at: it is cast where the
        // Warrior already faces, with a raised-weapon flourish instead of a swing.
        if (skill.Targeting == SkillTargeting.Self)
        {
            Empower(skill);
            return;
        }

        // Where a skill goes: at the target when there is one, otherwise straight ahead of the
        // Warrior. Never at the cursor — the cursor is wherever the last click to walk left it,
        // which is as often behind the character as in front, and a Whirlwind that opened
        // behind the Warrior's back read as the skill misfiring. Only a ground skill, which is
        // placed rather than swung, still goes where the cursor points.
        var aim = skill.Targeting == SkillTargeting.GroundAoe ? AimDirection(origin) : _motor.Facing;

        if (GetParent().GetNodeOrNull<PlayerCombat>("PlayerCombat")?.Target is { IsAlive: true } target
            && skill.Targeting is SkillTargeting.SingleTarget or SkillTargeting.Cone)
        {
            var toTarget = (target.Body.GlobalPosition - origin) with { Y = 0 };

            if (toTarget.LengthSquared() > 0.0001f) aim = toTarget.Normalized();
        }

        // The Warrior stands still while a skill plays out (REF-01): held for every pulse it
        // fires, turned to where it is aimed, and an area centred on where it was used.
        _motor.Stop();
        _motor.HoldFor(CastHold(skill));
        _motor.FaceTowards(aim);

        // Its own move (REF-22); a spinning skill turns on each of its hits instead.
        if (skill.Motion != "spin") _motor.GetNodeOrNull<Visual.VisualRoot>("VisualRoot")?.SkillMove(skill.Motion, aim, CastHold(skill));

        var center = skill.Targeting == SkillTargeting.GroundAoe
            ? GroundPoint(origin, aim, (float)skill.Radius)
            : origin;

        var pulse = new Pulse
        {
            Skill = skill,
            Center = center,
            Aim = aim,
            FollowsCaster = false,
            Remaining = System.Math.Max(1, skill.Hits),
            Timer = 0,
        };

        FirePulse(pulse);

        if (pulse.Remaining > 0)
        {
            pulse.Timer = skill.HitInterval;
            _pulses.Add(pulse);
        }
    }

    /// <summary>
    /// A self-buff: Iron Skin, the Blade Aura (REF-03).
    /// </summary>
    /// <remarks>
    /// The status is built from the resolved skill rather than from the <c>applies</c> block,
    /// so mastery and the points spent reach the buff — an aura that stayed at its authored
    /// 25% however far it was ranked up would make investing in it pointless.
    /// </remarks>
    private void Empower(ResolvedSkill skill)
    {
        var kind = skill.Applies?.Kind ?? "";

        _motor.Stop();
        _motor.HoldFor(SelfCastHold);
        _motor.GetNodeOrNull<Visual.VisualRoot>("VisualRoot")?.SkillMove(skill.Motion, _motor.Facing, SelfCastHold);

        var effect = kind switch
        {
            "fortify" => StatusEffectSet.Fortify(skill.Magnitude, skill.Duration),
            "empower" => StatusEffectSet.Empower(skill.Magnitude, skill.Duration),
            _ => null,
        };

        if (effect is null)
        {
            GD.PushWarning($"SkillCaster: self skill '{skill.Id}' applies nothing usable ('{kind}').");
            return;
        }

        // The aura wears the rank of the cast that raised it, so a mastered buff is a
        // different colour on the ground and on the blade (REF-03).
        if (_motor.GetNodeOrNull<Combat.AuraVisual>($"Aura{effect.Kind}") is { } aura)
        {
            aura.Rank = skill.Rank;
        }

        _self.Statuses.Apply(effect);
    }

    /// <summary>How long a self-buff plants the character. Long enough to read as a cast.</summary>
    private const double SelfCastHold = 0.6;

    /// <summary>How long a skill holds the character: every pulse, and a beat after the last.</summary>
    private static double CastHold(ResolvedSkill skill) =>
        (System.Math.Max(1, skill.Hits) - 1) * skill.HitInterval + 0.35;

    private void FirePulse(Pulse pulse)
    {
        pulse.Remaining--;

        var skill = pulse.Skill;
        var center = pulse.FollowsCaster ? _motor.GlobalPosition : pulse.Center;
        var aim = pulse.Aim;

        // A spinning skill turns the body on every hit. Only the body: the arc stays where the
        // skill was aimed, in front of the Warrior.
        if (skill.Motion == "spin")
        {
            _motor.GetNodeOrNull<Visual.VisualRoot>("VisualRoot")?.Spin(skill.HitInterval, pulse.Index);
        }

        List<Combatant> targets;

        switch (skill.Targeting)
        {
            case SkillTargeting.SelfAoe:
                targets = AreaQuery.Sphere(_motor, center, (float)skill.Radius);
                AoeVisual.Circle(center, (float)skill.Radius, hostile: false, skill.Rank);
                break;

            case SkillTargeting.Cone:
                targets = AreaQuery.Cone(_motor, center, aim, (float)skill.Radius, (float)skill.ConeAngle);
                AoeVisual.Cone(center, aim, (float)skill.Radius, (float)skill.ConeAngle, hostile: false, skill.Rank,
                    swirl: skill.Motion == "spin");
                break;

            case SkillTargeting.GroundAoe:
                targets = AreaQuery.Sphere(_motor, center, (float)skill.Radius);
                AoeVisual.Circle(center, (float)skill.Radius, hostile: false, skill.Rank);
                break;

            case SkillTargeting.SingleTarget:
                var single = GetParent().GetNodeOrNull<PlayerCombat>("PlayerCombat")?.Target;
                targets = single is { IsAlive: true } ? [single] : [];

                // A ring under the one target it lands on. Small, but it is what carries the
                // rank colour for the skills that cover no ground (REF-03).
                if (targets.Count > 0)
                {
                    AoeVisual.Circle(targets[0].Body.GlobalPosition, 0.9f, hostile: false, skill.Rank);
                }

                break;

            default:
                targets = [];
                break;
        }

        if (targets.Count == 0) return;

        foreach (var target in targets)
        {
            if (!target.IsAlive) continue;

            target.TakeAttack(_self, skillCoef: skill.DamageCoef, skill: true);

            // What is not a creature — a shard — cannot be stunned, but can be weakened.
            var resist = (target.Body as EnemyBrain)?.StunResist ?? 1.0;

            StatusApplication.Try(skill.Applies, target, resist);
        }
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
                pulse.Timer += pulse.Skill.HitInterval;
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
