using Kiln.Core.Combat;
using Kiln.Core.Foundation;

namespace Kiln.Core.Items;

/// <summary>What a bonus line or a socket stone actually changes.</summary>
public enum BonusStatKind
{
    DamagePct,
    SkillDamagePct,
    CritChance,
    CritDamage,
    PierceChance,
    Evasion,
    MaxHpFlat,
    MaxManaFlat,
    DefenseFlat,
    AttackPowerFlat,
    HpRegen,
    AttackSpeed,
    MoveSpeedPct,

    /// <summary>Damage against one monster family — the build lever kept from the original.</summary>
    VsFamily,

    /// <summary>Resistance to one element.</summary>
    ResistElement,

    /// <summary>Resistance to every element at once.</summary>
    ResistAll,
}

/// <summary>
/// The bridge between the string a designer writes in a bonus pool and the typed field it
/// lands on in <see cref="StatModifiers"/>.
/// <para>
/// It exists so a typo fails loudly at content-validation time instead of quietly applying
/// nothing. A bonus line that silently does zero is nearly impossible to notice in play —
/// the item still reads as an upgrade — so strictness here pays for itself many times over.
/// </para>
/// <para>
/// Magnitudes are written in the units a designer thinks in: whole percent for percentage
/// stats (12 means +12%) and raw points for flat ones. <see cref="Apply"/> converts.
/// </para>
/// </summary>
public readonly record struct BonusStat(BonusStatKind Kind, MonsterFamily Family = default, DamageElement Element = default)
{
    /// <summary>Percent stats are stored as fractions internally, so 12 in data becomes 0.12.</summary>
    public bool IsPercent => Kind is BonusStatKind.DamagePct
        or BonusStatKind.SkillDamagePct
        or BonusStatKind.CritChance
        or BonusStatKind.CritDamage
        or BonusStatKind.PierceChance
        or BonusStatKind.Evasion
        or BonusStatKind.MoveSpeedPct
        or BonusStatKind.VsFamily
        or BonusStatKind.ResistElement
        or BonusStatKind.ResistAll;

    public static bool TryParse(string key, out BonusStat stat, out string? error)
    {
        stat = default;
        error = null;

        if (string.IsNullOrWhiteSpace(key))
        {
            error = "stat key is empty";
            return false;
        }

        // Compound keys name their subject after a dot: vs_family.undead, resist.fire.
        var dot = key.IndexOf('.');

        if (dot < 0)
        {
            var simple = key switch
            {
                "damage_pct" => BonusStatKind.DamagePct,
                "skill_damage_pct" => BonusStatKind.SkillDamagePct,
                "crit_chance" => BonusStatKind.CritChance,
                "crit_damage" => BonusStatKind.CritDamage,
                "pierce_chance" => BonusStatKind.PierceChance,
                "evasion" => BonusStatKind.Evasion,
                "max_hp_flat" => BonusStatKind.MaxHpFlat,
                "max_mana_flat" => BonusStatKind.MaxManaFlat,
                "defense_flat" => BonusStatKind.DefenseFlat,
                "attack_power_flat" => BonusStatKind.AttackPowerFlat,
                "hp_regen" => BonusStatKind.HpRegen,
                "attack_speed" => BonusStatKind.AttackSpeed,
                "move_speed_pct" => BonusStatKind.MoveSpeedPct,
                "resist_all" => BonusStatKind.ResistAll,
                _ => (BonusStatKind?)null,
            };

            if (simple is null)
            {
                error = $"unknown stat '{key}'";
                return false;
            }

            stat = new BonusStat(simple.Value);
            return true;
        }

        var prefix = key[..dot];
        var subject = key[(dot + 1)..];

        switch (prefix)
        {
            case "vs_family":
                if (!TryParseEnum<MonsterFamily>(subject, out var family))
                {
                    error = $"unknown monster family '{subject}' in '{key}'";
                    return false;
                }

                stat = new BonusStat(BonusStatKind.VsFamily, Family: family);
                return true;

            case "resist":
                if (!TryParseEnum<DamageElement>(subject, out var element))
                {
                    error = $"unknown element '{subject}' in '{key}'";
                    return false;
                }

                stat = new BonusStat(BonusStatKind.ResistElement, Element: element);
                return true;

            default:
                error = $"unknown stat group '{prefix}' in '{key}'";
                return false;
        }
    }

    public static BonusStat Parse(string key) =>
        TryParse(key, out var stat, out var error) ? stat : throw new ArgumentException(error, nameof(key));

    private static bool TryParseEnum<T>(string text, out T value) where T : struct, Enum =>
        Enum.TryParse(text, ignoreCase: true, out value) && Enum.IsDefined(value);

    /// <summary>The data-file spelling. Round-trips with <see cref="TryParse"/>.</summary>
    public string Key => Kind switch
    {
        BonusStatKind.VsFamily => "vs_family." + Family.ToString().ToLowerInvariant(),
        BonusStatKind.ResistElement => "resist." + Element.ToString().ToLowerInvariant(),
        _ => ToSnake(Kind.ToString()),
    };

    private static string ToSnake(string name)
    {
        var chars = new List<char>(name.Length + 4);

        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0) chars.Add('_');
            chars.Add(char.ToLowerInvariant(name[i]));
        }

        return new string([.. chars]);
    }

    /// <summary>Adds this line's contribution to a modifier set.</summary>
    public void Apply(StatModifiers mods, double magnitude)
    {
        var value = IsPercent ? magnitude / 100.0 : magnitude;

        switch (Kind)
        {
            case BonusStatKind.DamagePct: mods.DamagePct += value; break;
            case BonusStatKind.SkillDamagePct: mods.SkillDamagePct += value; break;
            case BonusStatKind.CritChance: mods.CritChance += value; break;
            case BonusStatKind.CritDamage: mods.CritDamage += value; break;
            case BonusStatKind.PierceChance: mods.PierceChance += value; break;
            case BonusStatKind.Evasion: mods.Evasion += value; break;
            case BonusStatKind.MaxHpFlat: mods.MaxHpFlat += value; break;
            case BonusStatKind.MaxManaFlat: mods.MaxManaFlat += value; break;
            case BonusStatKind.DefenseFlat: mods.DefenseFlat += value; break;
            case BonusStatKind.AttackPowerFlat: mods.AttackPowerFlat += value; break;
            case BonusStatKind.HpRegen: mods.HpRegenFlat += value; break;
            case BonusStatKind.AttackSpeed: mods.AttackSpeedFlat += value; break;
            case BonusStatKind.MoveSpeedPct: mods.MoveSpeedPct += value; break;

            case BonusStatKind.VsFamily:
                mods.VsFamily[Family] = mods.VsFamilyBonus(Family) + value;
                break;

            case BonusStatKind.ResistElement:
                mods.Resist[Element] = mods.ResistTo(Element) + value;
                break;

            case BonusStatKind.ResistAll:
                foreach (var each in Enum.GetValues<DamageElement>())
                {
                    mods.Resist[each] = mods.ResistTo(each) + value;
                }

                break;
        }
    }

    /// <summary>
    /// Plain-language description for tooltips (ITM-09, FR-5.9). Deliberately says what the
    /// number does rather than naming the stat: "+12% damage", not "DamagePct 0.12".
    /// </summary>
    public string Describe(double magnitude)
    {
        var n = magnitude.ToString(magnitude % 1 == 0 ? "0" : "0.#");

        return Kind switch
        {
            BonusStatKind.DamagePct => $"+{n}% damage",
            BonusStatKind.SkillDamagePct => $"+{n}% skill damage",
            BonusStatKind.CritChance => $"+{n}% critical hit chance",
            BonusStatKind.CritDamage => $"+{n}% critical damage",
            BonusStatKind.PierceChance => $"+{n}% chance to ignore armour",
            BonusStatKind.Evasion => $"+{n}% chance to evade",
            BonusStatKind.MaxHpFlat => $"+{n} maximum health",
            BonusStatKind.MaxManaFlat => $"+{n} maximum mana",
            BonusStatKind.DefenseFlat => $"+{n} defence",
            BonusStatKind.AttackPowerFlat => $"+{n} attack power",
            BonusStatKind.HpRegen => $"+{n} health regenerated per second",
            BonusStatKind.AttackSpeed => $"+{n} attack speed",
            BonusStatKind.MoveSpeedPct => $"+{n}% movement speed",
            BonusStatKind.VsFamily => $"+{n}% damage against {Family.ToString().ToLowerInvariant()}s",
            BonusStatKind.ResistElement => $"+{n}% {Element.ToString().ToLowerInvariant()} resistance",
            BonusStatKind.ResistAll => $"+{n}% resistance to all elements",
            _ => $"+{n} {Key}",
        };
    }
}
