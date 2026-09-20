using Godot;

namespace Kiln.Game.Visual;

/// <summary>
/// Every entity gets exactly one VisualRoot child, and all of its appearance hangs off it.
/// Gameplay nodes (collision, AI, stats) are siblings, never children of the visual, so
/// swapping placeholder primitives for real models can never disturb gameplay structure.
/// </summary>
/// <remarks>
/// Convention enforced by review: if gameplay code reaches through a VisualRoot to find a
/// node, that is a bug — it will break the moment art lands.
/// </remarks>
[GlobalClass]
public partial class VisualRoot : Node3D
{
    private Node3D? _current;

    /// <summary>The logical visual id from game/data/tables/visuals.json.</summary>
    [Export] public string VisualId { get; set; } = string.Empty;

    /// <summary>Optional per-instance colour override, so one primitive serves many variants.</summary>
    [Export] public string TintOverride { get; set; } = string.Empty;

    [Export] public float VisualScale { get; set; } = 1.0f;

    public override void _Ready()
    {
        _restPosition = Position;

        if (!string.IsNullOrEmpty(VisualId))
        {
            Apply(VisualId, string.IsNullOrEmpty(TintOverride) ? null : TintOverride, VisualScale);
        }
    }

    private Vector3 _restPosition;
    private Vector3 _lungeDirection;
    private double _lunge;
    private ModelAnimator? _animator;
    private double _lungeDuration;
    private float _lungeDistance;

    /// <summary>
    /// A short shove forward and back along <paramref name="direction"/> — the swing.
    /// </summary>
    /// <remarks>
    /// With placeholder capsules and no skeletal animation, a basic attack was completely
    /// invisible against a single enemy: the only feedback was the victim's flash and a
    /// damage number, so the player could not tell an attack from standing still. This is the
    /// cheapest motion that reads as "I swung".
    /// <para>
    /// A model that can animate plays the swing instead, and the shove is dropped rather than
    /// layered on top — a body that both swings and slides forward reads as skating.
    /// </para>
    /// </remarks>
    public void Lunge(Vector3 direction, float distance = 0.35f, double seconds = 0.18)
    {
        var flat = direction with { Y = 0 };

        if (flat.LengthSquared() < 0.0001f) return;

        if (_animator is { Ready: true })
        {
            _animator.Attack();
            return;
        }

        _lungeDirection = flat.Normalized();
        _lungeDistance = distance;
        _lungeDuration = System.Math.Max(0.01, seconds);
        _lunge = _lungeDuration;
    }

    private StandardMaterial3D? _flashMaterial;
    private Color _baseColor;
    private double _flash;

    /// <summary>
    /// Briefly whitens the body on impact.
    /// <para>
    /// Damage numbers say how much; the flash says <em>that</em> — and which of several
    /// overlapping bodies took it. With placeholder capsules and no animation, it is a large
    /// part of what makes a hit land visually.
    /// </para>
    /// </summary>
    public void Flash(double seconds = 0.09)
    {
        if (_current is null) return;

        if (_flashMaterial is null)
        {
            // The registry shares one material per colour, so flashing in place would flash
            // every enemy of that colour. Take a private copy the first time.
            var mesh = _current as MeshInstance3D ?? _current.GetChildOrNull<MeshInstance3D>(0);
            if (mesh?.GetActiveMaterial(0) is not StandardMaterial3D source) return;

            if (source.Duplicate() is not StandardMaterial3D copy) return;

            _flashMaterial = copy;
            _baseColor = copy.AlbedoColor;
            mesh.MaterialOverride = copy;
        }

        _flash = seconds;
    }

    public override void _Process(double delta)
    {
        if (_flash > 0 && _flashMaterial is not null)
        {
            _flash -= delta;

            _flashMaterial.AlbedoColor = _flash > 0
                ? _baseColor.Lerp(Colors.White, 0.75f)
                : _baseColor;
        }

        if (_lunge <= 0) return;

        _lunge -= delta;

        if (_lunge <= 0)
        {
            Position = _restPosition;
            return;
        }

        // Out fast and back slower, so the swing has a direction in time as well as in space.
        // A symmetric curve reads as a twitch rather than a blow.
        var t = 1.0 - (_lunge / _lungeDuration);
        var curve = t < 0.35 ? t / 0.35 : 1.0 - ((t - 0.35) / 0.65);

        Position = _restPosition + (_lungeDirection * _lungeDistance * (float)curve);
    }

    /// <summary>Replaces the current visual. Safe to call at runtime (used by the debug tools).</summary>
    public void Apply(string visualId, string? tint = null, double scale = 1.0)
    {
        if (_current is not null)
        {
            _current.QueueFree();
            _current = null;
        }

        // The private flash copy belonged to the old visual.
        _flashMaterial = null;
        _flash = 0;

        if (!GameContent.IsLoaded)
        {
            GD.PushWarning($"VisualRoot '{Name}': content not loaded yet; visual '{visualId}' skipped.");
            return;
        }

        if (!GameContent.Database.Visuals.TryGetValue(visualId, out var def))
        {
            // Should be impossible: the content validator fails the build on a missing
            // visual reference. If it happens, something bypassed the pipeline.
            GD.PushError($"VisualRoot '{Name}': unknown visual id '{visualId}'.");
            return;
        }

        GroundRadius = (float)(def.Radius * scale);

        _current = VisualRegistry.Create(def, tint, scale);
        AddChild(_current);

        AdoptAnimator();
    }

    /// <summary>
    /// Hands the new model to an animator, if it brought animations with it.
    /// </summary>
    /// <remarks>
    /// The animator is a child of this node rather than of the model, so replacing the visual
    /// — which happens whenever an enemy's definition is applied — cannot leave a second one
    /// running against a freed skeleton.
    /// </remarks>
    private void AdoptAnimator()
    {
        _animator?.QueueFree();
        _animator = null;

        if (_current is null) return;

        var animator = new ModelAnimator { Name = "Animator" };

        AddChild(animator);
        animator.Adopt(_current, this);

        if (animator.Ready) _animator = animator;
        else animator.QueueFree();
    }

    /// <summary>
    /// How wide what is drawn here stands on the ground, in metres.
    /// </summary>
    /// <remarks>
    /// Taken from the same visual definition and per-creature scale that built the mesh, so
    /// anything drawing around a body — the target ring today — is sized by the body rather
    /// than by a number somebody guessed once. It keeps working across the ENG-07 boundary:
    /// when real models replace the placeholders, the radius still comes from the data that
    /// sizes them.
    /// </remarks>
    public float GroundRadius { get; private set; } = 0.4f;
}
