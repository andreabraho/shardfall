using Godot;
using Kiln.Game.Input;

namespace Kiln.Game.Debug;

/// <summary>
/// Marks every quest in the content as finished (F9), so quest-locked borders open.
/// </summary>
/// <remarks>
/// The world graph gates the second half of Act 1 behind quests that the quest system does
/// not yet exist to complete. Without this the maps past the Ridge are unreachable in play:
/// they would have to be judged by loading their scene directly, which skips the arrival
/// point, the carried health and everything else a border is supposed to do.
/// <para>
/// It grants the flag rather than removing the requirement, so the gate, the graph and the
/// data all keep saying exactly what they say today. When quests land, this node goes and
/// nothing else has to change.
/// </para>
/// <para>
/// Debug builds only — it frees itself in a release build rather than sitting there as an
/// unbound key waiting to be found.
/// </para>
/// </remarks>
public partial class DebugPassage : Node
{
    public override void _Ready()
    {
        if (!OS.IsDebugBuild())
        {
            QueueFree();
            return;
        }

        GD.Print("[debug] F9 marks every quest finished, opening quest-locked borders");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed(GameActions.DebugCompleteQuests)) return;

        GetViewport().SetInputAsHandled();

        foreach (var id in Kiln.Data.Ids.Quests.All)
        {
            PlayerProfile.CompletedQuests.Add(id);
        }

        var notice = $"{Kiln.Data.Ids.Quests.All.Length} quests marked finished.";

        GD.Print($"[debug] {notice}");
        World.ZoneTransition.Announce(GetTree(), notice);
    }
}
