using Kiln.Core.Combat;
using Kiln.Core.Foundation;

namespace Kiln.Core.Items;

public enum EquipOutcome
{
    Equipped,
    NotEquipment,
    WrongClass,
    LevelTooLow,
    AlreadyEquipped,
}

/// <summary>
/// What the character is wearing, and the one place gear turns into stats (ITM-05, PRG-01).
/// </summary>
/// <remarks>
/// Aggregation is recomputed from scratch on every change rather than adjusted incrementally.
/// Gear swaps happen a few hundred times in a playthrough and the recompute is a few dozen
/// additions, so the incremental version would buy nothing and cost the classic bug where
/// unequipping leaves a stat behind.
/// </remarks>
public sealed class Equipment
{
    private readonly IItemSpecs _specs;
    private readonly Dictionary<EquipSlot, ItemInstance> _worn = [];

    public Equipment(IItemSpecs specs) => _specs = specs;

    public IReadOnlyDictionary<EquipSlot, ItemInstance> Worn => _worn;

    public event Action? Changed;

    public ItemInstance? In(EquipSlot slot) => _worn.GetValueOrDefault(slot);

    public EquipOutcome CanEquip(ItemInstance item, int level, CharacterClass cls, out EquipSlot slot)
    {
        slot = default;

        if (!_specs.TryGet(item.DefId, out var spec) || spec.Slot is not { } itemSlot) return EquipOutcome.NotEquipment;

        slot = itemSlot;

        if (!spec.AllowedFor(cls)) return EquipOutcome.WrongClass;
        if (level < spec.LevelReq) return EquipOutcome.LevelTooLow;
        if (ReferenceEquals(In(itemSlot), item)) return EquipOutcome.AlreadyEquipped;

        return EquipOutcome.Equipped;
    }

    /// <summary>
    /// Wears the item, handing back whatever it replaced so the caller can put it in the bag.
    /// </summary>
    public EquipOutcome TryEquip(ItemInstance item, int level, CharacterClass cls, out ItemInstance? displaced)
    {
        displaced = null;

        var verdict = CanEquip(item, level, cls, out var slot);

        if (verdict != EquipOutcome.Equipped) return verdict;

        displaced = In(slot);
        _worn[slot] = item;
        Changed?.Invoke();

        return EquipOutcome.Equipped;
    }

    public ItemInstance? Unequip(EquipSlot slot)
    {
        if (!_worn.Remove(slot, out var item)) return null;

        Changed?.Invoke();

        return item;
    }

    /// <summary>Total modifiers from every worn piece, including sockets and rolled lines.</summary>
    public StatModifiers Modifiers()
    {
        var total = new StatModifiers();

        foreach (var item in _worn.Values)
        {
            if (!_specs.TryGet(item.DefId, out var spec)) continue;

            total.Add(item.ModifiersFor(spec, _specs));
        }

        return total;
    }

    /// <summary>Average weapon damage of the equipped weapon, scaled by its upgrade level.</summary>
    public double WeaponDamage()
    {
        if (In(EquipSlot.Weapon) is not { } weapon || !_specs.TryGet(weapon.DefId, out var spec)) return 0;

        return (weapon.WeaponDamageMin(spec) + weapon.WeaponDamageMax(spec)) / 2.0;
    }

    public (double Min, double Max) WeaponDamageRange()
    {
        if (In(EquipSlot.Weapon) is not { } weapon || !_specs.TryGet(weapon.DefId, out var spec)) return (0, 0);

        return (weapon.WeaponDamageMin(spec), weapon.WeaponDamageMax(spec));
    }

    public double ArmorValue()
    {
        var total = 0.0;

        foreach (var item in _worn.Values)
        {
            if (_specs.TryGet(item.DefId, out var spec)) total += item.ArmorValue(spec);
        }

        return total;
    }

    /// <summary>
    /// Pushes gear into a stat block. <paramref name="baseModifiers"/> carries anything that is
    /// not gear — buffs, difficulty, temporary effects — so applying equipment never erases it.
    /// </summary>
    public void ApplyTo(StatBlock stats, StatModifiers? baseModifiers = null)
    {
        var mods = baseModifiers?.Clone() ?? new StatModifiers();
        mods.Add(Modifiers());

        stats.Modifiers = mods;
        stats.WeaponDamage = WeaponDamage();
        stats.ArmorValue = ArmorValue();
    }

    public Dictionary<EquipSlot, long> Save() => _worn.ToDictionary(p => p.Key, p => p.Value.Uid);

    public void Load(IReadOnlyDictionary<EquipSlot, ItemInstance> worn)
    {
        _worn.Clear();

        foreach (var (slot, item) in worn) _worn[slot] = item;

        Changed?.Invoke();
    }
}
