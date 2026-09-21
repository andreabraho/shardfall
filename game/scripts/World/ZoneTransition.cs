using Godot;

namespace Kiln.Game.World;

/// <summary>
/// Moves the player from one map to the next (WLD-01).
/// </summary>
/// <remarks>
/// A zone change replaces the whole scene tree, so the only thing that can survive it is a
/// static: which zone the player came from. The arriving scene reads it to decide where on
/// the ground to put them, so a border has two ends that agree without either scene knowing
/// anything about the other's layout.
/// </remarks>
public static class ZoneTransition
{
    /// <summary>The zone the player just left, or empty on a cold start.</summary>
    public static string ArrivingFrom { get; private set; } = "";

    public static void Begin(SceneTree tree, string fromZone, string toZone)
    {
        var scene = GameWorld.Graph[toZone]?.Scene;

        if (string.IsNullOrEmpty(scene))
        {
            GD.PushError($"[gate] '{toZone}' has no scene to load.");
            return;
        }

        ArrivingFrom = fromZone;
        GD.Print($"[gate] {fromZone} → {toZone}");

        // Deferred: the change tears down the tree, and doing that inside a physics callback
        // from the body that just entered the gate frees the node mid-signal.
        tree.CallDeferred(SceneTree.MethodName.ChangeSceneToFile, scene);
    }

    /// <summary>Consumes the arrival, so re-entering a scene later does not reuse it.</summary>
    public static string TakeArrival()
    {
        var from = ArrivingFrom;
        ArrivingFrom = "";

        return from;
    }

    /// <summary>
    /// Tells the player why a gate refused them, through whichever HUD the scene has.
    /// </summary>
    public static void Announce(SceneTree tree, string message)
    {
        GD.Print($"[gate] refused: {message}");
        Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndRefused);
        UI.WorldNotice.Show(tree, message);
    }
}
