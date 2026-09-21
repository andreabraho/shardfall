using System.Collections.Generic;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.Items;
using Kiln.Data.Definitions;
using Kiln.Data.Items;

namespace Kiln.Game.Items;

/// <summary>
/// The session's item catalogue and the factory that mints rolled copies.
/// <para>
/// Built once after content loads, alongside <see cref="GameContent"/>. Everything that
/// creates an item goes through here so uids stay unique and every roll comes off the same
/// seeded stream.
/// </para>
/// </summary>
public static class GameItems
{
    private static ItemCatalogue? _catalogue;
    private static ItemFactory? _factory;
    private static DeterministicRng? _lootRng;

    public static bool IsLoaded => _catalogue is not null;

    public static ItemCatalogue Catalogue =>
        _catalogue ?? throw new System.InvalidOperationException(
            "GameItems.Load() must run after GameContent.Load().");

    public static ItemFactory Factory =>
        _factory ?? throw new System.InvalidOperationException("GameItems.Load() must run first.");

    /// <summary>
    /// Loot's own RNG stream. Forked from the session seed so a drop roll can never shift a
    /// damage roll — the two systems would otherwise interfere in ways that make a bug
    /// report impossible to reproduce.
    /// </summary>
    public static DeterministicRng LootRng =>
        _lootRng ??= new DeterministicRng(GameSession.Seed).Fork("loot");

    /// <summary>
    /// Crafting's stream — the anvil, the socket bench, the reroll table.
    /// <para>
    /// Held rather than forked per call. A fork is deterministic in its label, so
    /// <c>Fork("anvil")</c> on every attempt would hand back the same stream every time and
    /// every upgrade would produce the same outcome, which would quietly make the pity
    /// counter meaningless.
    /// </para>
    /// </summary>
    public static DeterministicRng CraftRng =>
        _craftRng ??= new DeterministicRng(GameSession.Seed).Fork("craft");

    /// <summary>
    /// Encounters — shard modifier rolls and wave composition. Separate from loot so arming a
    /// shard cannot shift what the next kill drops, which would make a loot bug impossible to
    /// reproduce from a seed.
    /// </summary>
    public static DeterministicRng EncounterRng =>
        _encounterRng ??= new DeterministicRng(GameSession.Seed).Fork("encounter");

    private static DeterministicRng? _craftRng;
    private static DeterministicRng? _encounterRng;

    public static void Load()
    {
        _catalogue = new ItemCatalogue(GameContent.Database);
        _factory = new ItemFactory(_catalogue, _catalogue);
        _lootRng = null;
        _craftRng = null;
        _encounterRng = null;

        GD.Print($"[items] catalogue ready — {_catalogue.Specs.Count} item kinds");
    }

    public static ItemSpec? Spec(string id) => _catalogue is not null && _catalogue.TryGet(id, out var spec) ? spec : null;

    /// <summary>Display name for an item, resolving the localisation key for now by trimming it.</summary>
    public static string NameOf(ItemInstance item)
    {
        var name = GameContent.IsLoaded && GameContent.Database.Items.TryGetValue(item.DefId, out var def)
            ? Localise(def.Name)
            : item.DefId;

        return item.UpgradeLevel > 0 ? $"{name} +{item.UpgradeLevel}" : name;
    }

    /// <summary>Display name for an item definition, when there is no instance to name.</summary>
    public static string NameOfId(string itemId) =>
        GameContent.IsLoaded && GameContent.Database.Items.TryGetValue(itemId, out var def)
            ? Localise(def.Name)
            : itemId;

    /// <summary>Display name for an enemy definition.</summary>
    public static string NameOfEnemy(string enemyId) =>
        GameContent.IsLoaded && GameContent.Database.Enemies.TryGetValue(enemyId, out var def)
            ? Localise(def.Name)
            : enemyId;

    /// <summary>
    /// Stand-in for the localisation table (Phase 11). Turns "$item.wpn_iron_sword.name" into
    /// "Iron Sword" so the UI is readable while the real strings do not exist yet.
    /// </summary>
    public static string Localise(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        if (!key.StartsWith('$')) return key;

        var parts = key.Split('.');
        var stem = parts.Length >= 2 ? parts[^2] : key.TrimStart('$');

        // Ids read prefix_word_word; drop the prefix and title-case the rest.
        var words = stem.Split('_');
        var text = new System.Text.StringBuilder();

        for (var i = 1; i < words.Length; i++)
        {
            if (words[i].Length == 0) continue;

            if (text.Length > 0) text.Append(' ');

            text.Append(char.ToUpperInvariant(words[i][0]));
            text.Append(words[i][1..]);
        }

        return text.Length > 0 ? text.ToString() : stem;
    }
}

/// <summary>What a kill produced.</summary>
public sealed record LootRoll(long Yang, IReadOnlyList<ItemInstance> Items);

/// <summary>
/// Turns a drop table into actual items (ITM-03).
/// </summary>
/// <remarks>
/// Each entry rolls its own chance independently, which is what makes a table readable: a
/// 4% sword is a 4% sword regardless of what else is in the table. <see cref="RollOne"/>
/// exists for guaranteed drops — a shard always gives something — and is the only place the
/// relative weights matter.
/// </remarks>
public static class LootRoller
{
    public static LootRoll Roll(DropTableDef? table, DeterministicRng rng)
    {
        if (table is null) return new LootRoll(0, []);

        var yang = table.YangRange.Length > 1
            ? rng.NextIntInclusive(table.YangRange[0], table.YangRange[1])
            : 0;

        var items = new List<ItemInstance>();

        foreach (var entry in table.Entries)
        {
            if (!rng.Chance(entry.Chance)) continue;

            var count = entry.CountRange.Length > 1
                ? rng.NextIntInclusive(entry.CountRange[0], entry.CountRange[1])
                : 1;

            items.Add(Mint(entry.Item, count, rng));
        }

        return new LootRoll(yang, items);
    }

    /// <summary>Exactly one item, chosen by weight. For drops that are guaranteed to give something.</summary>
    public static ItemInstance? RollOne(DropTableDef? table, DeterministicRng rng)
    {
        if (table is null || table.Entries.Length == 0) return null;

        var weights = new double[table.Entries.Length];

        for (var i = 0; i < table.Entries.Length; i++) weights[i] = table.Entries[i].Weight;

        var entry = table.Entries[rng.WeightedIndex(weights)];
        var count = entry.CountRange.Length > 1
            ? rng.NextIntInclusive(entry.CountRange[0], entry.CountRange[1])
            : 1;

        return Mint(entry.Item, count, rng);
    }

    private static ItemInstance Mint(string itemId, int count, DeterministicRng rng)
    {
        var spec = GameItems.Spec(itemId);

        // Equipment is rolled — bonus lines and socket slots; everything else is a plain
        // stack and must not burn RNG, or adding a material to a table would shift every
        // later roll in the session.
        return spec is { IsEquipment: true }
            ? GameItems.Factory.Create(itemId, rng, count)
            : GameItems.Factory.CreatePlain(itemId, count);
    }
}
