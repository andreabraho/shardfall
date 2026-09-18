using Kiln.Core.Foundation;

namespace Kiln.Core.Combat;

/// <summary>The four attributes (doc 06 §2). Kept simple and readable on purpose.</summary>
public readonly record struct Attributes(int Str, int Dex, int Int, int Vit)
{
    public const int StartingValue = 6;

    /// <summary>Points granted per level.</summary>
    public const int PointsPerLevel = 4;

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
