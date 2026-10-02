using Kiln.Core.Foundation;

namespace Kiln.Core.Combat;

/// <summary>
/// Everything gear, bonus lines, sockets and buffs contribute on top of attributes.
/// <para>
/// Typed fields rather than a string-keyed bag: the JSON bonus pools (doc 06 §5) map onto
/// these in Phase 4, and a typo in a data file should fail at load with a named error
/// instead of silently applying nothing.
/// </para>
/// </summary>
public sealed class StatModifiers
{
    /// <summary>Percent, as a fraction: 0.12 = +12% damage.</summary>
    /// <summary>Average damage: a share more on every basic attack, and nothing on a skill.</summary>
    public double DamagePct { get; set; }

    public double SkillDamagePct { get; set; }
    public double CritChance { get; set; }
    public double CritDamage { get; set; }
    public double PierceChance { get; set; }
    public double Evasion { get; set; }

    public double MaxHpFlat { get; set; }
    public double MaxManaFlat { get; set; }
    public double DefenseFlat { get; set; }
    public double AttackPowerFlat { get; set; }
    public double HpRegenFlat { get; set; }
    public double AttackSpeedFlat { get; set; }
    public double MoveSpeedPct { get; set; }

    /// <summary>Per cent of maximum health healed every few seconds after a kill (see KillRegen).</summary>
    public double KillRegenPct { get; set; }

    /// <summary>Points of each attribute worn (2026-10-02): a bracelet's or an earring's own stat.</summary>
    public double Str { get; set; }
    public double Dex { get; set; }
    public double Int { get; set; }
    public double Vit { get; set; }

    /// <summary>Damage bonus against a monster family — the build lever kept from the original (doc 01 §2.6).</summary>
    public Dictionary<MonsterFamily, double> VsFamily { get; } = [];

    public Dictionary<DamageElement, double> Resist { get; } = [];

    public double VsFamilyBonus(MonsterFamily family) =>
        VsFamily.TryGetValue(family, out var value) ? value : 0;

    public double ResistTo(DamageElement element) =>
        Resist.TryGetValue(element, out var value) ? value : 0;

    public void Add(StatModifiers other)
    {
        DamagePct += other.DamagePct;
        SkillDamagePct += other.SkillDamagePct;
        CritChance += other.CritChance;
        CritDamage += other.CritDamage;
        PierceChance += other.PierceChance;
        Evasion += other.Evasion;
        MaxHpFlat += other.MaxHpFlat;
        MaxManaFlat += other.MaxManaFlat;
        DefenseFlat += other.DefenseFlat;
        AttackPowerFlat += other.AttackPowerFlat;
        HpRegenFlat += other.HpRegenFlat;
        AttackSpeedFlat += other.AttackSpeedFlat;
        MoveSpeedPct += other.MoveSpeedPct;
        KillRegenPct += other.KillRegenPct;
        Str += other.Str;
        Dex += other.Dex;
        Int += other.Int;
        Vit += other.Vit;

        foreach (var (family, value) in other.VsFamily)
        {
            VsFamily[family] = VsFamilyBonus(family) + value;
        }

        foreach (var (element, value) in other.Resist)
        {
            Resist[element] = ResistTo(element) + value;
        }
    }

    /// <summary>`attributes` with the points worn added.</summary>
    public Attributes Raise(Attributes attributes) => new(
        attributes.Str + (int)Math.Round(Str),
        attributes.Dex + (int)Math.Round(Dex),
        attributes.Int + (int)Math.Round(Int),
        attributes.Vit + (int)Math.Round(Vit));

    public StatModifiers Clone()
    {
        var copy = new StatModifiers();
        copy.Add(this);
        return copy;
    }
}
