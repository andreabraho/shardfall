using Godot;
using Kiln.Core.Progression;

namespace Kiln.Game.Visual;

/// <summary>
/// How loudly a skill shows itself, by mastery rank (REF-03).
/// </summary>
/// <remarks>
/// A rank that only changes a number in a tooltip is a rank the player never feels. The skill
/// they have poured seven points and six hundred casts into should not look like the one they
/// bought this morning — so every effect a skill draws, its aura and its flash on the ground,
/// takes its colour and its size from here.
/// <para>
/// The escalation runs warm: the skill's own colour, then amber, then violet, then a
/// white-gold that is nearly blinding. Each rank keeps part of the base colour so a Blade Aura
/// still reads as the Blade Aura at Perfect — the rank changes how loud it is, never what it
/// is.
/// </para>
/// </remarks>
public static class MasteryStyle
{
    private static readonly Color Amber = new(1.00f, 0.72f, 0.28f);
    private static readonly Color Violet = new(0.78f, 0.46f, 1.00f);
    private static readonly Color WhiteGold = new(1.00f, 0.97f, 0.82f);

    /// <summary>The skill's colour at this rank.</summary>
    public static Color Tint(Color baseColour, MasteryRank rank) => rank switch
    {
        MasteryRank.Master => baseColour.Lerp(Amber, 0.45f),
        MasteryRank.GrandMaster => baseColour.Lerp(Violet, 0.55f),
        MasteryRank.Perfect => baseColour.Lerp(WhiteGold, 0.70f),
        _ => baseColour,
    };

    /// <summary>How much brighter an effect burns at this rank.</summary>
    public static float Brightness(MasteryRank rank) => rank switch
    {
        MasteryRank.Master => 1.30f,
        MasteryRank.GrandMaster => 1.65f,
        MasteryRank.Perfect => 2.10f,
        _ => 1.0f,
    };

    /// <summary>How much wider it is drawn. Small: the effect must not lie about its reach.</summary>
    public static float Flourish(MasteryRank rank) => rank switch
    {
        MasteryRank.Master => 1.06f,
        MasteryRank.GrandMaster => 1.12f,
        MasteryRank.Perfect => 1.18f,
        _ => 1.0f,
    };

    /// <summary>Extra embers an aura throws off at this rank.</summary>
    public static int Embers(MasteryRank rank) => rank switch
    {
        MasteryRank.Master => 12,
        MasteryRank.GrandMaster => 26,
        MasteryRank.Perfect => 44,
        _ => 0,
    };
}
