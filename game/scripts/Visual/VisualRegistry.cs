using System.Collections.Generic;
using Godot;
using Shardfall.Data.Definitions;

namespace Shardfall.Game.Visual;

/// <summary>
/// The art-swap boundary (ENG-07, NFR-M.4).
/// <para>
/// No gameplay code ever names a mesh. It names a logical visual id — "mesh_placeholder_quadruped" —
/// and this registry decides what that currently is. Today: a coloured primitive. Later:
/// a real model, by changing <c>game/data/tables/visuals.json</c> and nothing else.
/// </para>
/// <para>
/// This is what makes the placeholder-art decision (Q2) cheap to reverse instead of a
/// rewrite later.
/// </para>
/// </summary>
public static class VisualRegistry
{
    private static readonly Dictionary<string, Mesh> MeshCache = new();
    private static readonly Dictionary<string, Material> MaterialCache = new();

    /// <summary>
    /// Builds the visual for a definition. Returns a plain MeshInstance3D for primitives,
    /// or an instanced scene once <c>primitive: "model"</c> and a model_path are set.
    /// </summary>
    public static Node3D Create(VisualDef def, string? tintOverride = null, double scale = 1.0)
    {
        if (def.Primitive == "model" && !string.IsNullOrEmpty(def.ModelPath))
        {
            var packed = ResourceLoader.Load<PackedScene>(def.ModelPath);
            if (packed is not null)
            {
                var instance = packed.Instantiate<Node3D>();
                instance.Scale = Vector3.One * (float)scale;
                return instance;
            }

            GD.PushWarning($"VisualRegistry: model '{def.ModelPath}' for '{def.Id}' failed to load; using a primitive.");
        }

        var mesh = new MeshInstance3D
        {
            Name = def.Id,
            Mesh = GetMesh(def),
            MaterialOverride = GetMaterial(tintOverride ?? def.Color),
            Scale = Vector3.One * (float)scale,
        };

        return mesh;
    }

    private static Mesh GetMesh(VisualDef def)
    {
        var key = $"{def.Primitive}:{def.Height:F3}:{def.Radius:F3}";
        if (MeshCache.TryGetValue(key, out var cached)) return cached;

        Mesh mesh = def.Primitive switch
        {
            "box" => new BoxMesh { Size = new Vector3((float)def.Radius * 2f, (float)def.Height, (float)def.Radius * 2f) },
            "sphere" => new SphereMesh { Radius = (float)def.Radius, Height = (float)def.Height },
            "cylinder" => new CylinderMesh
            {
                TopRadius = (float)def.Radius,
                BottomRadius = (float)def.Radius,
                Height = (float)def.Height,
            },
            // A shard monolith: tapered, so it reads differently from an enemy at a glance.
            "monolith" => new CylinderMesh
            {
                TopRadius = (float)def.Radius * 0.45f,
                BottomRadius = (float)def.Radius,
                Height = (float)def.Height,
            },
            // "capsule" is the default: it reads as a character.
            _ => new CapsuleMesh
            {
                Radius = (float)def.Radius,
                Height = (float)def.Height,
            },
        };

        MeshCache[key] = mesh;
        return mesh;
    }

    private static Material GetMaterial(string hexColor)
    {
        if (MaterialCache.TryGetValue(hexColor, out var cached)) return cached;

        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(hexColor),
            Roughness = 0.7f,
        };

        MaterialCache[hexColor] = material;
        return material;
    }

    /// <summary>Drops cached meshes and materials. Call when reloading content at runtime.</summary>
    public static void ClearCache()
    {
        MeshCache.Clear();
        MaterialCache.Clear();
    }
}
