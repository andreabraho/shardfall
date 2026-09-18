using System.Collections.Generic;
using Godot;

namespace Kiln.Game.Combat;

/// <summary>
/// Floating damage numbers and hit-stop (CBT-12).
/// <para>
/// With placeholder capsules and no animation, essentially all of the "weight" of a hit has
/// to come from feedback like this — the number popping, the freeze on impact, the flash.
/// It is not polish here, it is the only thing making a hit read as a hit.
/// </para>
/// </summary>
public partial class CombatFeedback : Node3D
{
    private static CombatFeedback? _instance;

    private readonly List<Label3D> _pool = [];
    private readonly List<(Label3D Label, double Elapsed, Vector3 Origin, Vector3 Drift)> _active = [];

    private double _hitStopRemaining;

    /// <summary>How long a number lives.</summary>
    [Export] public double Lifetime { get; set; } = 0.6;

    /// <summary>
    /// World size per font pixel. This is the knob that actually controls how big numbers
    /// look — font size alone does not, because Label3D multiplies the two.
    /// </summary>
    [Export] public float PixelSize { get; set; } = 0.0075f;

    [Export] public int NormalFontSize { get; set; } = 32;
    [Export] public int CritFontSize { get; set; } = 42;
    [Export] public int OutlineSize { get; set; } = 5;

    /// <summary>How far a number drifts upward before expiring.</summary>
    [Export] public float RiseHeight { get; set; } = 1.1f;

    /// <summary>Sideways scatter, so a fast chain does not stack into an unreadable column.</summary>
    [Export] public float Scatter { get; set; } = 0.45f;

    /// <summary>
    /// Hard cap on numbers on screen at once. Beyond this the oldest is recycled: past a
    /// dozen the numbers stop being information and become an obstacle.
    /// </summary>
    [Export] public int MaxActive { get; set; } = 14;

    [Export] public Color NormalColor { get; set; } = new(1f, 0.95f, 0.85f);
    [Export] public Color CritColor { get; set; } = new(1f, 0.72f, 0.25f);
    [Export] public Color PlayerHurtColor { get; set; } = new(1f, 0.4f, 0.4f);

    public override void _Ready()
    {
        _instance = this;
        ProcessMode = ProcessModeEnum.Always; // must keep running during hit-stop
    }

    public override void _ExitTree()
    {
        if (_instance == this) _instance = null;

        // Safety: this node owns the only code that restores TimeScale. If it is freed
        // mid-hit-stop (scene change, reload) the game would stay frozen forever.
        if (Engine.TimeScale < 1.0) Engine.TimeScale = 1.0;
    }

    public static void Number(Vector3 worldPosition, int amount, bool critical, bool evaded, bool onPlayer = false)
        => _instance?.Spawn(worldPosition, amount, critical, evaded, onPlayer);

    /// <summary>
    /// Freezes time briefly on impact. Scaled by significance: a crit bites harder than a
    /// chip hit, which is what makes big hits feel big without any animation work.
    /// </summary>
    public static void HitStop(double seconds) => _instance?.BeginHitStop(seconds);

    private void BeginHitStop(double seconds)
    {
        // Never extend an ongoing freeze — overlapping hits would compound into a stutter.
        _hitStopRemaining = Math.Max(_hitStopRemaining, Math.Clamp(seconds, 0, 0.12));
        Engine.TimeScale = 0.0001;
    }

    private void Spawn(Vector3 worldPosition, int amount, bool critical, bool evaded, bool onPlayer)
    {
        // Recycle the oldest rather than letting numbers pile up over the fight.
        while (_active.Count >= MaxActive)
        {
            var (oldest, _, _, _) = _active[0];
            oldest.Visible = false;
            _pool.Add(oldest);
            _active.RemoveAt(0);
        }

        var label = Rent();

        label.Text = evaded ? "miss" : amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        label.Modulate = evaded
            ? new Color(0.75f, 0.78f, 0.82f)
            : onPlayer ? PlayerHurtColor : critical ? CritColor : NormalColor;

        label.PixelSize = PixelSize;
        label.OutlineSize = OutlineSize;
        label.FontSize = critical ? CritFontSize : NormalFontSize;
        label.GlobalPosition = worldPosition;
        label.Visible = true;

        var drift = new Vector3(
            (float)GD.RandRange(-Scatter, Scatter),
            RiseHeight * (float)GD.RandRange(0.85, 1.15),
            (float)GD.RandRange(-Scatter, Scatter));

        _active.Add((label, 0, worldPosition, drift));
    }

    private Label3D Rent()
    {
        if (_pool.Count > 0)
        {
            var reused = _pool[^1];
            _pool.RemoveAt(_pool.Count - 1);
            return reused;
        }

        var label = new Label3D
        {
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            // FixedSize would hold the same screen size at every zoom, so numbers grow
            // relatively larger the further you pull the camera out and swamp the view.
            // Scaling with distance keeps them part of the scene.
            FixedSize = false,
            OutlineModulate = new Color(0, 0, 0, 0.85f),
            RenderPriority = 10,
        };

        AddChild(label);
        return label;
    }

    public override void _Process(double delta)
    {
        // Hit-stop runs on unscaled time, or the freeze would never end.
        if (_hitStopRemaining > 0)
        {
            _hitStopRemaining -= delta / Math.Max(Engine.TimeScale, 0.0001);

            if (_hitStopRemaining <= 0)
            {
                Engine.TimeScale = 1.0;
            }
        }

        if (_active.Count == 0) return;

        var unscaled = delta / Math.Max(Engine.TimeScale, 0.0001);

        for (var i = _active.Count - 1; i >= 0; i--)
        {
            var (label, elapsed, origin, drift) = _active[i];
            elapsed += unscaled;

            if (elapsed >= Lifetime)
            {
                label.Visible = false;
                _pool.Add(label);
                _active.RemoveAt(i);
                continue;
            }

            var t = (float)(elapsed / Lifetime);

            // Rise fast, slow near the top, fade out over the last third.
            var eased = 1f - Mathf.Pow(1f - t, 2f);
            label.GlobalPosition = origin + (drift * eased);
            label.Modulate = label.Modulate with { A = t < 0.66f ? 1f : 1f - ((t - 0.66f) / 0.34f) };

            _active[i] = (label, elapsed, origin, drift);
        }
    }
}
