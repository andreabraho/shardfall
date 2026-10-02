using System.Collections.Generic;
using System.Linq;
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
        GameActions.Skill4, GameActions.Skill5, GameActions.Skill6,
    ];

    public override void _Ready()
    {
        _motor = GetParent<PlayerMotor>();
        _self = GetParent().GetNode<Combatant>("Combatant");

        // One aura per buff, so both can be worn at once. They belong to the skills rather
        // than to the scene, so they are built here instead of being placed in player.tscn.
        foreach (var kind in new[] { StatusKind.Empower, StatusKind.Fortify, StatusKind.Frenzy })
        {
            _motor.CallDeferred(Node.MethodName.AddChild,
                new Combat.AuraVisual { Name = $"Aura{kind}", Kind = kind });
        }

        // Every effect drawn once where the player stands, and every skill's sound read, so
        // the first real cast does not stall while the game meets them (REF-21). A moment after
        // arriving, once the model holds its blade.
        GetTree().CreateTimer(0.5).Timeout += Prewarm;
    }

    private void Prewarm()
    {
        if (!IsInstanceValid(this) || !IsInsideTree() || !GameContent.IsLoaded) return;

        SkillFx.Prewarm(this, _motor.GlobalPosition + (Vector3.Down * 0.5f));

        // The blade's ribbon too, drawn for a moment too faint to see.
        _motor.GetNodeOrNull<Visual.VisualRoot>("VisualRoot")?.Trail(0, 0.1, new Color(1, 1, 1, 0.01f));

        Audio.AudioDirector.Preload(GameContent.Database.Skills.Values.Select(s => s.Sound).Distinct());
    }

    /// <summary>
    /// The colour a skill's effects are drawn in at the rank it was cast at: its own colour,
    /// warmed by the rank and a little toward the rank's second colour (REF-21).
    /// </summary>
    private static Color FxColour(ResolvedSkill skill)
    {
        var own = GameContent.Database.Skills.TryGetValue(skill.Id, out var def) ? new Color(def.Color) : Colors.White;

        return Visual.MasteryStyle.Tint(own, skill.Rank).Lerp(Visual.MasteryStyle.Accent(own, skill.Rank), 0.3f);
    }

    private static float FxBright(ResolvedSkill skill) => Visual.MasteryStyle.Brightness(skill.Rank);

    private static string SoundOf(string skillId) =>
        GameContent.Database.Skills.TryGetValue(skillId, out var def) ? def.Sound : Kiln.Data.Ids.Sounds.SndSkill;

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
        ShowUnseen();
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
                if (IsGroundTargeted(id) && CanCast(id) && GetParent().GetNodeOrNull<PlayerCombat>("PlayerCombat")?.Target is not { IsAlive: true })
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

        // A skill that wants a target is not cast without one, or — a blow of the hand — at one
        // out of its reach: said, and nothing spent.
        if (def.NeedsTarget)
        {
            var chosen = GetParent().GetNodeOrNull<PlayerCombat>("PlayerCombat")?.Target;

            if (chosen is not { IsAlive: true })
            {
                UI.WorldNotice.Show(GetTree(), L10n.T("Choose a target first."));
                return;
            }

            if (def.Targeting == SkillTargeting.SingleTarget && chosen.Body.GlobalPosition.DistanceTo(_motor.GlobalPosition) > ReachOf(skill))
            {
                UI.WorldNotice.Show(GetTree(), L10n.T("Too far."));
                return;
            }
        }

        _self.Mana.TrySpend(skill.ManaCost);

        // Its own sound (REF-21); a skill that moves on every hit makes it every hit instead.
        if (!PerHit(def.Motion) && skill.Windup <= 0) Audio.AudioDirector.Play(def.Sound, _motor.GlobalPosition);
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
        Quests.Tutorial.Did(GetTree(), Kiln.Core.Quests.TutorialStep.UseSkill);
    }

    /// <summary>Why <see cref="CanInvest"/> said no, in words, for the console.</summary>
    public string WhyNot(string skillId) =>
        _book is null || _progression is null ? "no character"
        : _progression.UnspentSkillPoints <= 0 ? "no skill points left"
        : !GameContent.IsLoaded || !GameContent.Database.Skills.ContainsKey(skillId) ? "unknown skill"
        : _book.IsFullyInvested(skillId) ? $"already at {SkillBook.MaxPoints} points"
        : !_book.CanLearn(skillId) ? $"already knows {SkillBook.MaxSkills} skills"
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
        && !_book.IsFullyInvested(skillId)
        && _book.CanLearn(skillId);

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
        Quests.Tutorial.Did(GetTree(), Kiln.Core.Quests.TutorialStep.LearnSkill);

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
            && (skill.Targeting is SkillTargeting.SingleTarget or SkillTargeting.Cone or SkillTargeting.Line or SkillTargeting.GroundAoe || skill.Motion == "roll"))
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
        // A slow skill's move (2026-10-02) is played to land as its wait ends, not at once.
        if (!PerHit(skill.Motion) && !Charged(skill)) _motor.GetNodeOrNull<Visual.VisualRoot>("VisualRoot")?.SkillMove(skill.Motion, aim, CastHold(skill), FxColour(skill), skill.Windup);

        // A charged skill gathers itself first: the Warrior draws back, light gathers at the
        // blade, and the blow goes when the windup is spent (REF-21, the Piercing Blow).
        if (Charged(skill))
        {
            _motor.GetNodeOrNull<Visual.VisualRoot>("VisualRoot")?.WindUp(skill.Windup);
            SkillFx.Gather(this, origin, aim, skill.Windup, FxColour(skill), FxBright(skill));
        }

        var center = origin;

        // An area placed on the ground goes on the chosen target, if it is within the skill's
        // range (CLS-04, the Rain of Shafts) — otherwise where the cursor points, as ever.
        if (skill.Targeting == SkillTargeting.GroundAoe)
        {
            var chosen = GetParent().GetNodeOrNull<PlayerCombat>("PlayerCombat")?.Target;
            var range = skill.Range > 0 ? (float)skill.Range : (float)skill.Radius * 2f;

            center = chosen is { IsAlive: true } && chosen.Body.GlobalPosition.DistanceTo(origin) <= range
                ? chosen.Body.GlobalPosition
                : GroundPoint(origin, aim, skill.Range > 0 ? (float)skill.Range * 0.5f : (float)skill.Radius);
        }

        var pulse = new Pulse
        {
            Skill = skill,
            Center = center,
            Aim = aim,
            FollowsCaster = skill.Motion is "roll" or "spin",
            Remaining = System.Math.Max(1, skill.Hits),
            Timer = 0,
        };

        // A charged skill's first hit waits for its windup; any other lands now.
        if (skill.Windup > 0)
        {
            pulse.Timer = skill.Windup;
            _pulses.Add(pulse);
            return;
        }

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
            "frenzy" => StatusEffectSet.Frenzy(skill.Magnitude, skill.Duration),
            "stealth" => StatusEffectSet.Stealth(skill.Magnitude, skill.Duration),
            // The Hexed Blade (CLS-05): stronger blows, and health back from them.
            "hex" => StatusEffectSet.Empower(skill.Magnitude, skill.Duration),
            // The Arcane Mail (CLS-05): less taken, and a share thrown back.
            "mail" => StatusEffectSet.Fortify(skill.Magnitude, skill.Duration),
            "haste" => StatusEffectSet.Haste(skill.Magnitude, skill.Duration),
            "focus" => StatusEffectSet.Focus(skill.Magnitude, skill.Duration),
            _ => null,
        };

        // What rides with it: the share a hexed blade drinks, the share a mail throws back.
        var second = kind switch
        {
            "hex" => StatusEffectSet.Leech(skill.Applies!.Magnitude, skill.Duration),
            "mail" => StatusEffectSet.Thorns(skill.Applies!.Magnitude, skill.Duration),
            _ => null,
        };

        // The Mending Touch (CLS-06): health back at once, a share of the whole.
        if (kind == "heal")
        {
            var mended = (int)System.Math.Round(_self.Stats.MaxHp * skill.Magnitude);

            _self.Heal(mended);
            Combat.CombatFeedback.Heal(_motor.GlobalPosition + (Vector3.Up * 2.0f), mended);
            SkillFx.Cleanse(this, _motor.GlobalPosition);
            return;
        }

        // Going unseen stops the fight with whatever was being fought: the next blow is chosen,
        // not owed. The target stays chosen.
        if (kind == "stealth") GetParent().GetNodeOrNull<PlayerCombat>("PlayerCombat")?.Disengage();

        // The moment of the cast (REF-21); the aura that follows is the status's own.
        var at = _motor.GlobalPosition;

        switch (kind)
        {
            case "fortify": SkillFx.Harden(this, at, FxColour(skill), FxBright(skill)); break;
            case "empower": SkillFx.Sharpen(this, at, FxColour(skill), FxBright(skill)); break;
            case "frenzy": SkillFx.Frenzy(this, at, FxColour(skill), FxBright(skill)); break;
            case "stealth": SkillFx.Harden(this, at, FxColour(skill), FxBright(skill)); break;
            case "hex": SkillFx.Sharpen(this, at, FxColour(skill), FxBright(skill)); break;
            case "mail": SkillFx.Harden(this, at, FxColour(skill), FxBright(skill)); break;
            case "haste": SkillFx.Frenzy(this, at, FxColour(skill), FxBright(skill)); break;
            case "focus": SkillFx.Sharpen(this, at, FxColour(skill), FxBright(skill)); break;
        }

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

        if (second is not null) _self.Statuses.Apply(second);
    }

    /// <summary>
    /// Whether a skill moves, and sounds, on each of its hits rather than as it is cast: the
    /// Whirlwind's turns, the Triple Cut's three cuts, and the Piercing Blow's thrust once its
    /// charge is spent (REF-21).
    /// </summary>
    private static bool PerHit(string motion) => motion is "spin" or "roll" or "triple" or "charge" or "dash" or "volley" or "seals";

    /// <summary>The Triple Cut's three moves, one per hit: across, down on the diagonal, and overhead.</summary>
    private static readonly string[] TripleMoves = ["slice", "wave", "chop"];

    /// <summary>How far the Whirlwind carries the Warrior forward over all its turns, in metres.</summary>
    private const float WhirlAdvance = 3.0f;

    /// <summary>How far a single-target skill lands on the creature chosen.</summary>
    private const float SingleReach = 4.5f;

    /// <summary>How far this skill lands on the creature chosen: its own range, or a blade's reach.</summary>
    private static float ReachOf(ResolvedSkill skill) => skill.Range > 0 ? (float)skill.Range : SingleReach;

    /// <summary>How far a single-target skill reaches for a creature when none is chosen.</summary>

    private const float UnaimedReach = 3.5f;

    /// <summary>How long a self-buff plants the character. Long enough to read as a cast.</summary>
    private const double SelfCastHold = 0.6;

    /// <summary>How long a skill holds the character: every pulse, and a beat after the last.</summary>
    private static double CastHold(ResolvedSkill skill) =>
        skill.Windup + ((System.Math.Max(1, skill.Hits) - 1) * skill.HitInterval) + (skill.Windup > 0 && !Charged(skill) ? 0.5 : 0.35);

    /// <summary>
    /// Whether a skill with a wait gathers itself and is then let go — the Piercing Blow, the Blade
    /// Wave — rather than playing one slow move through it, as the Heavy Strike and the Ground Slam do.
    /// </summary>
    private static bool Charged(ResolvedSkill skill) => skill.Windup > 0 && skill.Motion is "charge" or "wave" or "breath";

    private void FirePulse(Pulse pulse)
    {
        pulse.Remaining--;

        var skill = pulse.Skill;
        var center = pulse.FollowsCaster ? _motor.GlobalPosition : pulse.Center;
        var aim = pulse.Aim;

        // A spinning skill turns the body on every hit. Only the body: the arc stays where the
        // skill was aimed, in front of the Warrior.
        var colour = FxColour(skill);
        var bright = FxBright(skill);

        // A spinning skill turns the body on every hit and makes its sound each time (REF-21:
        // all the way round, 360°).
        if (skill.Motion is "spin" or "roll")
        {
            // No ribbon: a blade turning all the way round draws a disc, and the rings of wind
            // already show the turn.
            _motor.GetNodeOrNull<Visual.VisualRoot>("VisualRoot")?.Spin(skill.HitInterval, pulse.Index);
            Audio.AudioDirector.Play(SoundOf(skill.Id), _motor.GlobalPosition);

            // The Blade Vortex (CLS-03): each turn carries the Ninja on, through whatever is there.
            if (skill.Motion == "roll") Dash(aim, (float)skill.Width, skill.HitInterval, Tween.TransitionType.Linear);

            // The Whirlwind carries the Warrior three metres on in all (2026-10-02, at the user's call), a
            // share of it each turn.
            if (skill.Motion == "spin" && PlayerProfile.Class == CharacterClass.Warrior) Dash(aim, WhirlAdvance / System.Math.Max(1, skill.Hits), skill.HitInterval, Tween.TransitionType.Linear);
        }

        // The Flash Step (CLS-03): the Ninja goes through what stands in the line, to its end or to
        // whatever stops the body, striking with both blades on the way.
        if (skill.Motion == "dash")
        {
            _motor.GetNodeOrNull<Visual.VisualRoot>("VisualRoot")?.SkillMove("charge", aim, 0.35, colour);
            Audio.AudioDirector.Play(SoundOf(skill.Id), _motor.GlobalPosition);
            Dash(aim, (float)skill.Radius);
        }

        // The Piercing Blow's thrust, as the charge is let go.
        if (skill.Motion == "charge" || Charged(skill))
        {
            _motor.GetNodeOrNull<Visual.VisualRoot>("VisualRoot")?.SkillMove(skill.Motion, aim, 0.35, colour);
            Audio.AudioDirector.Play(SoundOf(skill.Id), _motor.GlobalPosition);
        }

        // A slow skill that is not a charge makes its sound as it lands.
        if (skill.Windup > 0 && !Charged(skill) && !PerHit(skill.Motion) && pulse.Index == 0)
        {
            Audio.AudioDirector.Play(SoundOf(skill.Id), _motor.GlobalPosition);
        }

        // The Volley and the Flying Seals (CLS-04, CLS-06): a shot, or a seal thrown, on every hit.
        if (skill.Motion is "volley" or "seals")
        {
            _motor.GetNodeOrNull<Visual.VisualRoot>("VisualRoot")?.SkillMove(skill.Motion, aim, skill.HitInterval);
            Audio.AudioDirector.Play(SoundOf(skill.Id), _motor.GlobalPosition);
        }

        // The Triple Cut: a different cut each hit, each with its sound and its ribbon.

        if (skill.Motion == "triple")
        {
            _motor.GetNodeOrNull<Visual.VisualRoot>("VisualRoot")?.SkillMove(
                TripleMoves[pulse.Index % TripleMoves.Length], aim, skill.HitInterval, colour);
            Audio.AudioDirector.Play(SoundOf(skill.Id), _motor.GlobalPosition);
        }

        List<Combatant> targets;

        switch (skill.Targeting)
        {
            case SkillTargeting.SelfAoe when skill.Motion is "spin" or "roll":
                targets = AreaQuery.Sphere(_motor, center, (float)skill.Radius);
                SkillFx.Whirl(this, center, (float)skill.Radius, colour, skill.HitInterval, pulse.Index, bright);
                break;

            case SkillTargeting.SelfAoe:
                targets = AreaQuery.Sphere(_motor, center, (float)skill.Radius);
                SkillFx.Slam(this, center, (float)skill.Radius, colour, bright);
                break;

            case SkillTargeting.Cone when skill.Motion == "triple":
                targets = AreaQuery.Cone(_motor, center, aim, (float)skill.Radius, (float)skill.ConeAngle);
                SkillFx.Cut(this, center, aim, (float)skill.Radius, pulse.Index, colour, bright);
                break;

            case SkillTargeting.Line when skill.Motion is "charge" or "dash" or "shoot":
                targets = AreaQuery.Line(_motor, center, aim, (float)skill.Radius, (float)skill.Width);
                SkillFx.Pierce(this, center, aim, (float)skill.Radius, colour, bright);
                break;

            case SkillTargeting.Cone:
                targets = AreaQuery.Cone(_motor, center, aim, (float)skill.Radius, (float)skill.ConeAngle);
                SkillFx.Cleave(this, center, aim, (float)skill.Radius, (float)skill.ConeAngle, colour, bright);
                break;

            // The Blade Wave strikes one (2026-10-02): the chosen target if it stands in the ray's way,
            // else the nearest that does — and the ray is drawn to that one, so it reads as one struck.
            case SkillTargeting.Line when skill.MaxTargets == 1 && skill.Motion == "wave":
                var picked = GetParent().GetNodeOrNull<PlayerCombat>("PlayerCombat")?.Target;
                var struck = AreaQuery.Line(_motor, center, aim, (float)skill.Radius, (float)skill.Width)
                    .Where(t => t.IsAlive)
                    .OrderBy(t => ReferenceEquals(t, picked) ? 0 : 1)
                    .ThenBy(t => t.Body.GlobalPosition.DistanceSquaredTo(center))
                    .FirstOrDefault();

                targets = struck is null ? [] : [struck];
                SkillFx.Bolt(this, center, struck?.Body.GlobalPosition ?? center + (aim * (float)skill.Radius), struck is not null, colour, bright);
                break;

            case SkillTargeting.Line:
                targets = AreaQuery.Line(_motor, center, aim, (float)skill.Radius, (float)skill.Width);
                SkillFx.Wave(this, center, aim, (float)skill.Radius, (float)skill.Width, colour, bright);
                break;

            case SkillTargeting.GroundAoe:
                targets = AreaQuery.Sphere(_motor, center, (float)skill.Radius);
                AoeVisual.Circle(center, (float)skill.Radius, hostile: false, skill.Rank);

                // Loosed upward, it comes down as a rain (CLS-04).
                if (skill.Motion == "shoot_up") SkillFx.Rain(this, center, (float)skill.Radius, colour, bright);

                break;

            // A shot, a seal, a claw: at the chosen target from as far as the skill reaches, or at
            // the nearest in front within that reach — and, for one that bursts, at everything
            // round where it lands (CLS-04, CLS-05, CLS-06).
            case SkillTargeting.SingleTarget when skill.Range > 0:
                var marked = GetParent().GetNodeOrNull<PlayerCombat>("PlayerCombat")?.Target;

                if (marked is not null && (!marked.IsAlive || marked.Body.GlobalPosition.DistanceTo(center) > skill.Range + 1.5)) marked = null;

                marked ??= AreaQuery.Cone(_motor, center, aim, (float)skill.Range, 70f)
                    .Where(t => t.IsAlive)
                    .OrderBy(t => t.Body.GlobalPosition.DistanceSquaredTo(center))
                    .FirstOrDefault();

                var lands = marked?.Body.GlobalPosition ?? center + (aim * (float)skill.Range);

                if (skill.Motion == "claw")
                {
                    if (marked is not null) SkillFx.Claw(this, lands, colour, bright);
                }
                else if (skill.Motion is "seals" or "cast")
                {
                    SkillFx.Bolt(this, center, lands, marked is not null, colour, bright);
                }
                else
                {
                    SkillFx.Arrow(this, center, lands, colour, bright, 0.2f);
                }

                targets = marked is null ? [] : [marked];

                if (marked is not null && skill.Radius > 0)
                {
                    SkillFx.Slam(this, lands, (float)skill.Radius, colour, bright, quiet: true);
                    targets = [marked, .. AreaQuery.Sphere(_motor, lands, (float)skill.Radius).Where(t => !ReferenceEquals(t, marked))];
                }

                break;

            case SkillTargeting.SingleTarget:
                var single = GetParent().GetNodeOrNull<PlayerCombat>("PlayerCombat")?.Target;

                // A target can be chosen from afar (the right click, 2026-10-01), but a blow of the
                // hand lands only on what is within its reach.
                if (single is not null && single.Body.GlobalPosition.DistanceTo(center) > SingleReach) single = null;

                // With nothing chosen, or the chosen one out of reach, the nearest creature within
                // reach in front takes it.
                single ??= AreaQuery.Cone(_motor, center, aim, UnaimedReach, 140f)
                    .Where(t => t.IsAlive)
                    .OrderBy(t => t.Body.GlobalPosition.DistanceSquaredTo(center))
                    .FirstOrDefault();

                targets = single is { IsAlive: true } ? [single] : [];

                // The blow landing on the one target, in the rank's colour (REF-03, REF-21).
                if (targets.Count > 0)
                {
                    SkillFx.Strike(this, targets[0].Body.GlobalPosition, colour, bash: skill.Motion == "bash", bright);
                }

                break;

            default:
                targets = [];
                break;
        }

        if (targets.Count == 0) return;

        // A capped skill lands on the nearest few only (REF-21) — the chosen target first, if it is among them.
        if (skill.MaxTargets > 0 && targets.Count > skill.MaxTargets)
        {
            var chosen = GetParent().GetNodeOrNull<PlayerCombat>("PlayerCombat")?.Target;

            targets = targets
                .OrderBy(t => ReferenceEquals(t, chosen) ? 0 : 1)
                .ThenBy(t => t.Body.GlobalPosition.DistanceSquaredTo(center))
                .Take(skill.MaxTargets)
                .ToList();
        }

        // Out of the Shadow Veil (CLS-03): the first blow ends it and is worth more — what the veil
        // gives, or what the skill itself gives for being struck unseen, whichever is more.
        var unseen = _self.Statuses.Has(StatusKind.Stealth);
        var ambush = unseen
            ? 1 + System.Math.Max(_self.Statuses.MagnitudeOf(StatusKind.Stealth), skill.Applies is null && skill.Targeting == SkillTargeting.SingleTarget ? skill.Magnitude : 0)
            : 1.0;

        if (unseen) _self.Statuses.Remove(StatusKind.Stealth);

        var sparks = 0;

        foreach (var target in targets)
        {
            if (!target.IsAlive) continue;

            // Sparks on everything an area skill touches (REF-21), a handful at most: a pull of
            // twenty does not need twenty flares to read as hit.
            if (skill.Targeting != SkillTargeting.SingleTarget && sparks++ < 8)
            {
                SkillFx.Impact(this, target.Body.GlobalPosition, colour, 0.8f, bright);
            }

            // A skill that deals nothing only leaves its mark: the Dread (CLS-05).
            var dealt = skill.DamageCoef > 0
                ? target.TakeAttack(_self, skillCoef: skill.DamageCoef * ambush, skill: true, pierce: skill.Pierces, element: skill.Element)
                : new DamageResult(0, false, false, false, 0);

            // The Spellbreak (CLS-05): what it had raised is gone.
            if (target.IsAlive && !dealt.Evaded && target.Body is EnemyBrain raised
                && GameContent.Database.Skills.TryGetValue(skill.Id, out var cast) && cast.Dispels)
            {
                raised.Dispel();
            }

            // What is not a creature — a shard — cannot be stunned, but can be weakened.
            var resist = (target.Body as EnemyBrain)?.StunResist ?? 1.0;

            // A poison or a bleeding the skill gives a strength of its own is a share of the blow
            // that put it there, every tick, so it grows with the blade and the points (CLS-03).
            if (skill.Applies is { Kind: "poison" or "bleed" or "burn" } dot && skill.Magnitude > 0)
            {
                if (target.IsAlive && !dealt.Evaded && GameSession.CombatRng.Chance(dot.Chance))
                {
                    var tick = System.Math.Max(1.0, dealt.Amount * skill.Magnitude);

                    target.Statuses.Apply(dot.Kind == "poison" ? StatusEffectSet.Poison(tick, dot.Duration)
                        : dot.Kind == "burn" ? StatusEffectSet.Burn(tick, dot.Duration) : StatusEffectSet.Bleed(tick, dot.Duration));
                }

                continue;
            }

            StatusApplication.Try(skill.Applies, target, resist);
        }
    }

    /// <summary>How long the Flash Step takes to cross its line.</summary>
    private const double DashSeconds = 0.14;

    /// <summary>How far short of a wall the dash stops.</summary>
    private const float DashMargin = 0.4f;

    /// <summary>
    /// Carries the body along <paramref name="aim"/>, up to <paramref name="reach"/> metres, through
    /// creatures but not through the world: as far as a ray at chest height finds clear.
    /// </summary>
    public void Dash(Vector3 aim, float reach, double seconds = DashSeconds, Tween.TransitionType ease = Tween.TransitionType.Quad)
    {
        var from = _motor.GlobalPosition;
        var chest = from + (Vector3.Up * 0.9f);
        var query = PhysicsRayQueryParameters3D.Create(chest, chest + (aim * reach), Layers.World);
        query.CollideWithAreas = false;

        var hit = _motor.GetWorld3D().DirectSpaceState.IntersectRay(query);
        var far = hit.Count == 0 ? reach : System.Math.Max(0f, chest.DistanceTo(hit["position"].AsVector3()) - DashMargin);

        if (far < 0.2f) return;

        var tween = _motor.CreateTween();
        tween.TweenProperty(_motor, "global_position", from + (aim * far), seconds).SetTrans(ease).SetEase(Tween.EaseType.Out);
    }

    private bool _unseenShown;

    /// <summary>The body drawn faint while the Shadow Veil is on, and whole again when it ends.</summary>
    private void ShowUnseen()
    {
        var unseen = _self.Statuses.Has(StatusKind.Stealth);

        if (unseen == _unseenShown) return;

        _unseenShown = unseen;

        if (_motor.GetNodeOrNull<Node>("VisualRoot") is not { } visual) return;

        foreach (var mesh in visual.FindChildren("*", "GeometryInstance3D", true, false).OfType<GeometryInstance3D>())
        {
            mesh.Transparency = unseen ? 0.7f : 0f;
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
