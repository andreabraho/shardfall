namespace Kiln.Core.World;

/// <summary>
/// How a map's surrounding mountains are shaped (2026-09-27, at your call): the numbers that
/// make wooded hills of one ring and snowy peaks or red mesas of another.
/// </summary>
/// <param name="Low">The height of the lowest stretch of the ridge, in metres.</param>
/// <param name="High">The height of the highest.</param>
/// <param name="Rise">How far in from the foot the slope takes to reach two thirds of its height. Smaller is steeper.</param>
/// <param name="Terrace">The height of one step, for mesas cut in ledges; 0 for a natural slope.</param>
/// <param name="Seed">Which mountains: the same seed gives the same skyline on every load.</param>
public sealed record BorderShape(double Low, double High, double Rise, double Terrace, int Seed);

/// <summary>
/// The mountains that close a map in (2026-09-27, at your call), as a height at every point.
/// </summary>
/// <remarks>
/// The playable ground is a square round the origin. The mountains rise from a foot line that
/// follows that square with its corners rounded — pushed out wherever something on the map
/// stands close to the edge, so no camp or gate is ever buried — and stay high all the way
/// out, so no view from inside finds the end of the world.
/// <para>
/// Each way out of the map is a pass: a valley cut through the first range along the line
/// from the middle of the map through the gate, its floor climbing gently away and a second
/// range closing it again further on, so the road visibly goes somewhere.
/// </para>
/// <para>
/// Pure arithmetic, deliberately. The game builds the mesh, the trees on the slopes and the
/// wall at the foot from this one function, and a test can ask it whether a gate is open.
/// </para>
/// </remarks>
public sealed class BorderTerrain
{
    /// <summary>How square the foot line is: higher keeps the corners fuller.</summary>
    private const double Squareness = 12;

    /// <summary>How many samples the foot line is held as, round the whole ring.</summary>
    public const int FootSamples = 720;

    /// <summary>The height at which the slope stops being walkable ground and the wall stands.</summary>
    public const double FenceHeight = 1.2;

    /// <summary>How far past a gate its pass is closed off, in metres along the pass.</summary>
    public const double PastGate = 7;

    private readonly BorderShape _shape;
    private readonly double[] _foot;
    private readonly (double X, double Z)[] _passes;
    private readonly double _seedX;
    private readonly double _seedZ;

    /// <param name="shape">The mountains' proportions.</param>
    /// <param name="inset">Where the foot line stands, measured along an axis from the middle.</param>
    /// <param name="keepClear">Points on the map the mountains must stay clear of, each with its clearance.</param>
    /// <param name="passes">The gates out of the map; each gets a pass through the mountains.</param>
    public BorderTerrain(
        BorderShape shape, double inset,
        IEnumerable<(double X, double Z, double Clearance)> keepClear,
        IEnumerable<(double X, double Z)> passes)
    {
        _shape = shape;
        _passes = passes.ToArray();

        var rng = new Foundation.DeterministicRng((ulong)(uint)shape.Seed).Fork("border");
        _seedX = rng.NextDouble(0, 1000);
        _seedZ = rng.NextDouble(0, 1000);

        _foot = new double[FootSamples];

        // The line itself wanders a few metres in and out, so it never reads as ruled.
        for (var i = 0; i < FootSamples; i++)
        {
            var angle = i * Math.Tau / FootSamples;
            _foot[i] = inset + (ValueNoise.Fbm(_seedX + (Math.Cos(angle) * 3), _seedZ + (Math.Sin(angle) * 3), 3) * 5) - 1;
        }

        // Anything near the edge pushes the line out round itself: a bump shaped like a cone,
        // highest straight behind the point and falling off to either side.
        foreach (var (x, z, clearance) in keepClear)
        {
            var r = Rounded(x, z);
            var at = Math.Atan2(z, x);
            var reach = r + clearance;

            for (var i = 0; i < FootSamples; i++)
            {
                var sideways = Math.Abs(AngleBetween(i * Math.Tau / FootSamples, at)) * r;
                var needed = reach - (sideways * 0.6);

                if (needed > _foot[i]) _foot[i] = needed;
            }
        }
    }

