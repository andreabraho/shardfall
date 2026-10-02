using Godot;
using Kiln.Game.Combat;
using Kiln.Game.Input;

namespace Kiln.Game.Player;

/// <summary>
/// The defensive cooldown on its own key: the Warrior's Guard Stance (CBT-10, decision D4), and
/// the Ninja's Shadow Step (CLS-03, 2026-10-01) — a short dash the way the Ninja is going, and a
/// moment in which nothing touches it.
/// <para>
/// This is the replacement for the dodge roll that click-to-move rules out. It restores the
/// "did you react in time" moment: a short, high-value window that has to be spent on the
/// right telegraph, with a cost — you cannot move while holding it, so guarding instead of
/// walking out is a real trade rather than a free button.
/// </para>
/// </summary>
public partial class DefensiveAbility : Node
{
    private PlayerMotor _motor = null!;
    private Combatant _self = null!;
    private double _active;
    private double _cooldown;

    /// <summary>
    /// The skill this ability is. All of its numbers come from that definition, so Guard
    /// Stance tunes and ranks up exactly like every other skill instead of having a second,
    /// silently diverging copy of its values in code.
    /// </summary>
    [Export] public string SkillId { get; set; } = "skl_guard_stance";

    // Fallbacks, used only if the definition is missing.
    [Export] public double DamageReduction { get; set; } = 0.70;
    [Export] public double Duration { get; set; } = 1.2;
    [Export] public double CooldownSeconds { get; set; } = 10.0;
    [Export] public double ManaCost { get; set; } = 10;

    public bool IsGuarding => _active > 0;

    public double CooldownRemaining => System.Math.Max(0, _cooldown);

    /// <summary>The length of the cooldown now running, so a bar can show how far through it is.</summary>
    public double CooldownTotal { get; private set; } = 1;

    /// <summary>Whether the player has the mana for it right now.</summary>
    public bool Affordable => _self.Mana.CanAfford(Values().Mana);

    [Signal] public delegate void GuardChangedEventHandler(bool guarding);

    private GuardVisual _visual = null!;

    /// <summary>The Ninja's: a dash and a moment untouched, in place of a guard held.</summary>
    public const string StepId = "skl_shadow_step";

    /// <summary>The Archer's (CLS-04): a leap away from where it faces, and a moment untouched.</summary>
    public const string BackstepId = "skl_backstep";

    /// <summary>The Asura's (CLS-05): a shell that takes blows in its place.</summary>
    public const string WardId = "skl_shadow_ward";

    /// <summary>The Dragonseer's (CLS-06): a shell, and every harmful effect gone as it is raised.</summary>
    public const string SealId = "skl_pure_seal";

    /// <summary>True for a class that moves out of the way rather than standing behind a guard.</summary>
    private static bool Steps => PlayerProfile.Class is Kiln.Core.Foundation.CharacterClass.Blade or Kiln.Core.Foundation.CharacterClass.Archer;

    /// <summary>True for a class whose defence is a shell that takes blows, and leaves it free to move.</summary>
    private static bool Wards => PlayerProfile.Class is Kiln.Core.Foundation.CharacterClass.Sura or Kiln.Core.Foundation.CharacterClass.Shaman;

    /// <summary>The defence on Shift of the class played.</summary>
    public static string SkillOf(Kiln.Core.Foundation.CharacterClass played) => played switch
    {
        Kiln.Core.Foundation.CharacterClass.Blade => StepId,
        Kiln.Core.Foundation.CharacterClass.Archer => BackstepId,
        Kiln.Core.Foundation.CharacterClass.Sura => WardId,
        Kiln.Core.Foundation.CharacterClass.Shaman => SealId,
        _ => "skl_guard_stance",
    };

