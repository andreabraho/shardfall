using System.Collections.Generic;
using System.Linq;
using Godot;
using Kiln.Core.Combat;
using Kiln.Core.Foundation;
using Kiln.Core.Progression;
using Kiln.Data.Definitions;
using Kiln.Game.Items;

namespace Kiln.Game.UI;

/// <summary>
/// Puts a skill into words (REF-03): what it does, at the investment the player has in it.
/// </summary>
/// <remarks>
/// One place, because the skill screen and the hotbar tooltip have to agree. They did not
/// before: the bar listed mana and cooldown from the base definition while the character
/// sheet showed a rank, so a mastered skill advertised the numbers of an unmastered one.
/// Everything here is read through <see cref="ResolvedSkill"/>, so what is printed is what
/// will be cast.
/// </remarks>
public static class SkillText
{
    /// <summary>The skills of the class played, in the order they become available.</summary>
    public static IEnumerable<SkillDef> ClassSkills() =>
        !GameContent.IsLoaded
            ? []
            : GameContent.Database.Skills.Values
                .Where(s => s.Class == PlayerProfile.Class)
                // Its one branch, and what has a key of its own whatever the branch (the Guard).
                .Where(s => PlayerProfile.Path.Length == 0 || s.Tree == PlayerProfile.Path || s.CastType == "channel")
                .OrderBy(s => s.SuggestedLevel)
                .ThenBy(s => s.Id, System.StringComparer.Ordinal);

    /// <summary>
    /// The intelligence of the character as it stands — what is worn counted — or what was placed
    /// when there is no character in the world to ask.
    /// </summary>
    private static int Mind() =>
        (Engine.GetMainLoop() as SceneTree)?.GetFirstNodeInGroup("player")?.GetNodeOrNull<Combat.Combatant>("Combatant")?.Stats.Attributes.Int
        ?? PlayerProfile.Progression?.TotalAttributes.Int
        ?? Attributes.StartingValue;

    /// <summary>One line saying what a share takes from intelligence, and what it comes to with the player's.</summary>
    private static string Grown(string what, double strength, double perPoint, double cap) =>
        L10n.F("{0}: {1:0.##}% more for each point of INT, up to {2:P0}. With yours: {3:P1}.", what, perPoint * 100, cap, Attributes.SeerScaled(strength, Mind(), perPoint, cap));

    /// <summary>Name, rank and points: the one line that says how far a skill has come.</summary>
    public static string Title(SkillDef def, SkillBook book)
    {
        var name = GameItems.Localise(def.Name);
        var points = book.PointsIn(def.Id);

        if (points == 0) return name;

        var rank = book.RankOf(def.Id);
        var rankWord = rank == MasteryRank.Normal ? "" : $"  ·  {Words.Of(rank)}";

        return $"{name}  ·  {points}/{SkillBook.MaxPoints}{rankWord}";
    }

