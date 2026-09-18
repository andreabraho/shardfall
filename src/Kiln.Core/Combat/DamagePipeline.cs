using Kiln.Core.Foundation;

namespace Kiln.Core.Combat;

/// <summary>One attack, as presented to the damage pipeline.</summary>
public readonly record struct DamageRequest
{
    public required StatBlock Attacker { get; init; }
    public required StatBlock Defender { get; init; }

    /// <summary>Weapon coefficient for the swing (auto-attacks are 1.0).</summary>
    public double WeaponCoef { get; init; } = 1.0;

    /// <summary>Skill coefficient (doc 06 §7). 1.0 for a plain attack.</summary>
    public double SkillCoef { get; init; } = 1.0;

    public DamageElement Element { get; init; } = DamageElement.Physical;

    /// <summary>Flat damage added before percentage bonuses.</summary>
    public double FlatBonus { get; init; }

    /// <summary>
    /// Enemy damage multiplier for the active difficulty (doc 02 §1). Must be 1.0 when the
    /// player is the attacker — difficulty scales what the player *takes*, never what they
    /// deal, or Wanderer would quietly become a damage buff.
    /// </summary>
    public double DifficultyDamageMultiplier { get; init; } = 1.0;

    public bool CanCrit { get; init; } = true;

    /// <summary>Skips the evade roll. Used by unavoidable effects such as shard pulses.</summary>
    public bool Unavoidable { get; init; }

    public DamageRequest() { }
}

/// <summary>
/// The outcome, with the reasons intact so the UI can show "MISS", a crit colour, or a
/// pierce marker, and so tests can assert on the path taken rather than just the number.
/// </summary>
public readonly record struct DamageResult(
    int Amount,
    bool Evaded,
    bool Critical,
    bool Pierced,
    double MitigationApplied)
{
    public static readonly DamageResult Miss = new(0, true, false, false, 0);
}

/// <summary>
/// The damage evaluation order from doc 06 §4. Pure, deterministic given the RNG stream,
/// and unit-tested — every balance decision downstream depends on this being predictable
/// (NFR-R.2).
/// </summary>
public static class DamagePipeline
{
    /// <summary>±5% spread, so equal hits do not look identical without being swingy.</summary>
    public const double VarianceMin = 0.95;
    public const double VarianceMax = 1.05;

    public static DamageResult Resolve(in DamageRequest request, DeterministicRng rng)
    {
        var attacker = request.Attacker;
        var defender = request.Defender;

        // 0. Evade. Not in the original doc draft; added because Evasion is a derived stat
        //    and a stat that exists but does nothing is a bug waiting to be discovered.
        if (!request.Unavoidable && rng.Chance(defender.Evasion))
        {
            return DamageResult.Miss;
        }

        // 1-2. Base and flat.
        var damage = (attacker.AttackPower * request.WeaponCoef * request.SkillCoef) + request.FlatBonus;

        // 3. Percentage bonuses.
        damage *= 1 + attacker.Modifiers.DamagePct
                    + (request.SkillCoef != 1.0 ? attacker.Modifiers.SkillDamagePct : 0);

        // 4. Damage versus the defender's family.
        damage *= 1 + attacker.Modifiers.VsFamilyBonus(defender.Family);

        // 5. Crit.
        var critical = request.CanCrit && rng.Chance(attacker.CritChance);
        if (critical) damage *= attacker.CritDamage;

        // 6-7. Pierce skips mitigation entirely.
        var pierced = rng.Chance(attacker.PierceChance);
        var mitigation = pierced ? 0 : defender.DamageReductionAgainst(attacker.Level);
        damage *= 1 - mitigation;

        // 8. Elemental resistance.
        damage *= 1 - defender.ResistTo(request.Element);

        // 9. Difficulty.
        damage *= request.DifficultyDamageMultiplier;

        // 10. Variance.
        damage *= rng.NextDouble(VarianceMin, VarianceMax);

        // 11. A connecting hit always does something, so a heavily armoured target still
        //     shows feedback rather than a silent zero.
        var amount = Math.Max(1, (int)Math.Round(damage, MidpointRounding.AwayFromZero));

        return new DamageResult(amount, false, critical, pierced, mitigation);
    }

    /// <summary>
    /// Average damage over many rolls, ignoring variance and randomness. Used by the balance
    /// simulator and by tooltips, where a stable number matters more than a sampled one.
    /// </summary>
    public static double ExpectedDamage(in DamageRequest request)
    {
        var attacker = request.Attacker;
        var defender = request.Defender;

        var damage = (attacker.AttackPower * request.WeaponCoef * request.SkillCoef) + request.FlatBonus;

        damage *= 1 + attacker.Modifiers.DamagePct
                    + (request.SkillCoef != 1.0 ? attacker.Modifiers.SkillDamagePct : 0);

        damage *= 1 + attacker.Modifiers.VsFamilyBonus(defender.Family);

        if (request.CanCrit)
        {
            damage *= 1 + (attacker.CritChance * (attacker.CritDamage - 1));
        }

        var mitigation = defender.DamageReductionAgainst(attacker.Level);
        damage *= 1 - (mitigation * (1 - attacker.PierceChance));

        damage *= 1 - defender.ResistTo(request.Element);
        damage *= request.DifficultyDamageMultiplier;

        if (!request.Unavoidable)
        {
            damage *= 1 - defender.Evasion;
        }

        return Math.Max(0, damage);
    }
}