    /// <summary>The mountains' height at a point on the ground, in metres. Zero on the map itself.</summary>
    public double Height(double x, double z)
    {
        var angle = Math.Atan2(z, x);
        var depth = Rounded(x, z) - FootAt(angle);

        var h = 0.0;

        if (depth > 0)
        {
            // Along the ring the ridge swells and sags between its low and high stretches.
            var swell = ValueNoise.Fbm(_seedX + 40 + (Math.Cos(angle) * 2.2), _seedZ + (Math.Sin(angle) * 2.2), 3);
            var peak = _shape.Low + ((_shape.High - _shape.Low) * Clamp01(swell * 1.1));

            var rise = 1 - Math.Exp(-depth / Math.Max(1, _shape.Rise));

            // Crags: ridged noise breaks the ridge into peaks and saddles.
            var crag = ValueNoise.Ridged((x / 34) + _seedX, (z / 34) + _seedZ, 4);

            h = peak * rise * (0.62 + (0.62 * crag));
            h += ValueNoise.Fbm((x / 9) + _seedZ, (z / 9) + _seedX, 2) * 2.4 * Math.Min(1, depth / 8);

            if (_shape.Terrace > 0) h = Terraced(h, _shape.Terrace);
        }

        foreach (var (gx, gz) in _passes)
        {
            h = Pass(x, z, gx, gz, h);
        }

        return Math.Max(0, h);
    }

    /// <summary>
    /// The foot of the wall round the map: for each of <paramref name="count"/> directions out
    /// of the middle, the first point where the ground has risen past <see cref="FenceHeight"/>
    /// — or, down a pass, the point just behind its gate — never further out than
    /// <paramref name="limit"/> along either axis, where the walkable ground ends.
    /// </summary>
    public IReadOnlyList<(double X, double Z)> Fence(double limit, int count = 360)
    {
        var line = new List<(double X, double Z)>(count);

        for (var i = 0; i < count; i++)
        {
            var angle = i * Math.Tau / count;
            var (dx, dz) = (Math.Cos(angle), Math.Sin(angle));
            var r = 30.0;

            while (r < 400)
            {
                var (x, z) = (dx * r, dz * r);

                if (Height(x, z) >= FenceHeight || BehindAGate(x, z)) break;
                if (Math.Max(Math.Abs(x), Math.Abs(z)) >= limit) break;

                r += 0.5;
            }

            line.Add((dx * r, dz * r));
        }

        return line;
    }

    /// <summary>The foot line's distance from the middle, measured the rounded-square way, in a direction.</summary>
    public double FootAt(double angle)
    {
        var at = ((angle % Math.Tau) + Math.Tau) % Math.Tau * FootSamples / Math.Tau;
        var i = (int)Math.Floor(at);
        var t = at - i;

        return (_foot[i % FootSamples] * (1 - t)) + (_foot[(i + 1) % FootSamples] * t);
    }

    /// <summary>Distance from the middle measured so that a square is a circle: the foot line's own measure.</summary>
    public static double Rounded(double x, double z) =>
        Math.Pow(Math.Pow(Math.Abs(x), Squareness) + Math.Pow(Math.Abs(z), Squareness), 1 / Squareness);

    /// <summary>How far down a pass a point is, and how far to the side of its road.</summary>
    private static (double Along, double Across, double GateAt) PassFrame(double x, double z, double gx, double gz)
    {
        var gate = Math.Sqrt((gx * gx) + (gz * gz));
        var (ux, uz) = (gx / gate, gz / gate);
        var along = (x * ux) + (z * uz);
        var across = Math.Abs((x * uz) - (z * ux));

        return (along, across, gate);
    }

    /// <summary>Whether a point lies on the road down a pass, within <paramref name="margin"/> of its middle.</summary>
    public bool OnAPassRoad(double x, double z, double margin)
    {
        foreach (var (gx, gz) in _passes)
        {
            var (along, across, gate) = PassFrame(x, z, gx, gz);

            if (along > gate - 14 && across < margin) return true;
        }

        return false;
    }

