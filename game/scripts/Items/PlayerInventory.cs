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
    /// <summary>The bag as the original lays it out (REF-10): five columns, nine rows a page, two pages.</summary>
    public const int Columns = 5;

    public const int PageRows = 9;

    public const int Pages = 2;

    /// <summary>An empty bag of the player's shape.</summary>
    public static Inventory NewBag(long yang = 0) => new(GameItems.Catalogue, Columns, PageRows * Pages, yang, PageRows);

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

        Bag = PlayerProfile.AdoptBag(() => NewBag(StartingYang));
        Gear = PlayerProfile.AdoptGear(() => new Equipment(GameItems.Catalogue));

        Bag.Changed += OnBagChanged;
        Gear.Changed += OnGearChanged;

        if (isNewCharacter) CallDeferred(nameof(GiveStartingGear));

        // Worn gear survives the journey, but the stat block it feeds is rebuilt with the
        // scene, so it has to be re-applied on arrival rather than only when gear changes.
        CallDeferred(nameof(ApplyToStats));
    }

    /// <summary>
    /// Lets go of the bag and the gear before this node is freed.
    /// </summary>
    /// <remarks>
    /// The bag outlives the scene and this node does not, so a subscription left behind is a
    /// dead node the bag still calls. Every border crossing added one, and the next change to
    /// the bag then threw <c>ObjectDisposedException</c> out of the middle of whatever caused
    /// it — which is worse than it sounds, because the throw aborts the caller: a shard that
    /// had just granted its rewards never finished breaking, and a drop the player walked
    /// over was never freed. One leak, two bugs that look unrelated.
    /// <para>
    /// The handlers are named methods rather than lambdas for exactly this reason: a lambda
    /// cannot be unsubscribed.
    /// </para>
    /// </remarks>
    public override void _ExitTree()
    {
        if (Bag is not null) Bag.Changed -= OnBagChanged;
        if (Gear is not null) Gear.Changed -= OnGearChanged;
    }

    private void OnBagChanged() => EmitSignal(SignalName.Changed);

    /// <summary>Every pickup makes the same small sound, whatever path it came in by.</summary>
    private void Picked(string description)
    {
        Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndPickup);
        EmitSignal(SignalName.PickedUp, description);
    }

    private void OnGearChanged()
    {
        ApplyToStats();
        EmitSignal(SignalName.Changed);
    }

    /// <summary>
    /// Puts what is worn on the character's model (REF-22): the blade, the helmet, the cape
    /// over epic armour, the glow of a blade at +7 and above. Not the shield, which is never
    /// drawn (2026-09-25).
    /// </summary>
    public void ShowGear()
    {
        if (GetParent().GetNodeOrNull<Visual.VisualRoot>("VisualRoot") is not { } visual) return;

        var weapon = Gear.In(EquipSlot.Weapon);
        var armour = Gear.In(EquipSlot.Armor);

        visual.Wear(new Visual.GearLook(
            Hands: weapon is null ? 0 : GameItems.Spec(weapon.DefId)?.Hands ?? 1,
            Upgrade: weapon?.UpgradeLevel ?? 0,
            Helmet: Gear.In(EquipSlot.Helmet) is not null,
            Cape: armour is not null && GameItems.Spec(armour.DefId)?.Rarity >= Rarity.Epic,
            SwordSkin: Cosmetics.Worn(Cosmetics.Sword),
            ArmourSkin: Cosmetics.Worn(Cosmetics.Armour)));
    }

    /// <summary>
    /// Starting kit for the test character. A real new game starts empty and the prologue
    /// hands the first weapon over; this exists so the gear systems can be exercised at all.
    /// </summary>
    private void GiveStartingGear()
    {
        var rng = GameItems.LootRng.Fork("starting-gear");

        // Level-one pieces. The iron sword and leather vest this used to hand out became level 8
        // and 6 in the item pass, so a new character's equip was refused and both vanished.
        // Anything that cannot be worn now goes in the bag instead of nowhere.
        foreach (var id in new[] { "wpn_worn_blade", "arm_padded_coat" })
        {
            var piece = GameItems.Factory.Create(id, rng);

            if (Equip(piece) != EquipOutcome.Equipped) Bag.TryAdd(piece);
        }

        // Enough draughts to learn what they are for before the first walk back to the
        // merchant (REF-02).
        Bag.TryAdd(GameItems.Factory.CreatePlain(Player.HealthFlask.DraughtId, 3));

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
                Picked(GameItems.NameOf(item));
                continue;
            }

            rejected.Add(item);
        }

        if (rejected.Count > 0) EmitSignal(SignalName.PickedUp, L10n.T("Bag full"));

        return rejected;
    }

    public bool TryPickUp(ItemInstance item)
    {
        if (!Bag.TryAdd(item))
        {
            // Picking up is asked for now (REF-09), so a refusal needs saying.
            EmitSignal(SignalName.PickedUp, L10n.T("Bag full"));
            return false;
        }

        Picked(GameItems.NameOf(item));
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

    /// <summary>Takes off what is worn in a slot, into the bag. False when the bag has no room.</summary>
    public bool Unequip(EquipSlot slot)
    {
        if (Gear.In(slot) is not { } item) return false;

        if (!Bag.TryAdd(item)) return false;

        Gear.Unequip(slot);
        return true;
    }

    /// <summary>Takes off what is worn in a slot, into one spot of the bag — a drag onto it.</summary>
    public bool UnequipTo(EquipSlot slot, int x, int y)
    {
        if (Gear.In(slot) is not { } item) return false;

        if (!Bag.TryPlace(item, x, y)) return false;

        Gear.Unequip(slot);
        return true;
    }

    /// <summary>
    /// Pushes gear into the character's stat block, and onto its model — an upgrade at the
    /// bench changes both, and the bench only calls this.
    /// </summary>
    public void ApplyToStats()
    {
        GetParent().GetNodeOrNull<Player.PlayerCharacter>("PlayerCharacter")?.RefreshStats();
        ShowGear();
    }
}