    public override void _Ready()
    {
        SkillId = SkillOf(PlayerProfile.Class);

        _motor = GetParent<PlayerMotor>();
        _self = GetParent().GetNode<Combatant>("Combatant");

        // Created in code rather than placed in the scene: it belongs to this ability and
        // has no meaning without it.
        _visual = new GuardVisual { Name = "GuardVisual" };

        // Deferred: the parent is still building its own children during _Ready, and a
        // direct AddChild there fails outright.
        _motor.CallDeferred(Node.MethodName.AddChild, _visual);

        // Flash on every absorbed hit — otherwise a successful guard looks like nothing
        // happened, which is the worst possible feedback for a defensive cooldown.
        _self.Damaged += (_, _, evaded) =>
        {
            if (IsGuarding && !evaded && !Steps) _visual.Flash();
        };

    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // A panel has the player's attention; firing a skill behind it is never intended.
        if (UI.UiState.ModalOpen) return;

        if (!@event.IsActionPressed(GameActions.DefensiveAbility)) return;

        TryGuard();
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Current values, including whatever mastery has changed.</summary>
    private (double Reduction, double Duration, double Cooldown, double Mana) Values()
    {
        var book = GetParent().GetNodeOrNull<PlayerCharacter>("PlayerCharacter")?.Skills;

        if (book is null
            || !book.IsUnlocked(SkillId)
            || !GameContent.IsLoaded
            || !GameContent.Database.Skills.TryGetValue(SkillId, out var def))
        {
            return (DamageReduction, Duration, CooldownSeconds, ManaCost);
        }

        var skill = Kiln.Data.Definitions.ResolvedSkill.For(def, book.RankOf(SkillId));
        return (skill.Magnitude, skill.Duration, skill.Cooldown, skill.ManaCost);
    }

    private void TryGuard()
    {
        if (IsGuarding || _cooldown > 0 || !_self.IsAlive || _self.Statuses.IsStunned) return;

        if (Steps)
        {
            Step();
            return;
        }

        if (Wards)
        {
            Ward();
            return;
        }

        var values = Values();

        if (!_self.Mana.TrySpend(values.Mana))
        {
            GD.Print($"[guard] not enough mana ({_self.Mana})");
            return;
        }

        Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndGuard);

        // Raising the guard shakes off whatever was eating at the Warrior: poison, bleeding,
        // a slow, a weakening, a vulnerability (2026-09-26, at your call). A stun still holds —
        // a stunned Warrior cannot raise the guard in the first place.
        var cleansed = _self.Statuses.CleanseAll();

        if (cleansed > 0)
        {
            Combat.SkillFx.Cleanse(this, _motor.GlobalPosition);
            GD.Print($"[guard] cleansed {cleansed} harmful effect(s)");
        }

        // The dome wears the rank the guard is held at (REF-03).
        _visual.Rank = GetParent().GetNodeOrNull<PlayerCharacter>("PlayerCharacter")?.Skills.RankOf(SkillId)
            ?? Kiln.Core.Progression.MasteryRank.Normal;

        _active = values.Duration;
        _cooldown = values.Cooldown;
        CooldownTotal = System.Math.Max(0.01, values.Cooldown);

        GetParent().GetNodeOrNull<PlayerCharacter>("PlayerCharacter")?.Skills.RecordUse(SkillId);

        _self.IncomingDamageMultiplier = 1.0 - values.Reduction;

        // And nothing new takes hold while it is up (2026-09-28, at your call).
        _self.Statuses.Warded = true;
        _motor.MovementLocked = true;
        _motor.Stop();
        _visual.SetGuarding(true);

        EmitSignal(SignalName.GuardChanged, true);
    }

