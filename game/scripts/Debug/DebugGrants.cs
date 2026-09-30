using System.Linq;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Game.Input;
using Kiln.Game.Items;

namespace Kiln.Game.Debug;

/// <summary>
/// Hands the player what a system needs to be exercised: yang, materials and one of every
/// shield, pair of boots and jewel (F8), and a level at a time (F1).
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

        GD.Print("[debug] F1 grants a level · F8 grants test gan, crafting materials and every shield, pair of boots and jewel · F10 unlocks every cosmetic");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(GameActions.DebugGrantLevel))
        {
            GetViewport().SetInputAsHandled();
            GrantLevel();
            return;
        }

        // Every look the bosses give (REF-23), to try them on without the bosses.
        if (@event.IsActionPressed(GameActions.DebugUnlockCosmetics))
        {
            GetViewport().SetInputAsHandled();
            Cosmetics.UnlockAll();
            UI.WorldNotice.Show(GetTree(), "[debug] every cosmetic unlocked — the wardrobe is in the character window (C)");
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

        // The weapons and armours once handed out here to try each model on are gone: the models
        // are approved and they left no room in the bag (2026-09-30). In their place, one of each
        // shield, pair of boots and piece of jewellery, to see their painted icons.
        var rng = GameItems.LootRng.Fork("debug-trinkets");
        var trinkets = 0;

        foreach (var def in GameContent.Database.Items.Values.Where(IsTrinket).OrderBy(d => d.Slot).ThenBy(d => d.LevelReq))
        {
            if (bag.Bag.TryAdd(GameItems.Factory.Create(def.Id, rng))) trinkets++;
        }

        GD.Print($"[debug] granted {Yang:N0} gan, {Kit.Length - refused}/{Kit.Length} material stacks and {trinkets} shields, boots and jewels"
            + (refused > 0 ? " — bag is full" : ""));
    }

    /// <summary>
    /// Unlocks every look and wears <paramref name="id"/> as <paramref name="kind"/> on the player:
    /// for scripted checks of the cosmetics, which cannot reach the wardrobe themselves.
    /// </summary>
    public void TryOn(string kind, string id)
    {
        Cosmetics.UnlockAll();
        PlayerProfile.Wardrobe.Wear(kind, string.IsNullOrEmpty(id) ? null : id);

        if (GetTree().GetFirstNodeInGroup("player") is Node player) player.GetNodeOrNull<PlayerInventory>("PlayerInventory")?.ShowGear();
    }

    private static bool IsTrinket(Kiln.Data.Definitions.ItemDef def) =>
        def.Slot is { } slot && slot is not (EquipSlot.Weapon or EquipSlot.Armor or EquipSlot.Helmet);

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
