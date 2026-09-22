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
/// and back round, while F2 hands over points to spend and credits casts to whatever is
/// already mastered, so a skill can also be walked up the ranks the way the player will.
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

    /// <summary>
    /// Casts credited per press of F2 to every skill that is already mastered.
    /// </summary>
    /// <remarks>
    /// Sized so the ranks arrive a press at a time rather than all at once: 250 puts a
    /// mastered skill at Grand Master on the first press and Perfect on the third, which is
    /// the point of the key — watching a rank land, not arriving at the last one.
    /// </remarks>
    [Export] public int Casts { get; set; } = 250;

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

        GD.Print($"[debug] F2 grants {Points} skill points and {Casts} casts to mastered skills"
            + " · F4 cycles every skill through the mastery ranks");
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

    /// <summary>
    /// Points to spend, and casts credited to whatever is already mastered — the two currencies
    /// a rank costs, so one key carries a skill the whole way to Perfect.
    /// </summary>
    /// <remarks>
    /// Only fully invested skills are credited, because that is the rule the book plays by:
    /// casts count from Master onward. Crediting the rest would mean a skill jumping straight
    /// from Normal to Grand Master the moment its seventh point landed, which is not what the
    /// player will ever see.
    /// </remarks>
    private void GrantPoints()
    {
        if (Character() is not { } character) return;

        character.Progression.GrantPoints(skillPoints: Points);

        var book = character.Skills;
        var uses = book.Save();
        var points = book.SavePoints();
        var credited = 0;

        foreach (var (id, spent) in points)
        {
            if (spent < SkillBook.MaxPoints) continue;

            uses[id] = uses.GetValueOrDefault(id) + Casts;
            credited++;
        }

        book.Load(uses, points);

        GD.Print($"[debug] granted {Points} skill points ({character.Progression.UnspentSkillPoints} unspent)"
            + $" and {Casts} casts to {credited} mastered skill(s)"
            + (credited == 0 ? " — nothing is mastered yet, spend seven points on a skill first" : ""));

        if (credited > 0) Announce(book);
    }

    /// <summary>Says where every learned skill now stands, so a press has a visible answer.</summary>
    private void Announce(SkillBook book)
    {
        var counts = new Dictionary<MasteryRank, int>();

        foreach (var id in book.Known)
        {
            var rank = book.RankOf(id);
            counts[rank] = counts.GetValueOrDefault(rank) + 1;
        }

        var parts = new List<string>();

        foreach (var rank in Cycle)
        {
            if (counts.TryGetValue(rank, out var many)) parts.Add($"{many} {UI.Words.Of(rank)}");
        }

        UI.WorldNotice.Show(GetTree(), "[debug] " + string.Join(", ", parts));
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
