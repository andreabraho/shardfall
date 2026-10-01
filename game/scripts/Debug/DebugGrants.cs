using System.Linq;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Game.Input;
using Kiln.Game.Items;

namespace Kiln.Game.Debug;

/// <summary>
/// Hands the player what a system needs to be exercised: every dagger, armour and headpiece of the Ninja
/// (F8, 2026-10-01: all it gives, at your call), and a level at a time (F1).
/// </summary>
/// <remarks>
/// <para>
/// Debug builds only. It frees itself in a release build rather than sitting there as an
/// unbound key waiting to be found.
/// </para>
/// </remarks>
public partial class DebugGrants : Node
{
    public override void _Ready()
    {
        if (!OS.IsDebugBuild())
        {
            QueueFree();
            return;
        }

        GD.Print("[debug] F1 grants a level · F8 grants every dagger, armour and headpiece of the Ninja · F10 unlocks every cosmetic");
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

        // The Ninja's gear and nothing else (2026-10-01): the gan, the materials and the trinkets once
        // handed out here are gone. The largest first, so the bag packs tight and all of it fits; each kind from the lowest level up.
        var rng = GameItems.LootRng.Fork("debug-ninja");
        // What is already worn or in the bag is not handed out again: with it the bag would not hold the rest.
        var owned = bag.Gear.Worn.Values.Concat(bag.Bag.Items.Select(p => p.Item)).Select(i => i.DefId).ToHashSet();
        var gear = GameContent.Database.Items.Values.Where(d => IsNinjaGear(d) && !owned.Contains(d.Id))
            .OrderByDescending(d => d.GridSize[0] * d.GridSize[1]).ThenBy(d => d.LevelReq).ToList();
        var given = gear.Count(def => bag.Bag.TryAdd(GameItems.Factory.Create(def.Id, rng)));

        var report = $"[debug] {given}/{gear.Count} Ninja daggers, armours and headpieces" + (given < gear.Count ? " — the bag is full" : "");

        GD.Print(report);
        UI.WorldNotice.Show(GetTree(), report);
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

    private static bool IsNinjaGear(Kiln.Data.Definitions.ItemDef def) =>
        def.Slot is EquipSlot.Weapon or EquipSlot.Armor or EquipSlot.Helmet && def.ClassRestriction.Contains("blade");

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
