using System.Collections.Generic;
using Godot;

namespace Kiln.Game.Visual;

/// <summary>
/// The ribbon of light a swung blade leaves behind it (REF-21), as in the original: faint on a
/// plain blow, in the skill's rank colour on a skill. Only the outer half of the blade draws
/// it, fading to nothing at its inner edge, so a full turn leaves a band, not a disc.
/// </summary>
/// <remarks>
/// It samples where the blade's edge is every frame it is asked to — from a third of the way up
/// to the tip — and draws the band between the last few samples, fading with their age. It is
/// in world space, so the ribbon stays where the blade passed instead of following it round.
/// <para>
/// A node on the blade, as the skin and the glow are, but drawn apart from it: top-level, with
/// its own mesh rebuilt each frame it has something to show, and empty the rest of the time.
/// </para>
/// </remarks>
public partial class BladeTrail : MeshInstance3D
{
    /// <summary>How long a sample stays on screen, fading, in seconds.</summary>
    private const double Fade = 0.16;

    private readonly List<Sample> _samples = [];
    private readonly ImmediateMesh _mesh = new();
    private MeshInstance3D _blade = null!;
    private Vector3 _base;
    private Vector3 _tip;

    private double _delay;
    private double _emitFor;
    private Color _colour = Colors.White;

    private readonly record struct Sample(Vector3 Base, Vector3 Tip, double Born);

    private double _clock;

    /// <summary>The trail on a blade, made the first time it is asked for.</summary>
    public static BladeTrail? On(MeshInstance3D? blade)
    {
        if (blade?.Mesh is null) return null;

        if (blade.GetNodeOrNull<BladeTrail>("Trail") is { } trail) return trail;

        trail = new BladeTrail { Name = "Trail" };
        blade.AddChild(trail);
        trail.Measure(blade);

        return trail;
    }

    /// <summary>
    /// Draws the trail from <paramref name="delay"/> seconds from now, for
    /// <paramref name="seconds"/>, in <paramref name="colour"/> (its alpha is how strong).
    /// </summary>
    public void Emit(double delay, double seconds, Color colour)
    {
        _delay = System.Math.Max(0, delay);
        _emitFor = System.Math.Max(0.02, seconds);
        _colour = colour;
    }

    /// <summary>
    /// Finds the blade's edge from its mesh: the long side of its box, from a third of the way
    /// out from the grip to the far end. The grip is the end nearer the mesh's origin, which is
    /// where the hand holds it.
    /// </summary>
    private void Measure(MeshInstance3D blade)
    {
        _blade = blade;

        TopLevel = true;
        Mesh = _mesh;
        CastShadow = ShadowCastingSetting.Off;
        MaterialOverride = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            VertexColorUseAsAlbedo = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
        };

        var box = blade.Mesh!.GetAabb();
        var axis = box.GetLongestAxisIndex();
        var low = box.Position[(int)axis];
        var high = box.End[(int)axis];

        // The tip is the end further from the grip.
        var (grip, far) = Mathf.Abs(low) < Mathf.Abs(high) ? (low, high) : (high, low);
        var centre = box.GetCenter();

        _tip = centre;
        _tip[(int)axis] = far;
        _base = centre;
        _base[(int)axis] = Mathf.Lerp(grip, far, 0.55f);

        GlobalTransform = Transform3D.Identity;
    }

    public override void _Process(double delta)
    {
        _clock += delta;

        if (_delay > 0)
        {
            _delay -= delta;
        }
        else if (_emitFor > 0)
        {
            _emitFor -= delta;

            if (IsInstanceValid(_blade) && _blade.IsVisibleInTree())
            {
                var to = _blade.GlobalTransform;
                _samples.Add(new Sample(to * _base, to * _tip, _clock));
            }
        }

        _samples.RemoveAll(s => _clock - s.Born > Fade);

        _mesh.ClearSurfaces();

        if (_samples.Count < 2) return;

        _mesh.SurfaceBegin(Godot.Mesh.PrimitiveType.TriangleStrip);

        foreach (var sample in _samples)
        {
            var life = 1f - (float)((_clock - sample.Born) / Fade);
            var strength = _colour.A * life * life;

            // Dim at the inner edge, full at the tip: the band reads as the edge's path.
            _mesh.SurfaceSetColor(_colour with { A = 0f });
            _mesh.SurfaceAddVertex(sample.Base);
            _mesh.SurfaceSetColor(_colour with { A = strength });
            _mesh.SurfaceAddVertex(sample.Tip);
        }

        _mesh.SurfaceEnd();
    }
}