    private bool BehindAGate(double x, double z)
    {
        foreach (var (gx, gz) in _passes)
        {
            var (along, across, gate) = PassFrame(x, z, gx, gz);

            if (along >= gate + PastGate && across < 40) return true;
        }

        return false;
    }

    /// <summary>
    /// Cuts a pass through the mountains behind a gate: a valley floor a dozen metres wide,
    /// flaring as it goes, walls rising either side, and the next range closing it far off.
    /// </summary>
    private static double Pass(double x, double z, double gx, double gz, double h)
    {
        var (along, across, gate) = PassFrame(x, z, gx, gz);

        if (along < gate - 14) return h;

        var floorHalf = 6 + (Math.Max(0, along - gate) * 0.08);
        var open = SmoothStep(floorHalf, floorHalf + 18, across);
        var closed = SmoothStep(gate + 55, gate + 110, along);
        var into = SmoothStep(gate - 14, gate, along);

        var keep = 1 - (into * (1 - Math.Max(open, closed)));
        var road = Math.Max(0, along - (gate + 4)) * 0.12 * (1 - open);

        return (h * keep) + road;
    }

    /// <summary>Ledges: flat treads and steep risers, the way a mesa weathers.</summary>
    private static double Terraced(double h, double step)
    {
        var k = h / step;
        var tread = Math.Floor(k);

        return step * (tread + SmoothStep(0.72, 1, k - tread));
    }

    private static double AngleBetween(double a, double b)
    {
        var d = (a - b) % Math.Tau;

        if (d > Math.PI) d -= Math.Tau;
        if (d < -Math.PI) d += Math.Tau;

        return d;
    }

    private static double Clamp01(double v) => Math.Clamp(v, 0, 1);

    public static double SmoothStep(double from, double to, double v)
    {
        var t = Clamp01((v - from) / (to - from));

        return t * t * (3 - (2 * t));
    }
}

/// <summary>
/// Smooth value noise, from a hash rather than a table so it needs no setup and is the same
/// everywhere. In [0, 1].
/// </summary>
public static class ValueNoise
{
    public static double Value(double x, double z)
    {
        var ix = Math.Floor(x);
        var iz = Math.Floor(z);
        var fx = x - ix;
        var fz = z - iz;

        var ux = fx * fx * (3 - (2 * fx));
        var uz = fz * fz * (3 - (2 * fz));

        var a = Hash((long)ix, (long)iz);
        var b = Hash((long)ix + 1, (long)iz);
        var c = Hash((long)ix, (long)iz + 1);
        var d = Hash((long)ix + 1, (long)iz + 1);

        return a + ((b - a) * ux) + ((c - a) * uz) + ((a - b - c + d) * ux * uz);
    }

    /// <summary>Several octaves, each twice as fine and half as strong. In [0, 1].</summary>
    public static double Fbm(double x, double z, int octaves)
    {
        double sum = 0, weight = 1, total = 0, scale = 1;

        for (var i = 0; i < octaves; i++)
        {
            sum += Value(x * scale, z * scale) * weight;
            total += weight;
            weight *= 0.5;
            scale *= 2.03;
        }

        return sum / total;
    }

    /// <summary>Sharp crests where plain noise has its middle: ridges and peaks. In [0, 1].</summary>
    public static double Ridged(double x, double z, int octaves)
    {
        double sum = 0, weight = 1, total = 0, scale = 1;

        for (var i = 0; i < octaves; i++)
        {
            var n = 1 - Math.Abs((Value(x * scale, z * scale) * 2) - 1);
            sum += n * n * weight;
            total += weight;
            weight *= 0.5;
            scale *= 2.03;
        }

        return sum / total;
    }

    /// <summary>A number in [0, 1] for a grid point, the same on every machine.</summary>
    public static double Hash(long x, long z)
    {
        unchecked
        {
            var h = (ulong)((x * 73856093L) ^ (z * 19349663L));
            h ^= h >> 33;
            h *= 0xff51afd7ed558ccdUL;
            h ^= h >> 33;
            h *= 0xc4ceb9fe1a85ec53UL;
            h ^= h >> 33;

            return (h >> 11) * (1.0 / 9007199254740992.0);
        }
    }
}
