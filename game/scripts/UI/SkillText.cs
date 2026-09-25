using System.Collections.Generic;
using System.Linq;
using Godot;
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
    /// <summary>The Warrior's skills in the order they become available.</summary>
    public static IEnumerable<SkillDef> WarriorSkills() =>
        !GameContent.IsLoaded
            ? []
            : GameContent.Database.Skills.Values
                .Where(s => s.Class == CharacterClass.Warrior)
                .OrderBy(s => s.SuggestedLevel)
                .ThenBy(s => s.Id, System.StringComparer.Ordinal);

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
                ? L10n.F("{0:P0} weapon damage, {1} times", skill.DamageCoef, skill.Hits)
                : L10n.F("{0:P0} weapon damage", skill.DamageCoef));
        }

        lines.Add(Shape(skill));

        if (Effect(skill) is { Length: > 0 } effect) lines.Add(effect);

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
            parts.Add(L10n.F("+{0:P0} damage", skill.Magnitude));
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
        SkillTargeting.SingleTarget => L10n.T("One target."),
        SkillTargeting.Cone => L10n.F("Everything in a {0:0}° arc {1:0.#} m in front of you.", skill.ConeAngle, skill.Radius),
        SkillTargeting.SelfAoe => L10n.F("Everything within {0:0.#} m of you.", skill.Radius),
        SkillTargeting.GroundAoe => L10n.F("Everything within {0:0.#} m of where you aim.", skill.Radius),
        _ => L10n.F("Everything within {0:0.#} m.", skill.Radius),
    };

    private static string Effect(ResolvedSkill skill)
    {
        if (skill.Applies is not { } applies) return "";

        // The self-buffs take their strength from the resolved skill, so investment reaches
        // them; everything else applies exactly what the data says.
        var magnitude = applies.Kind is "fortify" or "empower" ? skill.Magnitude : applies.Magnitude;
        var duration = applies.Kind is "fortify" or "empower" ? skill.Duration : applies.Duration;

        var body = applies.Kind switch
        {
            "fortify" => L10n.F("Take {0:P0} less damage for {1:0.#} s.", magnitude, duration),
            "empower" => L10n.F("Deal {0:P0} more damage for {1:0.#} s.", magnitude, duration),
            "stun" => L10n.F("Stuns for {0:0.#} s.", duration),
            "vulnerability" => L10n.F("The target takes {0:P0} more damage for {1:0.#} s.", magnitude, duration),
            "slow" => L10n.F("Slows by {0:P0} for {1:0.#} s.", magnitude, duration),
            "weaken" => L10n.F("The target deals {0:P0} less damage for {1:0.#} s.", magnitude, duration),
            "poison" => L10n.F("Poisons for {0:0.#} s.", duration),
            "bleed" => L10n.F("Makes the target bleed for {0:0.#} s.", duration),
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
