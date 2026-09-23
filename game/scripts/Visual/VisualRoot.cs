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

    /// <summary>
    /// The attack animation, in place (REF-01): the character stands still while it strikes.
    /// A model plays its swing; a primitive, which has none, gives a small nod forward so the
    /// blow is still visible.
    /// </summary>
    public void Swing(Vector3 direction)
    {
        if (_animator is { Ready: true })
        {
            _animator.Attack();
            return;
        }

        Lunge(direction, 0.12f, 0.16);
    }

    /// <summary>
    /// The flourish a self-buff is cast with: weapon raised rather than swung, so Iron Skin
    /// and the Blade Aura do not look like another attack (REF-03).
    /// </summary>
    public void Flourish()
    {
        if (_animator is { Ready: true })
        {
            _animator.Flourish();
            return;
        }

        Lunge(Vector3.Up, 0.18f, 0.35);
    }

    /// <summary>
    /// One turn on the spot, lasting <paramref name="seconds"/> (Whirlwind, REF-03).
    /// </summary>
    /// <remarks>
    /// The model's own spin when it has one. Otherwise the whole visual is turned a full
    /// revolution — the direction alternating hit to hit, so a flurry winds and unwinds rather
    /// than drilling one way — which is what a primitive can show of a spin.
    /// </remarks>
    public void Spin(double seconds, int hit)
    {
        if (_animator is { Ready: true } && _animator.Spin(seconds)) return;

        _spinFor = System.Math.Max(0.05, seconds);
        _spinLeft = _spinFor;
        _spinWay = hit % 2 == 0 ? 1f : -1f;
    }

    private double _spinFor;
    private double _spinLeft;
    private float _spinWay = 1f;
    private float? _restYaw;

    /// <summary>
    /// Turns the model inside this root, not the root: the root is what the motor points at
    /// the target, and it would snap the spin back on the next frame it faced something.
    /// </summary>
    private void TurnModel(double delta)
    {
        if (_current is null || (_spinLeft <= 0 && _restYaw is null)) return;

        _restYaw ??= _current.Rotation.Y;

        _spinLeft -= delta;

        if (_spinLeft <= 0)
        {
            _current.Rotation = _current.Rotation with { Y = _restYaw.Value };
            _restYaw = null;
            _spinLeft = 0;
            return;
        }

        var done = 1.0 - (_spinLeft / _spinFor);

        _current.Rotation = _current.Rotation with
        {
            Y = _restYaw.Value + (Mathf.Tau * (float)done * _spinWay),
        };
    }

    /// <summary>Cuts the swing animation short when the player walks off mid-follow-through.</summary>
    public void CancelSwing() => _animator?.CancelAttack();

    /// <summary>
    /// The flinch of being hit (REF-01): a short recoil away from the blow, and the model's own
    /// hit reaction when it has one. Visual only — the creature keeps acting; this is not a stun
    /// and not knockback.
    /// </summary>
    public void Flinch(Vector3 away)
    {
        var flat = away with { Y = 0 };

        if (flat.LengthSquared() < 0.0001f) return;

        _animator?.Hit();

        _lungeDirection = flat.Normalized();
        _lungeDistance = 0.14f;
        _lungeDuration = 0.16;
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

        TurnModel(delta);

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

        StandOnGround(def);

        AdoptAnimator();
    }

    /// <summary>
    /// Puts a model's feet on the ground its body stands on.
    /// </summary>
    /// <remarks>
    /// A model is centred on this node, and this node sits a fixed height above the body's
    /// origin — 0.85 m in the enemy scene, half of a 1.7 m figure. That was right while every
    /// creature was a person-sized KayKit model; a 0.55 m rat centred there floated sixty
    /// centimetres off the grass. The body's origin is its feet, so the model's lowest point
    /// is moved to meet it, whatever the model's size. A model that already stood there moves
    /// by nothing.
    /// <para>
    /// Only for models whose own lowest point is their feet — creatures and people. A shard
    /// stone is placed by its scene at its centre on purpose, and keeps that.
    /// </para>
    /// </remarks>
    private void StandOnGround(VisualDef def)
    {
        if (_current is null || def.Primitive != "model" || def.Fit == "stretch") return;
        if (GetParent() is not CharacterBody3D) return;

        var bounds = ModelFit.LocalBounds(_current);

        if (bounds.Size.Y <= 0.0001f) return;

        // In this node's space: the model's current bottom, and where the ground is.
        var bottom = (_current.Transform * bounds).Position.Y;
        var ground = -_restPosition.Y;

        _current.Position += new Vector3(0, ground - bottom, 0);
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
