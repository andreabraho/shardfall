using Godot;
using Kiln.Game.Input;
using Kiln.Game.Items;

namespace Kiln.Game.Debug;

/// <summary>
/// Hands the player what a system needs to be exercised: yang and materials (F8), and a
/// level at a time (F1).
/// </summary>
/// <remarks>
/// The upgrade ladder cannot be judged on a starting purse: reaching the first fallible rung
/// takes a few thousand yang, and seeing the pity counter do its job takes several attempts
/// at a rung that fails more often than not. Without this, testing the anvil means an hour of
/// killing wolves first, which is exactly the grind the design is trying to avoid — and it
/// would be testing the drop rate rather than the anvil.
/// <para>
/// Debug builds only. It frees itself in a release build rather than sitting there as an
/// unbound key waiting to be found.
/// </para>
/// </remarks>
public partial class DebugGrants : Node
{
    [Export] public int Yang { get; set; } = 250_000;

    private static readonly (string Id, int Count)[] Kit =
    [
        ("mat_iron_scrap", 60),
        ("mat_tempering_oil", 20),
        ("mat_steel_core", 20),
        ("mat_shard_essence", 10),
        ("mat_radiant_core", 3),
        ("mat_boring_stone", 6),
        ("mat_mutation_ink", 12),
        ("stn_ember", 3),
        ("stn_granite", 3),
        ("stn_falcon", 2),
        ("stn_bane_undead", 2),
    ];

    public override void _Ready()
    {
        if (!OS.IsDebugBuild())
        {
            QueueFree();
            return;
        }

        GD.Print("[debug] F1 grants a level · F8 grants test yang and crafting materials");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(GameActions.DebugGrantLevel))
        {
            GetViewport().SetInputAsHandled();
            GrantLevel();
            return;
        }

        if (!@event.IsActionPressed(GameActions.DebugGrantResources)) return;

        GetViewport().SetInputAsHandled();

        if (GetTree().GetFirstNodeInGroup("player") is not Node player) return;

        var bag = player.GetNodeOrNull<PlayerInventory>("PlayerInventory");

        if (bag is null || !GameItems.IsLoaded) return;

        bag.Bag.AddYang(Yang);

        var refused = 0;

        foreach (var (id, count) in Kit)
        {
            // TryGrant reports a full bag rather than dropping the stack, so a cramped bag
            // shows up as a number here instead of as materials that silently never arrived.
            if (!bag.Bag.TryGrant(id, count)) refused++;
        }

        GD.Print($"[debug] granted {Yang:N0} yang and {Kit.Length - refused}/{Kit.Length} material stacks"
            + (refused > 0 ? " — bag is full" : ""));
    }

    /// <summary>
    /// Exactly the experience the next level still wants, so one press is one level.
    /// </summary>
    /// <remarks>
    /// Granted through the character rather than written onto the progression, so everything a
    /// real level-up does happens too: the stat block is rebuilt, health and mana fill, the
    /// points land and the level-up is announced. A level that skipped all that would be a
    /// level the game has never actually seen.
    /// </remarks>
    private void GrantLevel()
    {
        if (GetTree().GetFirstNodeInGroup("player") is not Node player) return;

        var character = player.GetNodeOrNull<Player.PlayerCharacter>("PlayerCharacter");

        if (character is null) return;

        if (character.Progression.IsMaxLevel)
        {
            GD.Print($"[debug] already at the cap (level {character.Progression.Level})");
            return;
        }

        var owed = character.Progression.ExperienceForNextLevel - character.Progression.Experience;

        character.GrantExperience(System.Math.Max(1, owed));

        GD.Print($"[debug] level {character.Progression.Level}"
            + $" — {character.Progression.UnspentAttributePoints} attribute and"
            + $" {character.Progression.UnspentSkillPoints} skill points unspent");
    }
}
