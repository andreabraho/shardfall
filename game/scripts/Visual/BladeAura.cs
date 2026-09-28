using System.Linq;
using Godot;
using Kiln.Data.Definitions;

namespace Kiln.Game.Visual;

/// <summary>
/// Light round a blade, in two kinds. A worn aura (REF-23) is a stream of points wrapped round
/// the sword with a light of its colour, as the original's sword aura. The Blade Aura skill
/// (REF-21) is a faceted glow instead (2026-09-28, at your call): two shells the blade's own
/// shape, a little larger than it, lit towards their edges and stirred by a slow flow along
/// the blade, so the steel keeps its colour.
/// </summary>
/// <remarks>
/// A node on the blade, so taking the aura off is taking the node away, and it follows every
/// swing exactly. The two are worn under different names, so the look and the skill can be on
/// at once.
/// </remarks>
public partial class BladeAura : Node3D
{
    private const string ShaderCode = """
        shader_type spatial;
        render_mode blend_add, unshaded, depth_draw_never, cull_back, shadows_disabled;

        uniform vec4 colour : source_color = vec4(1.0);
        uniform float strength = 1.0;
        uniform float flow_scale = 4.0;
        uniform float flow_speed = 1.2;
        uniform vec3 along = vec3(0.0, 1.0, 0.0);

        varying vec3 local_pos;

        float hash(vec3 p) {
            p = fract(p * 0.3183099 + 0.1);
            p *= 17.0;
            return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        float noise(vec3 x) {
            vec3 i = floor(x);
            vec3 f = fract(x);
            f = f * f * (3.0 - 2.0 * f);
            return mix(
                mix(mix(hash(i), hash(i + vec3(1, 0, 0)), f.x), mix(hash(i + vec3(0, 1, 0)), hash(i + vec3(1, 1, 0)), f.x), f.y),
                mix(mix(hash(i + vec3(0, 0, 1)), hash(i + vec3(1, 0, 1)), f.x), mix(hash(i + vec3(0, 1, 1)), hash(i + vec3(1, 1, 1)), f.x), f.y),
                f.z);
        }

        void vertex() {
            local_pos = VERTEX;
        }

        void fragment() {
            // Bright towards the outline, faint where the shell faces the eye: round the blade, not on it.
            float rim = 1.0 - clamp(abs(dot(NORMAL, VIEW)), 0.0, 1.0);
            rim = pow(rim, 1.6);

            // A slow flow running up the blade, so the light moves like a flame and not like paint.
            vec3 p = local_pos * flow_scale - along * TIME * flow_speed;
            float n = noise(p) * 0.65 + noise(p * 2.1 + 7.0) * 0.35;

            ALBEDO = colour.rgb;
            ALPHA = clamp((0.08 + rim) * (0.35 + 0.9 * n) * strength, 0.0, 1.0);
        }
        """;

    private static Shader? _shader;

    /// <summary>Puts <paramref name="aura"/> on a blade, or takes any aura off with null.</summary>
    public static void Set(MeshInstance3D? blade, CosmeticDef? aura)
    {
        if (blade is null) return;

        Remove(blade, "Aura");

        if (aura is null || blade.Mesh is null) return;

        // A worn aura keeps its stream of points and its light, as it was (2026-09-28, at your
        // call: the Arcane Aura was right). Only the Blade Aura skill wears the faceted glow.
        var node = new BladeAura { Name = "Aura" };
        blade.AddChild(node);
        node.Stream(blade, new Color(aura.Color), aura.Particles, light: 0.7f);
    }

    /// <summary>How far round the blade a worn aura's points are born, in the blade's own units.</summary>
    private const float Margin = 0.035f;

