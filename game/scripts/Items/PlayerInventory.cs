using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.Items;

namespace Kiln.Game.Items;

/// <summary>
/// The player's bag, worn gear and purse, and the bridge that turns them into stats.
/// </summary>
/// <remarks>
/// Lives on the player rather than in a static so a future second character, or a companion
/// with its own gear, needs no restructuring. Everything it owns is engine-free
/// <c>Kiln.Core</c> state; this node only wires it to the scene tree.
/// </remarks>
public partial class PlayerInventory : Node
{
    [Export] public int GridWidth { get; set; } = 10;
    [Export] public int GridHeight { get; set; } = 8;

    /// <summary>Starting purse. Enough to reach the first upgrade without begging.</summary>
    [Export] public int StartingYang { get; set; } = 12_000;

    public Inventory Bag { get; private set; } = null!;

    public Equipment Gear { get; private set; } = null!;

    [Signal] public delegate void ChangedEventHandler();

    /// <summary>Raised when something is picked up, so the HUD can say what it was.</summary>
    [Signal] public delegate void PickedUpEventHandler(string description);

    public override void _Ready()
    {
        if (!GameItems.IsLoaded)
        {
            GD.PushError("PlayerInventory: GameItems is not loaded.");
            return;
        }

        // Adopted from the session, not built here: a zone gate replaces the scene tree, and
        // a bag owned by this node would be a fresh empty bag on the far side of every gate.
        var isNewCharacter = !PlayerProfile.Exists;

        Bag = PlayerProfile.AdoptBag(() => new Inventory(GameItems.Catalogue, GridWidth, GridHeight, StartingYang));
        Gear = PlayerProfile.AdoptGear(() => new Equipment(GameItems.Catalogue));

        Bag.Changed += () => EmitSignal(SignalName.Changed);
        Gear.Changed += () =>
        {
            ApplyToStats();
            EmitSignal(SignalName.Changed);
        };

        if (isNewCharacter) CallDeferred(nameof(GiveStartingGear));

        // Worn gear survives the journey, but the stat block it feeds is rebuilt with the
        // scene, so it has to be re-applied on arrival rather than only when gear changes.
        CallDeferred(nameof(ApplyToStats));

        Debug.DebugOverlay.Register("yang", this, () => $"{Bag.Yang:N0} ({Bag.FreeCells} free cells)");
    }

    /// <summary>
    /// Starting kit for the test character. A real new game starts empty and the prologue
    /// hands the first weapon over; this exists so the gear systems can be exercised at all.
    /// </summary>
    private void GiveStartingGear()
    {
        var rng = GameItems.LootRng.Fork("starting-gear");

        Equip(GameItems.Factory.Create("wpn_iron_sword", rng));
        Equip(GameItems.Factory.Create("arm_leather_vest", rng));

        Bag.TryAdd(GameItems.Factory.CreatePlain("mat_iron_scrap", 30));
        Bag.TryAdd(GameItems.Factory.CreatePlain("mat_tempering_oil", 4));
        Bag.TryAdd(GameItems.Factory.CreatePlain("mat_boring_stone", 2));
        Bag.TryAdd(GameItems.Factory.CreatePlain("mat_mutation_ink", 3));
        Bag.TryAdd(GameItems.Factory.CreatePlain("stn_ember", 1));

        ApplyToStats();
        EmitSignal(SignalName.Changed);
    }

    // ------------------------------------------------------------------ loot

    /// <summary>
    /// Takes a kill's reward. Returns whatever would not fit, so the caller can leave it on
    /// the ground rather than deleting it.
    /// </summary>
    public System.Collections.Generic.List<ItemInstance> Take(LootRoll loot)
    {
        var rejected = new System.Collections.Generic.List<ItemInstance>();

        if (loot.Yang > 0) Bag.AddYang(loot.Yang);

        foreach (var item in loot.Items)
        {
            if (Bag.TryAdd(item))
            {
                EmitSignal(SignalName.PickedUp, GameItems.NameOf(item));
                continue;
            }

            rejected.Add(item);
        }

        if (rejected.Count > 0) EmitSignal(SignalName.PickedUp, "Bag full");

        return rejected;
    }

    public bool TryPickUp(ItemInstance item)
    {
        if (!Bag.TryAdd(item)) return false;

        EmitSignal(SignalName.PickedUp, GameItems.NameOf(item));
        return true;
    }

    // ------------------------------------------------------------------ gear

    /// <summary>Wears an item from the bag, putting whatever it replaces back in the bag.</summary>
    public EquipOutcome Equip(ItemInstance item)
    {
        var character = GetParent().GetNodeOrNull<Player.PlayerCharacter>("PlayerCharacter");
        var level = character?.Progression.Level ?? 1;

        var outcome = Gear.TryEquip(item, level, CharacterClass.Warrior, out var displaced);

        if (outcome != EquipOutcome.Equipped) return outcome;

        Bag.Remove(item);

        // If the replaced item has nowhere to go the swap is undone rather than silently
        // eating it — a full bag must never cost the player their old weapon.
        if (displaced is not null && !Bag.TryAdd(displaced))
        {
            Gear.TryEquip(displaced, level, CharacterClass.Warrior, out _);
            Bag.TryAdd(item);

            return EquipOutcome.NotEquipment;
        }

        return EquipOutcome.Equipped;
    }

    public void Unequip(EquipSlot slot)
    {
        if (Gear.In(slot) is not { } item) return;

        if (!Bag.TryAdd(item)) return;

        Gear.Unequip(slot);
    }

    /// <summary>Pushes gear into the character's stat block.</summary>
    public void ApplyToStats() =>
        GetParent().GetNodeOrNull<Player.PlayerCharacter>("PlayerCharacter")?.RefreshStats();
}
