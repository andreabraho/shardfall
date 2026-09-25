using System.Collections.Generic;
using Godot;

namespace Kiln.Game.Visual;

/// <summary>
/// Gives a model the animations that ship apart from it (REF-22).
/// </summary>
/// <remarks>
/// The KayKit 2.0 characters come without animations: the clips live in separate files, one
/// per theme (general, movement, melee), all made for the same skeleton. Each file's clips
/// are gathered once into one library and handed to an AnimationPlayer on the model, rooted
/// where the clips expect it — the files and the characters share the node layout
/// "Rig_Medium/Skeleton3D", so every track finds its bone.
/// </remarks>
public static class AnimationLibraries
{
    private static readonly Dictionary<string, AnimationLibrary> Cache = [];

    /// <summary>Adds the clips of every file in <paramref name="paths"/> to the model.</summary>
    public static void Attach(Node3D model, IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return;

        var library = Gather(paths);

        if (library.GetAnimationList().Count == 0) return;

        var player = model.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

        if (player is null)
        {
            // The same place the animation files keep theirs: a child of the root, driving
            // the tree from the root down.
            player = new AnimationPlayer { Name = "AnimationPlayer", RootNode = new NodePath("..") };
            model.AddChild(player);
        }

        if (player.HasAnimationLibrary("")) player.RemoveAnimationLibrary("");

        player.AddAnimationLibrary("", library);
    }

    private static AnimationLibrary Gather(IReadOnlyList<string> paths)
    {
        var key = string.Join("|", paths);

        if (Cache.TryGetValue(key, out var known)) return known;

        var library = new AnimationLibrary();

        foreach (var path in paths)
        {
            if (ResourceLoader.Load<PackedScene>(path)?.Instantiate() is not Node source)
            {
                GD.PushWarning($"[visual] animation file '{path}' did not load.");
                continue;
            }

            foreach (var player in source.FindChildren("*", "AnimationPlayer", true, false))
            {
                if (player is not AnimationPlayer from) continue;

                foreach (var name in from.GetAnimationList())
                {
                    // Every file carries its own T-pose; the first is enough.
                    if (library.HasAnimation(name)) continue;

                    library.AddAnimation(name, from.GetAnimation(name));
                }
            }

            source.Free();
        }

        Cache[key] = library;
        return library;
    }
}