    /// <summary>A worn aura: a steady stream of points wrapped round the blade, and a light.</summary>
    private void Stream(MeshInstance3D blade, Color colour, string kind, float light)
    {
        var box = blade.Mesh!.GetAabb();

        // The blade's box, thickened on its thin sides so the points sit round the flat of the
        // blade and its edge rather than inside the steel.
        var half = box.Size * 0.5f;
        var extents = new Vector3(half.X + Margin, half.Y + Margin, half.Z + Margin);

        Position = box.GetCenter();

        // The size of a point is in the blade's space, which the model may have scaled: undone
        // here so a point is the same size on any model.
        var scale = blade.GlobalTransform.Basis.Scale;
        var shrink = 1f / Mathf.Max(0.01f, (scale.X + scale.Y + scale.Z) / 3f);

        foreach (var layer in SkinParticles.SwordAura(kind, colour, extents, shrink)) AddChild(layer);

        if (light <= 0) return;

        AddChild(new OmniLight3D
        {
            LightColor = colour,
            LightEnergy = light,
            OmniRange = 1.4f,
            ShadowEnabled = false,
        });
    }

    /// <summary>Wraps a blade in a glow of <paramref name="colour"/>, as a node called <paramref name="name"/>.</summary>
    /// <param name="kind">The effect's kind: embers run faster than frost.</param>
    /// <param name="light">Kept for callers; the aura casts no light (it tinted the steel).</param>
    public static BladeAura? Wrap(MeshInstance3D blade, Color colour, string kind, string name, float light = 0f)
    {
        if (blade.Mesh is null) return null;

        var node = new BladeAura { Name = name };
        blade.AddChild(node);
        node.Dress(blade, colour, kind);

        return node;
    }

    /// <summary>Takes the aura called <paramref name="name"/> off a blade, if it has one.</summary>
    public static void Remove(MeshInstance3D blade, string name)
    {
        if (blade.GetNodeOrNull<BladeAura>(name) is not { } old) return;

        blade.RemoveChild(old);
        old.QueueFree();
    }

    private void Dress(MeshInstance3D blade, Color colour, string kind)
    {
        var box = blade.Mesh!.GetAabb();
        var centre = box.GetCenter();
        var half = box.Size * 0.5f;

        // The blade's length is its longest side; the glow thickens it across, barely along.
        var sides = new[] { (half.X, Axis: Vector3.Right), (half.Y, Axis: Vector3.Up), (half.Z, Axis: Vector3.Back) }
            .OrderByDescending(s => s.Item1)
            .ToArray();

        var along = sides[0].Axis;
        var length = sides[0].Item1;
        var width = Mathf.Max(0.001f, sides[1].Item1);

        var speed = kind switch
        {
            "embers" => 1.8f,
            "frost" => 0.7f,
            _ => 1.2f,
        };

        // Close and bright, then wider and fainter. Faceted like the blade itself, which is the
        // look you chose over a smooth envelope (2026-09-28).
        Shell(blade.Mesh, centre, half, along, width * 0.45f, length, colour, 0.75f, speed);
        Shell(blade.Mesh, centre, half, along, width * 1.1f, length, colour, 0.35f, speed * 0.8f);
    }

    /// <summary>
    /// One shell: the blade's mesh again, scaled about its middle so it stands
    /// <paramref name="reach"/> clear of the blade across and a little past its ends.
    /// </summary>
    private void Shell(Mesh mesh, Vector3 centre, Vector3 half, Vector3 along, float reach, float length, Color colour, float strength, float speed)
    {
        Vector3 Grow(Vector3 axis, float h) => axis * ((h + (axis == along ? reach * 0.25f : reach)) / Mathf.Max(0.0001f, h));

        var scale = Grow(Vector3.Right, half.X) + Grow(Vector3.Up, half.Y) + Grow(Vector3.Back, half.Z);

        var material = new ShaderMaterial { Shader = _shader ??= new Shader { Code = ShaderCode } };
        material.SetShaderParameter("colour", colour);
        material.SetShaderParameter("strength", strength);
        material.SetShaderParameter("flow_scale", 2.5f / Mathf.Max(0.001f, length));
        material.SetShaderParameter("flow_speed", speed);
        material.SetShaderParameter("along", along);

        AddChild(new MeshInstance3D
        {
            Name = "Shell",
            Mesh = mesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,

            // Scaled about the blade's middle: moved so that point stays put.
            Scale = scale,
            Position = centre - (centre * scale),
        });
    }
}
