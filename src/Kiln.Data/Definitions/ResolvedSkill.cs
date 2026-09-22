using Kiln.Core.Foundation;
using Kiln.Core.Progression;

namespace Kiln.Data.Definitions;

/// <summary>
/// A skill's effective values at a given mastery rank and level of investment.
/// <para>
/// Resolved in one place so nothing casts a skill using its base numbers by accident. Ranks
/// are cumulative: Perfect inherits whatever Master and Grand Master changed and overrides
/// only what it names, so a rank definition lists the difference rather than restating the
/// whole skill.
/// </para>
/// </summary>
public readonly record struct ResolvedSkill(
    string Id,
    SkillTargeting Targeting,
    double Radius,
    double ConeAngle,
    double Cooldown,
    double ManaCost,
    double DamageCoef,
    int Hits,
    double Duration,
    double Magnitude,
    MasteryRank Rank,
    int Points,
    StatusApplicationDef? Applies = null)
{
    /// <summary>
    /// What each point beyond the first adds, as a fraction (REF-03).
    /// <para>
    /// Six points of growth at 6% each is a little over a third more by the time a skill is
    /// mastered — enough that a player who commits to one skill feels it against one they
    /// dabbled in, and not so much that the skills they skipped stop being usable.
    /// </para>
    /// </summary>
    public const double PerPoint = 0.06;

    /// <summary>The multiplier this many points is worth. One point is the base skill.</summary>
    public static double PointScale(int points) => 1 + (Math.Max(1, points) - 1) * PerPoint;

    public static ResolvedSkill For(SkillDef def, MasteryRank rank, int points = 1)
    {
        var radius = def.Radius;
        var cone = def.ConeAngle;
        var cooldown = def.Cooldown;
        var mana = def.ManaCost;
        var damage = def.DamageCoef;
        var hits = Math.Max(1, def.Hits);
        var duration = def.Duration;
        var magnitude = def.Magnitude;

        void Apply(SkillRankDef? tier)
        {
            if (tier is null) return;

            radius = tier.Radius ?? radius;
            cone = tier.ConeAngle ?? cone;
            cooldown = tier.Cooldown ?? cooldown;
            mana = tier.ManaCost ?? mana;
            damage = tier.DamageCoef ?? damage;
            hits = tier.Hits ?? hits;
            duration = tier.Duration ?? duration;
            magnitude = tier.Magnitude ?? magnitude;
        }

        if (def.Mastery is { } mastery)
        {
            if (rank >= MasteryRank.Master) Apply(mastery.Master);
            if (rank >= MasteryRank.GrandMaster) Apply(mastery.GrandMaster);
            if (rank >= MasteryRank.Perfect) Apply(mastery.Perfect);
        }

        // Points scale what the skill is for: its damage, or — when it deals none — how long
        // its effect lasts. Never its magnitude: a guard that grew from 70% to 95% mitigation
        // by investment would end the difficulty curve, and a fraction is the one number that
        // cannot be scaled safely without knowing what it means.
        var scale = PointScale(points);

        if (damage > 0) damage *= scale;
        else duration *= scale;

        return new ResolvedSkill(def.Id, def.Targeting, radius, cone, cooldown, mana, damage, hits,
            duration, magnitude, rank, Math.Max(1, points), def.Applies);
    }
}