    /// <summary>
    /// What the skill does right now, in full: damage, cost, shape, effect, and what the next
    /// rank costs.
    /// </summary>
    public static string Describe(SkillDef def, SkillBook book)
    {
        var points = System.Math.Max(1, book.PointsIn(def.Id));
        var skill = ResolvedSkill.For(def, book.RankOf(def.Id), points);
        var lines = new List<string>();

        if (skill.DamageCoef > 0)
        {
            lines.Add(skill.Hits > 1
                ? L10n.F("{0:P0} of your attack power, {1} times", skill.DamageCoef, skill.Hits)
                : L10n.F("{0:P0} of your attack power", skill.DamageCoef));
        }

        // The Mental Warrior: its skills take a little from vitality besides (2026-10-02).
        if (skill.DamageCoef > 0 && def.Class == CharacterClass.Warrior && PlayerProfile.Path == PlayerProfile.MentalPath)
        {
            lines.Add(L10n.F("Skills also take {0:0.#} attack power from each point of VIT.", Attributes.MentalSkillPerVit));
        }

        // The Dragonseer (CLS-06): everything it strikes with comes from intelligence.
        if (skill.DamageCoef > 0 && def.Class == CharacterClass.Shaman)
        {
            lines.Add(L10n.F("Your attack power takes {0:0.#} from each point of INT. With yours: +{1:0}.", Attributes.AttackPerPoint, Mind() * Attributes.AttackPerPoint));
        }

        // The Asura (CLS-05): its skills take from intelligence besides.
        if (skill.DamageCoef > 0 && def.Class == CharacterClass.Sura)
        {
            lines.Add(L10n.F("Skills also take {0:0.#} attack power from each point of INT. With yours: +{1:0}.", Attributes.AsuraSkillPerInt, Mind() * Attributes.AsuraSkillPerInt));
        }

        // The Storm Orb (CLS-06): what its radius is for is the leap, not a burst.
        if (def.Motion != "counter") lines.Add(def.Bounces > 0
            ? L10n.F("One target up to {0:0.#} m away; then it leaps to up to {1} more, each within {2:0.#} m of the last.", skill.Range, def.Bounces, skill.Radius)
            : Shape(skill));

        if (def.Element == DamageElement.Lightning) lines.Add(L10n.T("Lightning damage."));

        if (def.Pierces) lines.Add(L10n.T("Ignores the target's defence."));

        if (def.Element == DamageElement.Fire) lines.Add(L10n.T("Fire damage."));

        if (def.Element == DamageElement.Dark) lines.Add(L10n.T("Dark damage."));

        if (def.Guards > 0) lines.Add(L10n.F("+{0:0} defence for {1:0.#} s, and {2:0.#} more for each point of INT. With yours: +{3:0}.", def.Guards, def.Duration, Attributes.AsuraGuardPerInt, def.Guards + (Mind() * Attributes.AsuraGuardPerInt)));

        if (def.Dispels) lines.Add(L10n.T("Strips the target's rage and any shield held over it."));

        if (def.Windup > 0) lines.Add(L10n.F("Lands {0:0.#} s after it is cast.", def.Windup));

        if (def.NeedsTarget) lines.Add(L10n.T("Needs a target chosen (right click)."));

        // The Blade Vortex: it goes forward as it turns (CLS-03).
        if (skill.Motion == "roll")
        {
            lines.Add(L10n.F("You go {0:0.#} m forward as you turn, through whatever stands in the way.", skill.Width * System.Math.Max(1, skill.Hits)));
        }

        // The Shadow Strike: a blow that is worth more from out of the veil (CLS-03).
        if (skill.Targeting == SkillTargeting.SingleTarget && skill.Magnitude > 0 && skill.Applies is null)
        {
            lines.Add(L10n.F("Struck while unseen, it deals {0:P0} more damage.", skill.Magnitude));
        }

        if (Effect(skill) is { Length: > 0 } effect) lines.Add(effect);

        // Everything of the Asura's that is a share grows with intelligence (CLS-05).
        if (def.Class == CharacterClass.Sura)
        {
            switch (def.Applies?.Kind)
            {
                case "hex":
                    lines.Add(L10n.F("With your INT: +{0:0} attack power.", skill.Magnitude + (Mind() * Attributes.AsuraBladePerInt)));
                    break;
                case "mail":
                    lines.Add(Grown(L10n.T("Damage taken"), skill.Magnitude, Attributes.AsuraSharePerInt, Attributes.AsuraMailCap));
                    lines.Add(Grown(L10n.T("Thrown back"), def.Applies.Magnitude, Attributes.AsuraThornsPerInt, Attributes.AsuraThornsCap));
                    break;
                case "weaken":
                    lines.Add(Grown(L10n.T("Enemy damage"), def.Applies.Magnitude, Attributes.AsuraSharePerInt, Attributes.AsuraDreadCap));
                    break;
                case "vulnerability":
                    lines.Add(Grown(L10n.T("Damage it takes"), def.Applies.Magnitude, Attributes.AsuraSharePerInt, Attributes.AsuraBreakCap));
                    break;
            }

        }

        // The Dragonseer's blessings grow with intelligence (CLS-06).
        if (def.Class == CharacterClass.Shaman && (def.Applies?.Kind is "fortify" or "focus" || def.Id == Player.DefensiveAbility.SealId))
        {
            var (per, cap) = def.Applies?.Kind == "fortify" ? (Attributes.SeerBlessingPerInt, Attributes.SeerBlessingCap)
                : def.Applies?.Kind == "focus" ? (Attributes.SeerEyePerInt, Attributes.SeerEyeCap)
                : (Attributes.SeerSealPerInt, Attributes.SeerSealCap);
            lines.Add(L10n.F("Grows with INT: {0:0.##}% more a point, up to {1:P0}. With yours: {2:P1}.", per * 100, cap, Attributes.SeerScaled(skill.Magnitude, Mind(), per, cap)));
        }

        // The Shadow Step (CLS-03): a channelled skill that goes somewhere.
        // The Backlash (CLS-05): a blow turned back.
        if (def.CastType == "channel" && def.Motion == "counter")
        {
            lines.Add(L10n.F("For {0:0.##} s the next blow that would land on you does nothing, and dark force bursts round you: {1:P0} of your attack power to everything within {2:0.#} m. You stay free to move and to fight.", skill.Duration, skill.DamageCoef, skill.Radius));
        }

        if (def.CastType == "channel" && def.Radius > 0 && def.Motion != "counter")
        {
            lines.Add(def.Motion == "backstep"
                ? L10n.F("A leap of {0:0.#} m away from what you face. Nothing touches you for {1:0.##} s.", skill.Radius, skill.Duration)
                : L10n.F("A dash of {0:0.#} m the way you are going. Nothing touches you for {1:0.##} s.", skill.Radius, skill.Duration));
        }

        // A shell (CLS-05, CLS-06): it takes blows in your place, and you stay free to move.
        if (def.CastType == "channel" && def.Motion == "ward")
        {
            lines.Add(L10n.F("A shell that takes blows in your place, up to {0:P0} of your maximum health, for {1:0.#} s. You stay free to move and to fight.", skill.Magnitude, skill.Duration));

            if (def.Id == Player.DefensiveAbility.SealId) lines.Add(L10n.T("Raising it clears poison, bleeding, burning, slows, weakening and vulnerability."));
        }

        // The Guard, the other channelled skill, also cleanses as it is raised (2026-09-26).
        if (def.CastType == "channel" && def.Radius <= 0 && def.Motion is not ("ward" or "counter"))
        {

            lines.Add(L10n.T("Raising it clears poison, bleeding, slows, weakening and vulnerability."));
            lines.Add(L10n.T("While it is up, no stun, poison, bleeding, slow, weakening or vulnerability takes hold."));
        }

        lines.Add(L10n.F("{0:0} mana  ·  {1:0.#} s cooldown", skill.ManaCost, skill.Cooldown));

        if (book.PointsIn(def.Id) > 0) lines.Add(Next(def, book));

        return string.Join("\n", lines);
    }

