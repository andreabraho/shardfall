using Kiln.Core.Foundation;
using Kiln.Core.Progression;

namespace Kiln.Data.Definitions;

/// <summary>
/// A skill's effective values at a given mastery rank.
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
    double Cooldown,
    double ManaCost,
    double DamageCoef,
    int Hits,
    double Stagger,
    double Duration,
    double Magnitude,
    MasteryRank Rank,
    StatusApplicationDef? Applies = null)
{
    public static ResolvedSkill For(SkillDef def, MasteryRank rank)
    {
        var radius = def.Radius;
        var cooldown = def.Cooldown;
        var mana = def.ManaCost;
        var damage = def.DamageCoef;
        var hits = Math.Max(1, def.Hits);
        var stagger = def.Stagger;
        var duration = def.Duration;
        var magnitude = def.Magnitude;

        void Apply(SkillRankDef? tier)
        {
            if (tier is null) return;

            radius = tier.Radius ?? radius;
            cooldown = tier.Cooldown ?? cooldown;
            mana = tier.ManaCost ?? mana;
            damage = tier.DamageCoef ?? damage;
            hits = tier.Hits ?? hits;
            stagger = tier.Stagger ?? stagger;
            duration = tier.Duration ?? duration;
            magnitude = tier.Magnitude ?? magnitude;
        }

        if (def.Mastery is { } mastery)
        {
            if (rank >= MasteryRank.Master) Apply(mastery.Master);
            if (rank >= MasteryRank.GrandMaster) Apply(mastery.GrandMaster);
            if (rank >= MasteryRank.Perfect) Apply(mastery.Perfect);
        }

        return new ResolvedSkill(def.Id, def.Targeting, radius, cooldown, mana, damage, hits, stagger, duration, magnitude, rank, def.Applies);
    }
}
