using Kiln.Core.Foundation;

namespace Kiln.Core.Items;

/// <summary>
/// Everything the simulation needs to know about an item <em>kind</em>, as opposed to a
/// particular rolled copy of it (<see cref="ItemInstance"/>).
/// <para>
/// This is the engine-free projection of the JSON <c>ItemDef</c>. Core deliberately does not
/// reference the content assembly: keeping the dependency pointing one way means the whole
/// item system can be unit-tested against three hand-written specs instead of the real
/// content tree, and content changes cannot break simulation tests by accident.
/// </para>
/// </summary>
public sealed record ItemSpec
{
    public required string Id { get; init; }

    /// <summary>Null for anything that is not worn: materials, stones, consumables.</summary>
    public EquipSlot? Slot { get; init; }

    public Rarity Rarity { get; init; } = Rarity.Common;
    public int LevelReq { get; init; }
    public int Width { get; init; } = 1;
    public int Height { get; init; } = 1;

    /// <summary>1 means the item never stacks. Materials stack; equipment never does.</summary>
    public int MaxStack { get; init; } = 1;

    public int SellValue { get; init; }
    public double WeaponDamageMin { get; init; }
    public double WeaponDamageMax { get; init; }
    public double ArmorValue { get; init; }

    /// <summary>Socket slots the item is born with. They start closed — see <see cref="SocketBench"/>.</summary>
    public int Sockets { get; init; }

    public int MinBonusLines { get; init; }
    public int MaxBonusLines { get; init; }
    public string? BonusPoolId { get; init; }
    public string? UpgradePathId { get; init; }
    public IReadOnlyList<CharacterClass> ClassRestriction { get; init; } = [];

    /// <summary>
    /// Modifiers the item grants by itself, before anything is rolled: a ring's flat stats,
    /// or what a socket stone contributes once slotted.
    /// </summary>
    public IReadOnlyList<BonusLine> Grants { get; init; } = [];

    public bool IsEquipment => Slot is not null;

    public bool IsStackable => MaxStack > 1;

    public bool IsUpgradable => UpgradePathId is not null;

    public bool AllowedFor(CharacterClass cls) =>
        ClassRestriction.Count == 0 || ClassRestriction.Contains(cls);

    public int Cells => Width * Height;
}

/// <summary>
/// Lookup of item kinds by id. Implemented over the content database in the game, and over a
/// dictionary in tests.
/// </summary>
public interface IItemSpecs
{
    bool TryGet(string id, out ItemSpec spec);
}

public static class ItemSpecsExtensions
{
    public static ItemSpec Get(this IItemSpecs specs, string id) =>
        specs.TryGet(id, out var spec) ? spec : throw new KeyNotFoundException($"no item spec '{id}'");
}
