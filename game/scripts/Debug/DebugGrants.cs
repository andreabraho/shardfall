using System.Linq;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Game.Input;
using Kiln.Game.Items;

namespace Kiln.Game.Debug;

/// <summary>
/// Hands the player what a system needs to be exercised: every armour and headpiece of the class played
/// (F8, 2026-10-01: all it gives, at your call; the bag emptied first since 2026-10-02), and a level at a time (F1).
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

        GD.Print("[debug] F1 grants a level · F8 empties the bag and grants every armour and headpiece of the class played, then its weapons, in turn · F10 unlocks every cosmetic");
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

        // The armours and headpieces of the class played and nothing else (2026-10-01, the Ninja's; every class's since 2026-10-02; the daggers too at first, then taken off at your call): the gan, the materials and the trinkets once
        // handed out here are gone. The largest first, so the bag packs tight and all of it fits; each kind from the lowest level up.
        var rng = GameItems.LootRng.Fork("debug-ninja");
        // The bag is emptied first (2026-10-02, at your call), so all of it always fits; what is worn stays worn.
        foreach (var placed in bag.Bag.Items.ToList()) bag.Bag.Remove(placed.Item);

        // Armour and weapons do not fit the bag together (2026-10-02, the weapons at your call): one press gives the
        // armours and headpieces, the next the weapons, and so on in turn.
        var weapons = _weaponsNext;
        _weaponsNext = !_weaponsNext;

        var gear = GameContent.Database.Items.Values.Where(d => weapons ? IsClassWeapon(d) : IsClassGear(d))
            .OrderByDescending(d => d.GridSize[0] * d.GridSize[1]).ThenBy(d => d.LevelReq).ToList();
        var given = gear.Count(def => bag.Bag.TryAdd(GameItems.Factory.Create(def.Id, rng)));

        var report = $"[debug] {given}/{gear.Count} {(weapons ? "weapons" : "armours and headpieces")} of the {PlayerProfile.ClassName}"
            + (given < gear.Count ? " — the bag is full" : "") + $" · F8 again for the {(weapons ? "armours" : "weapons")}";

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

    /// <summary>True when the next press of F8 gives the weapons, false when the armours.</summary>
    private bool _weaponsNext;

    /// <summary>A weapon of the class played.</summary>
    private static bool IsClassWeapon(Kiln.Data.Definitions.ItemDef def) =>
        def.Slot is EquipSlot.Weapon && def.ClassRestriction.Contains(PlayerProfile.Class.ToString().ToLowerInvariant());

    /// <summary>An armour or a headpiece of the class played (2026-10-02: every class has its own).</summary>
    private static bool IsClassGear(Kiln.Data.Definitions.ItemDef def) =>
        def.Slot is EquipSlot.Armor or EquipSlot.Helmet && def.ClassRestriction.Contains(PlayerProfile.Class.ToString().ToLowerInvariant());

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
