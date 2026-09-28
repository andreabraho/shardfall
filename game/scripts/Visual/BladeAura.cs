using System.Linq;
using Godot;
using Kiln.Data.Definitions;

namespace Kiln.Game.Visual;

/// <summary>
/// An aura look on a blade (REF-23): a full glow round the sword, as the original's sword
/// aura — not a bubble round the player (2026-09-26), not a stream of points and not a coat of
/// paint on the steel (2026-09-28, at your call). The same on the sword and the great sword,
/// and beside a sword skin rather than in place of it.
/// </summary>
/// <remarks>
/// Two shells the blade's own shape, a little larger than it, lit only towards their edges and
/// stirred by a slow flow along the blade: the light hangs round the sword's outline while the
/// face of the blade keeps its own colour. A node on the blade, so taking the aura off is
/// taking the node away, and the shells follow every swing exactly.
/// <para>
/// The Blade Aura skill wraps the blade the same way while it lasts (REF-21), in its rank's
/// colour and under a name of its own, so the look and the skill are both worn at once.
/// </para>
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

        if (aura is null) return;

        Wrap(blade, new Color(aura.Color), aura.Particles, "Aura");
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