    /// <summary>
    /// One line: the numbers that decide whether a skill is worth a point, for the compact
    /// skill screen. The full text is a hover away.
    /// </summary>
    public static string Summary(SkillDef def, SkillBook book)
    {
        var skill = ResolvedSkill.For(def, book.RankOf(def.Id), System.Math.Max(1, book.PointsIn(def.Id)));
        var parts = new List<string>();

        if (skill.DamageCoef > 0)
        {
            parts.Add(skill.Hits > 1
                ? L10n.F("{0:P0} ×{1}", skill.DamageCoef, skill.Hits)
                : L10n.F("{0:P0}", skill.DamageCoef));
        }
        else if (skill.Applies is { Kind: "fortify" })
        {
            parts.Add(L10n.F("−{0:P0} damage taken", skill.Magnitude));
        }
        else if (skill.Applies is { Kind: "empower" })
        {
            parts.Add(L10n.F("+{0:P0} average damage", skill.Magnitude));
        }
        else if (skill.Applies is { Kind: "frenzy" })
        {
            parts.Add(L10n.F("+{0:P0} attack speed", skill.Magnitude));
        }
        else if (skill.Applies is { Kind: "stealth" })
        {
            parts.Add(L10n.F("unseen {0:0.#} s", skill.Duration));
        }
        else if (skill.Applies is { Kind: "hex" })
        {
            parts.Add(L10n.F("+{0:0} attack", skill.Magnitude));
        }
        else if (skill.Applies is { Kind: "mail" })
        {
            parts.Add(L10n.F("−{0:P0} damage taken", skill.Magnitude));
        }
        else if (skill.Applies is { Kind: "haste" })
        {
            parts.Add(L10n.F("+{0:P0} speed", skill.Magnitude));
        }
        else if (skill.Applies is { Kind: "focus" })
        {
            parts.Add(L10n.F("+{0:P0} critical", skill.Magnitude));
        }
        else if (skill.Applies is { Kind: "heal" })
        {
            parts.Add(L10n.F("heals {0:P0}", skill.Magnitude));
        }
        else if (skill.Applies is { Kind: "weaken" })
        {
            parts.Add(L10n.F("−{0:P0} enemy damage", skill.Applies.Magnitude));
        }
        else if (def.CastType == "channel" && def.Motion == "counter")
        {
            parts.Clear();
            parts.Add(L10n.F("turns a blow back, {0:P0}", skill.DamageCoef));
        }
        else if (def.CastType == "channel" && def.Motion == "ward")
        {
            parts.Add(L10n.F("absorbs {0:P0}", skill.Magnitude));
        }
        else if (def.CastType == "channel" && def.Radius > 0)
        {
            parts.Add(L10n.F("{0:0.#} m, untouched {1:0.##} s", skill.Radius, skill.Duration));
        }

        else if (skill.Magnitude > 0)
        {
            parts.Add(L10n.F("blocks {0:P0}", skill.Magnitude));
        }

        parts.Add(L10n.F("{0:0} mana", skill.ManaCost));
        parts.Add(L10n.F("{0:0.#} s", skill.Cooldown));

        return string.Join("  ·  ", parts);
    }

