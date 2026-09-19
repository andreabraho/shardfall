using Kiln.Core.Foundation;

namespace Kiln.Core.Combat;

/// <summary>
/// Attributes plus gear plus buffs, resolved into the derived stats combat actually uses
/// (doc 06 §2).
/// </summary>
/// <remarks>
/// Every cap here exists to stop a stat becoming a win button. Crit at 50%, pierce at 35%,
/// evasion at 30%, mitigation at 75%: past those points more of the same stat stops paying,
/// which is what keeps several gear directions viable instead of one dominant one.
/// </remarks>
public sealed class StatBlock
{
    public const double CritChanceCap = 0.50;
    public const double PierceChanceCap = 0.35;
    public const double EvasionCap = 0.30;
    public const double ResistCap = 0.70;
    public const double MitigationCap = 0.75;
    public const double AttackSpeedCap = 180;

    public Attributes Attributes { get; set; } = Attributes.Starting;
    public int Level { get; set; } = 1;
    public CharacterClass Class { get; set; } = CharacterClass.Warrior;
    public MonsterFamily Family { get; set; } = MonsterFamily.Human;

    /// <summary>Weapon damage contribution. Rolled between min and max per swing by the caller.</summary>
    public double WeaponDamage { get; set; }

    public double ArmorValue { get; set; }

    public StatModifiers Modifiers { get; set; } = new();

    /// <summary>
    /// Flat overrides used by data-driven enemies, which define hp/attack/defense directly
    /// rather than deriving them from attributes (doc 06 §6).
    /// </summary>
    public double? FlatMaxHp { get; set; }

    public double? FlatAttackPower { get; set; }
    public double? FlatDefense { get; set; }

    public int Primary => Attributes.PrimaryFor(Class, Attributes);

    public double MaxHp => FlatMaxHp
        ?? (120 + (Attributes.Vit * 18.0) + (Level * 14.0) + Modifiers.MaxHpFlat);

    public double MaxMana => 60 + (Attributes.Int * 12.0) + (Level * 6.0) + Modifiers.MaxManaFlat;

    public double HpRegenPerSecond => 0.4 + (Attributes.Vit * 0.05) + Modifiers.HpRegenFlat;

    /// <summary>
    /// Out-of-combat mana regeneration. Partly a share of the pool, so it does not fall
    /// behind as the pool grows — a flat trickle meant a high-level character with a large
    /// mana bar waited proportionally longer than a starting one, which is backwards.
    /// </summary>
    public double ManaRegenPerSecond => 1.2 + (Attributes.Int * 0.10) + (MaxMana * 0.012);

    /// <summary>Regeneration is throttled in combat so potions and pacing matter (doc 06 §2).</summary>
    public double HpRegenInCombat => HpRegenPerSecond * 0.25;

    public double ManaRegenInCombat => ManaRegenPerSecond * 0.30;

    public double AttackPower => FlatAttackPower
        ?? (WeaponDamage + (Primary * 1.8) + (Level * 1.2) + Modifiers.AttackPowerFlat);

    public double MagicPower => WeaponDamage + (Attributes.Int * 2.1) + (Level * 1.2) + Modifiers.AttackPowerFlat;

    public double Defense => FlatDefense
        ?? (ArmorValue + (Attributes.Vit * 0.8) + (Level * 0.6) + Modifiers.DefenseFlat);

    public double CritChance => Math.Clamp(0.03 + (Attributes.Dex * 0.0012) + Modifiers.CritChance, 0, CritChanceCap);

    public double CritDamage => 1.50 + Modifiers.CritDamage;

    public double PierceChance => Math.Clamp((Attributes.Dex * 0.0008) + Modifiers.PierceChance, 0, PierceChanceCap);

    public double Evasion => Math.Clamp((Attributes.Dex * 0.0006) + Modifiers.Evasion, 0, EvasionCap);

    public double AttackSpeed => Math.Min(100 + (Attributes.Dex * 0.5) + Modifiers.AttackSpeedFlat, AttackSpeedCap);

    /// <summary>Attacks per second. 100 attack speed = 1.0/s.</summary>
    public double AttacksPerSecond => AttackSpeed / 100.0;

    public double MoveSpeed => PlayerConstants.BaseMoveSpeed * (1 + Modifiers.MoveSpeedPct);

    /// <summary>
    /// Fraction of incoming damage removed by defence, against an attacker of this level.
    /// <para>
    /// Asymptotic by design: stacking defence always helps a little and never reaches
    /// invulnerability. A linear or subtractive formula would let gear trivialise the game,
    /// which is exactly the gear-check spiral a click-to-move game must avoid (doc 06 §4).
    /// </para>
    /// </summary>
    public double DamageReductionAgainst(int attackerLevel)
    {
        var defense = Math.Max(0, Defense);
        var reduction = defense / (defense + 60 + (14.0 * attackerLevel));
        return Math.Min(reduction, MitigationCap);
    }

    public double ResistTo(DamageElement element) => Math.Min(Modifiers.ResistTo(element), ResistCap);
}