    /// <summary>
    /// The Shadow Step: the way the Ninja is walking, or the way it faces when standing, as far
    /// as the skill's radius and no further than a wall; untouched for the skill's duration.
    /// </summary>
    private void Step()
    {
        if (!GameContent.IsLoaded || !GameContent.Database.Skills.TryGetValue(SkillId, out var def)) return;

        var book = GetParent().GetNodeOrNull<PlayerCharacter>("PlayerCharacter")?.Skills;
        var skill = Kiln.Data.Definitions.ResolvedSkill.For(def, book?.RankOf(SkillId) ?? Kiln.Core.Progression.MasteryRank.Normal);

        if (!_self.Mana.TrySpend(skill.ManaCost))
        {
            GD.Print($"[step] not enough mana ({_self.Mana})");
            return;
        }

        var going = _motor.Velocity with { Y = 0 };
        var back = def.Motion == "backstep";

        // The Ninja goes the way it is going; the Archer away from what it faces, still facing it.
        var aim = back ? -_motor.Facing : going.LengthSquared() > 0.25f ? going.Normalized() : _motor.Facing;

        Audio.AudioDirector.Play(def.Sound, _motor.GlobalPosition);
        Combat.SkillFx.Pierce(this, _motor.GlobalPosition, aim, (float)skill.Radius, new Color(def.Color), 0.7f);

        _motor.Stop();
        _motor.HoldFor(StepSeconds);
        _motor.FaceTowards(back ? -aim : aim);
        _motor.GetNodeOrNull<Visual.VisualRoot>("VisualRoot")?.SkillMove(back ? "backstep" : "step", aim, back ? StepSeconds + 0.14 : StepSeconds);
        GetParent().GetNodeOrNull<SkillCaster>("SkillCaster")?.Dash(aim, (float)skill.Radius, StepSeconds);

        _active = skill.Duration;
        _cooldown = skill.Cooldown;
        CooldownTotal = System.Math.Max(0.01, skill.Cooldown);
        book?.RecordUse(SkillId);

        _self.Untouchable = true;

        EmitSignal(SignalName.GuardChanged, true);
    }

    /// <summary>
    /// The Shadow Ward and the Pure Seal (CLS-05, CLS-06): a shell worth a share of maximum health
    /// that takes blows in the caster's place for the skill's duration. The body stays free to
    /// move and to fight. The seal also clears every harmful effect as it is raised.
    /// </summary>
    private void Ward()
    {
        if (!GameContent.IsLoaded || !GameContent.Database.Skills.TryGetValue(SkillId, out var def)) return;

        var book = GetParent().GetNodeOrNull<PlayerCharacter>("PlayerCharacter")?.Skills;
        var skill = Kiln.Data.Definitions.ResolvedSkill.For(def, book?.RankOf(SkillId) ?? Kiln.Core.Progression.MasteryRank.Normal);

        if (!_self.Mana.TrySpend(skill.ManaCost))
        {
            GD.Print($"[ward] not enough mana ({_self.Mana})");
            return;
        }

        Audio.AudioDirector.Play(def.Sound, _motor.GlobalPosition);

        if (SkillId == SealId && _self.Statuses.CleanseAll() > 0) Combat.SkillFx.Cleanse(this, _motor.GlobalPosition);

        _motor.GetNodeOrNull<Visual.VisualRoot>("VisualRoot")?.SkillMove("raise", _motor.Facing, 0.4);

        _visual.Rank = book?.RankOf(SkillId) ?? Kiln.Core.Progression.MasteryRank.Normal;
        _visual.SetGuarding(true);

        _self.Barrier = _self.Stats.MaxHp * skill.Magnitude;
        _warding = true;
        _active = skill.Duration;
        _cooldown = skill.Cooldown;
        CooldownTotal = System.Math.Max(0.01, skill.Cooldown);
        book?.RecordUse(SkillId);

        EmitSignal(SignalName.GuardChanged, true);
    }

    /// <summary>True while a shell is up: it ends early when it is spent.</summary>
    private bool _warding;

    /// <summary>How long the step takes to cross its distance.</summary>

    private const double StepSeconds = 0.18;

    public override void _Process(double delta)
    {
        if (_cooldown > 0) _cooldown -= delta;

        if (_active <= 0) return;

        _active -= delta;

        // Dying mid-guard must not leave the player permanently rooted; a shell ends when it is spent.
        if (_active > 0 && _self.IsAlive && !(_warding && _self.Barrier <= 0)) return;

        EndGuard();
    }

    private void EndGuard()
    {
        _active = 0;
        _self.Untouchable = false;
        _self.Barrier = 0;
        _warding = false;
        _self.IncomingDamageMultiplier = 1.0;
        _self.Statuses.Warded = false;
        _motor.MovementLocked = false;
        _visual.SetGuarding(false);

        EmitSignal(SignalName.GuardChanged, false);
    }
}
