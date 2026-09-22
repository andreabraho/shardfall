using System.Collections.Generic;
using Godot;
using Kiln.Core.Progression;
using Kiln.Game.Input;

namespace Kiln.Game.Debug;

/// <summary>
/// Pushes the skill book around so mastery can be looked at without earning it (F2, F4).
/// </summary>
/// <remarks>
/// Perfect costs seven points and six hundred casts. That is the right price to pay while
/// playing and an absurd one to pay to check whether the violet reads well against the
/// catacombs floor — so F4 walks every skill through Normal, Master, Grand Master and Perfect
/// and back round, and F2 hands over points to spend.
/// <para>
/// It writes the book through its own save and load, rather than through a back door added to
/// the book for the purpose: a debug tool that needs production code to grow a setter is a
/// debug tool that will one day be the reason something ships wrong.
/// </para>
/// <para>
/// Debug builds only. It frees itself in a release build rather than sitting there as an
/// unbound key waiting to be found.
/// </para>
/// </remarks>
public partial class DebugSkills : Node
{
    /// <summary>Points handed over per press of F2. More than a campaign grants, on purpose.</summary>
    [Export] public int Points { get; set; } = 20;

    private static readonly MasteryRank[] Cycle =
        [MasteryRank.Normal, MasteryRank.Master, MasteryRank.GrandMaster, MasteryRank.Perfect];

    private int _step;

    public override void _Ready()
    {
        if (!OS.IsDebugBuild())
        {
            QueueFree();
            return;
        }

        GD.Print("[debug] F2 grants skill points · F4 cycles every skill through the mastery ranks");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(GameActions.DebugGrantSkillPoints))
        {
            GetViewport().SetInputAsHandled();
            GrantPoints();
            return;
        }

        if (!@event.IsActionPressed(GameActions.DebugCycleMastery)) return;

        GetViewport().SetInputAsHandled();
        CycleMastery();
    }

    private void GrantPoints()
    {
        if (Character() is not { } character) return;

        character.Progression.GrantPoints(skillPoints: Points);

        GD.Print($"[debug] granted {Points} skill points ({character.Progression.UnspentSkillPoints} unspent)");
    }

    /// <summary>
    /// Sets every Warrior skill to the next rank in the cycle, learning them all on the way.
    /// </summary>
    /// <remarks>
    /// Nothing is charged for it — the points the player is holding are left alone — because
    /// this exists to look at the effects, not to test the economy of the skill screen.
    /// </remarks>
    private void CycleMastery()
    {
        if (Character() is not { } character || !GameContent.IsLoaded) return;

        _step = (_step + 1) % Cycle.Length;

        var rank = Cycle[_step];
        var uses = new Dictionary<string, int>(System.StringComparer.Ordinal);
        var points = new Dictionary<string, int>(System.StringComparer.Ordinal);

        foreach (var def in UI.SkillText.WarriorSkills())
        {
            points[def.Id] = rank == MasteryRank.Normal ? 1 : SkillBook.MaxPoints;

            uses[def.Id] = rank switch
            {
                MasteryRank.GrandMaster => SkillBook.GrandMasterUses,
                MasteryRank.Perfect => SkillBook.PerfectUses,
                _ => 0,
            };
        }

        character.Skills.Load(uses, points);

        GD.Print($"[debug] every skill set to {rank}"
            + (rank == MasteryRank.Normal ? " (one point each)" : $" ({SkillBook.MaxPoints} points each)"));

        UI.WorldNotice.Show(GetTree(), $"[debug] skills: {UI.Words.Of(rank)}");
    }

    private Player.PlayerCharacter? Character() =>
        GetTree().GetFirstNodeInGroup("player") is Node player
            ? player.GetNodeOrNull<Player.PlayerCharacter>("PlayerCharacter")
            : null;
}