    private static string Shape(ResolvedSkill skill) => skill.Targeting switch
    {
        SkillTargeting.Self => L10n.T("On yourself."),
        SkillTargeting.SingleTarget when skill.Range > 0 && skill.Radius > 0 => L10n.F("One target up to {0:0.#} m away, and everything within {1:0.#} m of it.", skill.Range, skill.Radius),
        SkillTargeting.SingleTarget when skill.Range > 0 => L10n.F("One target up to {0:0.#} m away.", skill.Range),
        SkillTargeting.SingleTarget => L10n.T("One target."),
        SkillTargeting.GroundAoe when skill.Range > 0 => L10n.F("Everything within {0:0.#} m of your target, up to {1:0.#} m away — or of where you aim.", skill.Radius, skill.Range),
        SkillTargeting.Cone => L10n.F("Everything in a {0:0}° arc {1:0.#} m in front of you.", skill.ConeAngle, skill.Radius),
        SkillTargeting.SelfAoe => L10n.F("Everything within {0:0.#} m of you.", skill.Radius),
        SkillTargeting.GroundAoe => L10n.F("Everything within {0:0.#} m of where you aim.", skill.Radius),
        SkillTargeting.Line => L10n.F("Everything in a line {0:0.#} m ahead of you, {1:0.#} m wide.", skill.Radius, skill.Width),
        _ => L10n.F("Everything within {0:0.#} m.", skill.Radius),
    };

    private static string Effect(ResolvedSkill skill)
    {
        if (skill.Applies is not { } applies) return "";

        // The self-buffs take their strength from the resolved skill, so investment reaches
        // them; everything else applies exactly what the data says.
        var own = applies.Kind is "fortify" or "empower" or "frenzy" or "stealth" or "hex" or "mail" or "haste" or "focus" or "heal";
        var magnitude = own ? skill.Magnitude : applies.Magnitude;
        var duration = own ? skill.Duration : applies.Duration;

        var body = applies.Kind switch
        {
            "fortify" => L10n.F("Take {0:P0} less damage for {1:0.#} s, moving {2:P0} slower.", magnitude, duration, StatusEffectSet.FortifyDrag),
            "frenzy" => L10n.F("Attack {0:P0} faster and move {1:P0} faster for {2:0.#} s, taking {3:P0} more damage.",
                magnitude, magnitude * 0.5, duration, StatusEffectSet.FrenzyExposure),
            "empower" => L10n.F("Your blows — not your skills — deal {0:P0} more damage for {1:0.#} s.", magnitude, duration),
            "stealth" => L10n.F("Unseen for {0:0.#} s: creatures lose you. Your first blow ends it and deals {1:P0} more damage.", duration, magnitude),
            "stun" => L10n.F("Stuns for {0:0.#} s.", duration),
            "vulnerability" => L10n.F("The target takes {0:P0} more damage for {1:0.#} s.", magnitude, duration),
            "slow" => L10n.F("Slows by {0:P0} for {1:0.#} s.", magnitude, duration),
            "weaken" => L10n.F("The target deals {0:P0} less damage for {1:0.#} s.", magnitude, duration),
            "poison" when skill.Magnitude > 0 => L10n.F("Poisons for {0:0.#} s: {1:P0} of the blow every second, up to five times over.", duration, skill.Magnitude),
            "poison" => L10n.F("Poisons for {0:0.#} s.", duration),
            "bleed" => L10n.F("Makes the target bleed for {0:0.#} s.", duration),
            "burn" when skill.Magnitude > 0 => L10n.F("Burns for {0:0.#} s: {1:P0} of the blow every second, up to three times over.", duration, skill.Magnitude),
            "burn" => L10n.F("Burns for {0:0.#} s.", duration),
            "hex" => L10n.F("+{0:0} attack power for {1:0.#} s, and {2:0.#} more for each point of INT; {3:P0} of the damage of each blow comes back as health.", magnitude, duration, Attributes.AsuraBladePerInt, applies.Magnitude),
            "mail" => L10n.F("Take {0:P0} less damage for {1:0.#} s, moving {2:P0} slower; {3:P0} of every blow taken from close by is thrown back.", magnitude, duration, StatusEffectSet.FortifyDrag, applies.Magnitude),
            "haste" => L10n.F("Move {0:P0} faster for {1:0.#} s, and one blow in ten finds nothing.", magnitude, duration),
            "focus" => L10n.F("+{0:P0} critical chance for {1:0.#} s.", magnitude, duration),
            "heal" => L10n.F("Heals {0:P0} of your maximum health at once.", magnitude),
            _ => "",
        };

        if (body.Length == 0 || applies.Chance >= 1) return body;

        return body + " " + L10n.F("({0:P0} of the time)", applies.Chance);
    }

