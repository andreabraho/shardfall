using System.Collections.Generic;
using System.Linq;
using Godot;
using Kiln.Data.Definitions;

namespace Kiln.Game.Visual;

/// <summary>
/// Puts a modular character together (REF-16): an outfit model, the head of a base body, and
/// the hair, beard and other parts made for the same skeleton — the Quaternius villagers.
/// </summary>
/// <remarks>
/// Every piece ships as its own glTF on the same 65-bone rig. Each skinned mesh is taken out of
/// its file and hung on the outfit's skeleton, where the shared bone names bind it. The base
/// body is a whole figure; only its head is kept — the triangles weighted to the head and the
/// neck — because the outfit brings its own body, and a second one underneath shows through.
/// </remarks>
public static class SkinnedParts
{
    /// <summary>Bones whose triangles count as the head.</summary>
    private static readonly HashSet<string> HeadBones = new(System.StringComparer.Ordinal) { "Head", "neck_01" };

    private static readonly Dictionary<string, ArrayMesh> CutHeads = new(System.StringComparer.Ordinal);

    public static void Assemble(Node3D model, VisualDef def)
    {
        if (def.Parts.Length == 0 && string.IsNullOrEmpty(def.Head) && string.IsNullOrEmpty(def.RetextureTo)) return;

        var skeleton = model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();

        if (skeleton is null)
        {
            GD.PushWarning($"[visual] '{def.Id}' has parts but its model has no skeleton.");
            return;
        }

        if (!string.IsNullOrEmpty(def.RetextureTo)) Retexture(model, def.RetextureFrom ?? "", def.RetextureTo!);

        if (!string.IsNullOrEmpty(def.Head)) Hang(def.Head!, skeleton, headOnly: true);

        foreach (var part in def.Parts) Hang(part, skeleton, headOnly: false);
    }

    /// <summary>Moves every skinned mesh of a part's file onto <paramref name="skeleton"/>.</summary>
    private static void Hang(string path, Skeleton3D skeleton, bool headOnly)
    {
        if (ResourceLoader.Load<PackedScene>(path)?.Instantiate<Node3D>() is not { } part)
        {
            GD.PushWarning($"[visual] part '{path}' did not load.");
            return;
        }

        foreach (var mesh in part.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().ToList())
        {
            // A whole figure's body is cut to its head; eyes, brows and hair stay whole.
            if (headOnly && mesh.Mesh is ArrayMesh whole && whole.GetAabb().Size.Y > 1.0f)
            {
                var cut = CutHead(path + ":" + mesh.Name, whole, mesh.Skin);

                if (cut is null) continue;

                mesh.Mesh = cut;
            }

            // Out of the file it came in, which owned it, and into this model.
            mesh.Owner = null;
            mesh.GetParent()?.RemoveChild(mesh);
            skeleton.AddChild(mesh);
            mesh.Transform = Transform3D.Identity;
            mesh.Skeleton = mesh.GetPathTo(skeleton);
        }

        part.QueueFree();
    }

    /// <summary>
    /// The same mesh with only the triangles whose three corners are mostly weighted to the
    /// head and neck. The vertices are kept as they are; only the index list is filtered.
    /// </summary>
    private static ArrayMesh? CutHead(string key, ArrayMesh whole, Skin? skin)
    {
        if (CutHeads.TryGetValue(key, out var cached)) return cached;

        if (skin is null) return null;

        var head = new HashSet<int>();

        for (var b = 0; b < skin.GetBindCount(); b++)
        {
            var name = skin.GetBindName(b).ToString();

            if (HeadBones.Contains(name)) head.Add(b);
        }

        var cut = new ArrayMesh();

        for (var s = 0; s < whole.GetSurfaceCount(); s++)
        {
            var arrays = whole.SurfaceGetArrays(s);
            var bones = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
            var weights = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
            var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();

            if (bones.Length == 0 || indices.Length == 0 || vertices.Length == 0) continue;

            var per = bones.Length / vertices.Length;
            var kept = new List<int>(indices.Length / 4);

            bool OnHead(int v)
            {
                var share = 0f;

                for (var k = 0; k < per; k++)
                {
                    if (head.Contains(bones[(v * per) + k])) share += weights[(v * per) + k];
                }

                return share >= 0.5f;
            }

            for (var t = 0; t + 2 < indices.Length; t += 3)
            {
                if (OnHead(indices[t]) && OnHead(indices[t + 1]) && OnHead(indices[t + 2]))
                {
                    kept.Add(indices[t]);
                    kept.Add(indices[t + 1]);
                    kept.Add(indices[t + 2]);
                }
            }

            if (kept.Count == 0) continue;

            arrays[(int)Mesh.ArrayType.Index] = kept.ToArray();

            var flags = (Mesh.ArrayFormat)((ulong)whole.SurfaceGetFormat(s) & ~((ulong)Mesh.ArrayFormat.FlagCompressAttributes));
            cut.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays, [], [], flags);
            cut.SurfaceSetMaterial(cut.GetSurfaceCount() - 1, whole.SurfaceGetMaterial(s));
        }

        CutHeads[key] = cut;

        return cut;
    }

    /// <summary>Swaps a base-colour texture on the model: an outfit's other colourway.</summary>
    private static void Retexture(Node3D model, string from, string to)
    {
        if (ResourceLoader.Load<Texture2D>(to) is not { } texture) return;

        foreach (var mesh in model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            if (mesh.Mesh is null) continue;

            for (var s = 0; s < mesh.Mesh.GetSurfaceCount(); s++)
            {
                if (mesh.Mesh.SurfaceGetMaterial(s) is not BaseMaterial3D material) continue;
                if (material.AlbedoTexture?.ResourcePath is not { } path || !path.Contains(from)) continue;

                var copy = (BaseMaterial3D)material.Duplicate();
                copy.AlbedoTexture = texture;
                mesh.SetSurfaceOverrideMaterial(s, copy);
            }
        }
    }
}
