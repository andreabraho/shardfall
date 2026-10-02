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
    /// The attack power a class draws from its attributes: its own attribute at the full rate,
    /// and for the Ninja a third of that from strength besides.
    /// </summary>
    public static double AttackFrom(CharacterClass characterClass, Attributes attributes) =>
        (PrimaryFor(characterClass, attributes) * AttackPerPoint)
        + (characterClass == CharacterClass.Blade ? attributes.Str * BladeAttackPerStr : 0);
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
