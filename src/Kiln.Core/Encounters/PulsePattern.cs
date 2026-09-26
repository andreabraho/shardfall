namespace Kiln.Core.Encounters;

/// <summary>
/// The shape of a stone's pulse (2026-09-26, at your call): not a full ring to run out of,
/// but a ring broken by safe gaps, so a player caught inside steps sideways into one instead
/// of leaving the fight.
/// </summary>
/// <remarks>
/// Angles are in degrees round the stone, measured as <c>atan2(x, z)</c> — 0 along +Z — the
/// same way the telegraphs are turned. The gaps sit at <see cref="Rotation"/> and evenly round
/// from there; everything between them is struck. A new rotation every pulse, so standing
/// still in last pulse's gap is not a plan.
/// </remarks>
/// <param name="Rotation">Where the first gap is centred.</param>
/// <param name="Gaps">How many safe gaps there are.</param>
/// <param name="GapDegrees">How wide each gap is.</param>
public sealed record PulsePattern(double Rotation, int Gaps, double GapDegrees)
{
    /// <summary>The pattern for a phase: four gaps of 35° at first (two fifths of the ring safe), three of 30° once the stone is angry (a quarter).</summary>
    public static PulsePattern For(ShardPhase phase, double rotation) =>
        phase >= ShardPhase.Two ? new PulsePattern(rotation, 3, 30) : new PulsePattern(rotation, 4, 35);

    /// <summary>Degrees from one gap's centre to the next.</summary>
    public double Period => 360.0 / Math.Max(1, Gaps);

    /// <summary>How wide each struck sector is.</summary>
    public double DangerDegrees => Period - GapDegrees;

    /// <summary>The centres of the struck sectors, halfway between the gaps.</summary>
    public IEnumerable<double> DangerCentres()
    {
        for (var i = 0; i < Gaps; i++) yield return Normalise(Rotation + (i * Period) + (Period / 2));
    }

    /// <summary>Whether something standing at <paramref name="angleDegrees"/> round the stone is struck.</summary>
    public bool Hits(double angleDegrees)
    {
        // Where in its period the angle falls, measured from a gap's centre.
        var within = Normalise(angleDegrees - Rotation) % Period;

        return within > GapDegrees / 2 && within < Period - (GapDegrees / 2);
    }

    private static double Normalise(double degrees) => ((degrees % 360) + 360) % 360;
}
