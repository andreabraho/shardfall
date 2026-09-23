using Godot;

namespace Kiln.Game.World;

/// <summary>
/// What a border looks like: two standing stones and a turning disc of light between them.
/// </summary>
/// <remarks>
/// Kept see-through on purpose — the disc is a haze the road is seen through, not a door.
/// <para>
/// The crossing to the next map used to be an invisible sphere on the road, with a sign that
/// appeared only while Alt was held. A player walking the road had nothing to aim for, and the
/// end of a map read as the road simply stopping. This is built by every <see cref="ZoneGate"/>
/// rather than placed in each scene, so every border in the game looks like one, including the
/// ones not yet dressed.
/// </para>
/// <para>
/// It says whether the way is open. Blue and turning briskly when the player may cross; a slow,
/// dim red when the next map asks for a level or a quest the player does not have yet — read
/// from across the field, before the walk to it.
/// </para>
/// </remarks>
public partial class PortalVisual : Node3D
{
    private static readonly Color OpenColour = new(0.45f, 0.78f, 1.0f);
    private static readonly Color ShutColour = new(0.85f, 0.32f, 0.26f);

    private static readonly Shader DiscShader = new()
    {
        Code = """
            shader_type spatial;
            render_mode unshaded, cull_disabled, blend_add, depth_draw_never;

            uniform vec4 tint : source_color = vec4(0.45, 0.78, 1.0, 1.0);
            uniform float speed = 1.0;
            uniform float strength = 1.0;

            void fragment() {
                vec2 p = UV * 2.0 - 1.0;
                float r = length(p);
                float a = atan(p.y, p.x);

                // A spiral turning inward, and a rim that holds the shape together.
                float spiral = 0.5 + 0.5 * sin(a * 3.0 + r * 10.0 - TIME * 3.0 * speed);
                float core = 1.0 - smoothstep(0.0, 0.55, r);
                float rim = smoothstep(0.72, 0.9, r) * (1.0 - smoothstep(0.9, 1.0, r));
                float body = (1.0 - smoothstep(0.85, 1.0, r)) * (0.25 + 0.45 * spiral);

                float glow = clamp(body * 0.7 + core * 0.3 + rim * 0.25, 0.0, 1.0);

                ALBEDO = tint.rgb * (0.5 + glow);
                ALPHA = glow * strength;
            }
            """,
    };

    private ShaderMaterial? _disc;
    private OmniLight3D? _light;
    private MeshInstance3D? _discMesh;
    private bool _open = true;
    private float _shown = 1f;

    /// <summary>Which way the road runs through the portal, flat. The stones stand either side of it.</summary>
    public Vector3 Along { get; set; } = Vector3.Right;

    /// <summary>Whether the player may cross. Drives the colour and the pace.</summary>
    public bool Open
    {
        get => _open;
        set => _open = value;
    }

    public override void _Ready()
    {
        var along = (Along with { Y = 0 }).Normalized();

        if (along.LengthSquared() < 0.01f) along = Vector3.Right;

        var across = along.Cross(Vector3.Up).Normalized();

        // The stones: the same rocks the woods are strewn with, stood on end.
        foreach (var side in new[] { -1f, 1f })
        {
            AddChild(new KitPiece
            {
                Name = side < 0 ? "StoneL" : "StoneR",
                PieceId = "kit_portal_stone",
                Position = (across * 3.4f * side) + new Vector3(0, 2.6f, 0),
            });
        }

        _disc = new ShaderMaterial { Shader = DiscShader };

        _discMesh = new MeshInstance3D
        {
            Name = "Disc",
            Mesh = new QuadMesh { Size = new Vector2(5.2f, 5.2f) },
            MaterialOverride = _disc,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0, 2.7f, 0),
        };

        AddChild(_discMesh);

        // The quad faces +Z; turn it to face along the road, so it is walked into, not past.
        _discMesh.LookAt(_discMesh.GlobalPosition + along, Vector3.Up);

        _light = new OmniLight3D
        {
            Name = "Glow",
            Position = new Vector3(0, 2.6f, 0),
            OmniRange = 9f,
            LightEnergy = 1.6f,
            ShadowEnabled = false,
        };

        AddChild(_light);
    }

    public override void _Process(double delta)
    {
        if (_disc is null || _light is null) return;

        // Eased between the two states, so a portal that opens on a level-up is seen to open.
        _shown = Mathf.MoveToward(_shown, _open ? 1f : 0f, (float)delta * 1.5f);

        var colour = ShutColour.Lerp(OpenColour, _shown);
        var pulse = 0.85f + (Mathf.Sin(Time.GetTicksMsec() / 1000f * 2.2f) * 0.15f);

        _disc.SetShaderParameter("tint", colour);
        _disc.SetShaderParameter("speed", Mathf.Lerp(0.25f, 1.0f, _shown));
        _disc.SetShaderParameter("strength", Mathf.Lerp(0.3f, 0.55f, _shown));

        _light.LightColor = colour;
        _light.LightEnergy = Mathf.Lerp(0.7f, 1.6f, _shown) * pulse;
    }
}