    /// <summary>
    /// What spending one more point does (REF-19): the grade it moves to and every number that
    /// changes, before and after — or, for a skill not yet learned, what learning it gives.
    /// </summary>
    public static string Change(SkillDef def, SkillBook book)
    {
        var points = book.PointsIn(def.Id);
        var name = GameItems.Localise(def.Name);

        if (book.IsFullyInvested(def.Id))
        {
            return name + "\n" + L10n.T("Fully invested. It ranks up from here by being used.");
        }

        if (points == 0)
        {
            var learn = new SkillBook();
            learn.Invest(def.Id);

            return L10n.F("Learn {0}", name) + "\n" + Describe(def, learn);
        }

        var next = points + 1;
        var nextRank = next >= SkillBook.MaxPoints ? MasteryRank.Master : book.RankOf(def.Id);
        var now = ResolvedSkill.For(def, book.RankOf(def.Id), points);
        var then = ResolvedSkill.For(def, nextRank, next);
        var lines = new List<string> { L10n.F("{0}  ·  grade {1} → {2}", name, points, next) };

        void Line(string label, double a, double b, string format)
        {
            if (System.Math.Abs(a - b) < 0.0005) return;

            lines.Add($"{label}: {string.Format(L10n.Culture, format, a)} → {string.Format(L10n.Culture, format, b)}");
        }

        Line(L10n.T("Damage"), now.DamageCoef, then.DamageCoef, "{0:P0}");
        Line(L10n.T("Strength"), now.Magnitude, then.Magnitude, "{0:P0}");
        Line(L10n.T("Duration"), now.Duration, then.Duration, "{0:0.#} s");
        Line(L10n.T("Reach"), now.Radius, then.Radius, "{0:0.#} m");
        Line(L10n.T("Mana"), now.ManaCost, then.ManaCost, "{0:0}");
        Line(L10n.T("Cooldown"), now.Cooldown, then.Cooldown, "{0:0.#} s");

        if (nextRank == MasteryRank.Master && book.RankOf(def.Id) != MasteryRank.Master)
        {
            lines.Add(L10n.T("This point masters it: from here it ranks up by being used."));
        }

        return string.Join("\n", lines);
    }

    /// <summary>What the next rank costs: points while they buy it, casts once they do not.</summary>
    private static string Next(SkillDef def, SkillBook book)
    {
        var owed = book.ToNextRank(def.Id);

        if (owed <= 0) return L10n.T("Perfect. Nothing left to earn.");

        return book.NextRankCostsPoints(def.Id)
            ? L10n.F("{0} more point(s) to master it.", owed)
            : L10n.F("{0} more casts to rank up.", owed);
    }

    public static StyleBoxFlat Panel() => new()
    {
        BgColor = new Color(0.07f, 0.08f, 0.10f, 0.97f),
        BorderColor = new Color(0.25f, 0.28f, 0.34f),
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
    };
}
