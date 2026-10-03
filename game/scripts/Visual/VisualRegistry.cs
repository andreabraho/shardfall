using System.Collections.Generic;
using System.Linq;
using Godot;
using Kiln.Data.Definitions;

namespace Kiln.Game.Visual;

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
    /// <summary>The name of a node that holds a piece hung on a bone: what recolours the creature leaves it alone.</summary>
    public const string AttachedPrefix = "Attached";

    /// <summary>Hangs the visual's pieces on its bones (A2-01), each upright over its bone as the creature stands at rest.</summary>
    private static void Attach(Node3D instance, VisualDef def)
    {
        if (def.Attachments.Length == 0) return;

        if (instance.FindChildren("*", "Skeleton3D", true, false).FirstOrDefault() is not Skeleton3D skeleton)
        {
            GD.PushWarning($"VisualRegistry: '{def.Id}' has attachments and no skeleton.");
            return;
        }

        // From the skeleton's space to the model's own, where up is up whatever the pack's axes were.
        var toModel = Transform3D.Identity;

        for (Node node = skeleton; node != instance && node is Node3D spatial; node = node.GetParent()) toModel = spatial.Transform * toModel;

        var index = 0;

        foreach (var piece in def.Attachments)
        {
            var bone = skeleton.FindBone(piece.Bone);
            var packed = bone < 0 ? null : ResourceLoader.Load<PackedScene>(piece.ModelPath);

            if (packed is null)
            {
                GD.PushWarning($"VisualRegistry: '{def.Id}' cannot hang '{piece.ModelPath}' on '{piece.Bone}'.");
                continue;
            }

            var rest = toModel * skeleton.GetBoneGlobalRest(bone);
            var tall = Mathf.Abs(rest.Origin.Y);

            // Where it should be in the model's space: over the bone, upright, facing the way the model faces (-Z).
            var wanted = new Transform3D(
                Basis.Identity.Rotated(Vector3.Right, Mathf.DegToRad((float)piece.Pitch)).Scaled(Vector3.One * (float)(piece.Scale * tall)),
                rest.Origin + new Vector3(0, (float)(piece.Up * tall), (float)(-piece.Forward * tall)));

            var holder = new BoneAttachment3D { Name = $"{AttachedPrefix}{index++}", BoneName = piece.Bone };
            var hung = packed.Instantiate<Node3D>();

            skeleton.AddChild(holder);
            holder.AddChild(hung);

            // The holder follows the bone; the piece keeps the place it had at rest.
            hung.Transform = rest.AffineInverse() * wanted;
        }
    }

    public static Node3D Create(VisualDef def, string? tintOverride = null, double scale = 1.0)
    {
        if (def.Primitive == "model" && !string.IsNullOrEmpty(def.ModelPath))
        {
            var packed = ResourceLoader.Load<PackedScene>(def.ModelPath);
            if (packed is not null)
            {
                var instance = packed.Instantiate<Node3D>();

                // A modular character's head, hair and colourway (REF-16).
                SkinnedParts.Assemble(instance, def);

                // Clips that ship in files of their own (REF-22).
                AnimationLibraries.Attach(instance, def.Animations);

                // Turned to face the way the engine thinks is forward, before it is fitted:
                // the fit centres the model, and centring something that is about to be spun
                // around leaves it off to one side.
                instance.RotationDegrees = new Vector3(0, (float)def.ModelYaw, 0);

                // Scaled to the height the data declares, not to the size its author worked
                // in, and then the per-creature scale on top. Without this a model lands at
                // whatever metre-per-unit its pack used and a wolf stands taller than a wall.
                if (def.Fit == "stretch")
                {
                    var width = (float)(def.Radius * 2 * scale);
                    ModelFit.Stretch(instance, new Vector3(width, (float)(def.Height * scale), width * 0.9f));
                }
                else
                {
                    ModelFit.ByHeight(instance, (float)(def.Height * scale));
                }

                // After the fit: a crown must not count toward how tall the creature is.
                Attach(instance, def);

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
