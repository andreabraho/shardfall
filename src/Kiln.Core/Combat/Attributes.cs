using Kiln.Core.Foundation;

namespace Kiln.Core.Combat;

/// <summary>The four attributes (doc 06 §2). Kept simple and readable on purpose.</summary>
public readonly record struct Attributes(int Str, int Dex, int Int, int Vit)
{
    public const int StartingValue = 6;

    /// <summary>Points granted per level.</summary>
    public const int PointsPerLevel = 3;

    public static readonly Attributes Starting =
        new(StartingValue, StartingValue, StartingValue, StartingValue);

    public int Total => Str + Dex + Int + Vit;

    public static Attributes operator +(Attributes a, Attributes b) =>
        new(a.Str + b.Str, a.Dex + b.Dex, a.Int + b.Int, a.Vit + b.Vit);

    /// <summary>The attribute a class's damage scales from (doc 06 §2).</summary>
    public static int PrimaryFor(CharacterClass characterClass, Attributes attributes) => characterClass switch
    {
        CharacterClass.Warrior => attributes.Str,
        CharacterClass.Blade => attributes.Dex,
        CharacterClass.Sura => attributes.Str,
        CharacterClass.Shaman => attributes.Int,
        CharacterClass.Archer => attributes.Dex,
        _ => attributes.Str,
    };

    /// <summary>Attack power for each point of the attribute a class strikes with.</summary>
    public const double AttackPerPoint = 1.8;

    /// <summary>
    /// Attack power the Ninja takes from each point of strength (2026-10-02, at your call): its
    /// blows come from dexterity, which already gives it crit, pierce, evasion and speed, and
    /// strength adds to them, less.
    /// </summary>
    public const double BladeAttackPerStr = 0.6;

    /// <summary>
    /// Attack power the Mental Warrior's skills, and only its skills, take from each point of
    /// vitality (2026-10-02, at your call): a little, strength staying what it strikes with.
    /// </summary>
    public const double MentalSkillPerVit = 0.6;

    /// <summary>
    /// Attack power the Asura's skills, and only its skills, take from each point of intelligence
    /// (CLS-05, 2026-10-02): the sword strikes with strength, the magic in it with the mind.
    /// </summary>
    public const double AsuraSkillPerInt = 1.2;

    /// <summary>
    /// What each point of intelligence adds to the Dragonseer's blessings (CLS-06, 2026-10-02, at
    /// your call: "all of its skills scale with INT, the critical and the damage reduced too") —
    /// its fire already strikes with intelligence — and where each stops growing.
    /// </summary>
    public const double SeerBlessingPerInt = 0.0012;

    public const double SeerBlessingCap = 0.40;

    public const double SeerEyePerInt = 0.0015;

    public const double SeerEyeCap = 0.35;

    public const double SeerSealPerInt = 0.002;

    public const double SeerSealCap = 0.50;

    /// <summary>
    /// What each point of intelligence adds to the Asura's skills besides their damage (CLS-05,
    /// 2026-10-02, at your call: "all of the Asura's skills scale with INT"): attack power on the
    /// Hexed Blade, and a share on everything that is a share, each up to a cap.
    /// </summary>
    public const double AsuraBladePerInt = 0.6;

    public const double AsuraSharePerInt = 0.001;

    public const double AsuraThornsPerInt = 0.0025;

    public const double AsuraWardPerInt = 0.002;

    public const double AsuraMailCap = 0.40;

    public const double AsuraThornsCap = 0.60;

    public const double AsuraDreadCap = 0.45;

    /// <summary>Defence the Dread gives its caster for each point of intelligence, on top of its own.</summary>
    public const double AsuraGuardPerInt = 0.8;

    public const double AsuraBreakCap = 0.45;

    public const double AsuraWardCap = 0.55;

    /// <summary>A blessing's strength at this much intelligence: its own, and a little for every point, up to its cap.</summary>
    public static double SeerScaled(double strength, int intelligence, double perPoint, double cap) =>
        Math.Min(Math.Max(strength, cap), strength + (Math.Max(0, intelligence) * perPoint));

    /// <summary>
    /// The attack power a class draws from its attributes: its own attribute at the full rate,
    /// and for the Ninja and the Archer a third of that from strength besides.
    /// </summary>
    public static double AttackFrom(CharacterClass characterClass, Attributes attributes) =>
        (PrimaryFor(characterClass, attributes) * AttackPerPoint)
        + (characterClass is CharacterClass.Blade or CharacterClass.Archer ? attributes.Str * BladeAttackPerStr : 0);
}

/// <summary>Damage element, for the resistance step of the pipeline.</summary>
public enum DamageElement
{
    Physical,
    Fire,
    Ice,
    Lightning,
    Dark,
}
