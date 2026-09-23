using System.Collections.Generic;
using Godot;

namespace Kiln.Game.World;

/// <summary>
/// What turns a standing stone into a shard: glowing veins that pulse up through the rock,
/// crystals turning about it, and a light that colours the ground around it (map themes,
/// 2026-09-23).
/// </summary>
/// <remarks>
/// No free pack has the thing itself, and it is the object each map is built around, so it is
/// dressed here from a plain stone. The veins are drawn over the stone's own surface rather
/// than as a separate mesh, so any rock a map chooses becomes a shard in its own colour: moss
/// green in the woods, and whatever the next five themes want.
/// <para>
/// The colour comes from the stone's visual definition. A rolled modifier overrides it — the
/// modifier is the one thing a player needs to know before walking in, and a stone burning in
/// the wrong colour says it from across the field.
/// </para>
/// </remarks>
public partial class ShardGlow : Node3D
{
    private const int Crystals = 4;

    private static readonly Shader VeinShader = new()
    {
        Code = """
            shader_type spatial;
            render_mode unshaded, blend_add, depth_draw_never, cull_back;

            uniform vec4 tint : source_color = vec4(0.5, 1.0, 0.4, 1.0);
            uniform float strength = 1.0;

            varying vec3 world;

            void vertex() {
                world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
            }

            // Two families of wavering bands, crossed: thin where either is near zero, which
            // reads as cracks with light behind them rather than stripes.
            float veins(vec3 p) {
                float a = abs(sin(p.y * 2.6 + sin(p.x * 2.1) * 1.8 + sin(p.z * 1.7) * 1.4));
                float b = abs(sin(p.x * 3.7 - p.y * 1.3 + sin(p.z * 2.9 + p.y) * 1.2));
                return pow(1.0 - min(a, b), 14.0);
            }

            void fragment() {
                float v = veins(world * 1.1);

                // A slow pulse climbing the stone.
                float pulse = 0.55 + 0.45 * sin(TIME * 2.2 - world.y * 1.8);

                // A faint rim, so the stone's outline glows even where no vein crosses it.
                float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0) * 0.35;

                ALBEDO = tint.rgb * 1.6;
                ALPHA = clamp((v * pulse + rim) * strength, 0.0, 1.0);
            }
            """,
    };

    private readonly List<MeshInstance3D> _crystals = [];
    private ShaderMaterial? _veins;
    private StandardMaterial3D? _crystalMaterial;
    private OmniLight3D? _light;
    private Color _colour = new(0.5f, 1f, 0.4f);
    private double _time;

    public override void _Ready()
    {
        _veins = new ShaderMaterial { Shader = VeinShader };

        _crystalMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        };

        // Long diamonds: a four-sided double cone, the cheapest shape that reads as a crystal.
        var diamond = new SphereMesh { Radius = 0.22f, Height = 0.9f, RadialSegments = 4, Rings = 2 };

        for (var i = 0; i < Crystals; i++)
        {
            var crystal = new MeshInstance3D
            {
                Name = $"Crystal{i}",
                Mesh = diamond,
                MaterialOverride = _crystalMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };

            AddChild(crystal);
            _crystals.Add(crystal);
        }

        _light = new OmniLight3D
        {
            Name = "Light",
            Position = new Vector3(0, 2.2f, 0),
            OmniRange = 10f,
            LightEnergy = 1.4f,
            ShadowEnabled = false,
        };

        AddChild(_light);
        Visible = false;
    }

    /// <summary>
    /// Dresses the stone the visual root has just built: veins in the stone's colour, or the
    /// modifier's when one was rolled.
    /// </summary>
    public void Dress(string visualId, Color? modifier)
    {
        var colour = GameContent.IsLoaded && GameContent.Database.Visuals.TryGetValue(visualId, out var def)
            && Color.HtmlIsValid(def.Color.TrimStart('#'))
            ? new Color(def.Color)
            : new Color(0.5f, 1f, 0.4f);

        _colour = modifier ?? colour;

        // The visual root rebuilds its model on every arm; wait a frame for it to exist.
        CallDeferred(nameof(Overlay));
        Visible = true;
    }

    private void Overlay()
    {
        if (_veins is null || _crystalMaterial is null || _light is null) return;

        _veins.SetShaderParameter("tint", _colour);
        _crystalMaterial.AlbedoColor = _colour with { A = 0.85f };
        _light.LightColor = _colour;

        if (GetParent().GetNodeOrNull<Node>("VisualRoot") is not { } root) return;

        // A dressed stone (map two's, with its banners and chains) names the rock "Stone", so
        // only the rock burns; a bare rock is all stone.
        var stone = root.FindChild("Stone", recursive: true, owned: false) ?? root;

        foreach (var mesh in Meshes(stone)) mesh.MaterialOverlay = _veins;
    }

    private static IEnumerable<MeshInstance3D> Meshes(Node node)
    {
        if (node is MeshInstance3D mesh) yield return mesh;

        foreach (var child in node.GetChildren())
        {
            foreach (var found in Meshes(child)) yield return found;
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;

        _time += delta;

        // Turning slowly about the stone, each at its own height and bob, never in step.
        for (var i = 0; i < _crystals.Count; i++)
        {
            var angle = (float)(_time * 0.6) + (i * Mathf.Tau / _crystals.Count);
            var height = 2.0f + (i % 2 * 0.8f) + (Mathf.Sin((float)(_time * 1.6) + i) * 0.25f);

            _crystals[i].Position = new Vector3(Mathf.Cos(angle) * 1.9f, height, Mathf.Sin(angle) * 1.9f);
            _crystals[i].Rotation = new Vector3(0, (float)(_time * 1.2) + i, 0.25f);
        }

        if (_light is not null) _light.LightEnergy = 1.1f + (Mathf.Sin((float)(_time * 2.2)) * 0.35f);
    }
}
