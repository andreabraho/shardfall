using Godot;
using Kiln.Data.Definitions;

namespace Kiln.Game.World;

/// <summary>
/// Paints a map's ground with game/shaders/ground.gdshader in the colours its zone gives
/// (2026-09-30): swathes of light and shade, patches, grain and specks, where it was one flat
/// colour from edge to edge.
/// </summary>
/// <remarks>
/// The scene's own ground colour stays the base the rest is mixed from, and it is what the
/// mountains read to grass their lower slopes (ZoneBorder) — so they are painted first and the
/// two still meet without a seam.
/// </remarks>
public static class ZoneGround
{
    private const string ShaderPath = "res://shaders/ground.gdshader";

    private static NoiseTexture2D? _noise;

    public static void Paint(Node root, ZoneDef zone)
    {
        if (zone.Ground is not { } ground) return;

        var mesh = root.GetNodeOrNull<MeshInstance3D>("NavigationRegion3D/Ground/Mesh");

        if (mesh is null) return;

        var own = (mesh.GetSurfaceOverrideMaterial(0) ?? mesh.MaterialOverride) is StandardMaterial3D flat
            ? flat.AlbedoColor
            : new Color(0.31f, 0.41f, 0.22f);

        var material = new ShaderMaterial { Shader = GD.Load<Shader>(ShaderPath) };

        material.SetShaderParameter("base_colour", own);
        material.SetShaderParameter("light_colour", Color.FromHtml(ground.Light));
        material.SetShaderParameter("dark_colour", Color.FromHtml(ground.Dark));
        material.SetShaderParameter("patch_colour", Color.FromHtml(ground.Patch));
        material.SetShaderParameter("patch_amount", (float)ground.PatchAmount);
        material.SetShaderParameter("speck_colour", Color.FromHtml(ground.Speck));
        material.SetShaderParameter("speck_amount", (float)ground.SpeckAmount);
        material.SetShaderParameter("grain", (float)ground.Grain);
        material.SetShaderParameter("noise", Noise());

        mesh.SetSurfaceOverrideMaterial(0, material);
    }

    /// <summary>
    /// One tileable noise every map shares, made once: it tiles, so sampled in world space at
    /// several scales it never shows a seam.
    /// </summary>
    private static NoiseTexture2D Noise() => _noise ??= new NoiseTexture2D
    {
        Width = 512,
        Height = 512,
        Seamless = true,
        GenerateMipmaps = true,
        Noise = new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
            Frequency = 0.012f,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = 4,
            Seed = 7,
        },
    };
}
