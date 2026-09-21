namespace Kiln.Core.Saving;

/// <summary>
/// Everything a save file holds (UIX-01, doc 06 §9), as plain data.
/// </summary>
/// <remarks>
/// Deliberately made of strings, numbers and lists rather than the game's own types. The live
/// objects change shape every few weeks; a save written last month has to keep loading. A
/// flat record is something a migration can reason about field by field, and a live
/// <c>ItemInstance</c> is not.
/// <para>
/// Fields added since version 1 — quests, play time, shard timers, merchants — came without a
/// version bump because each is new and defaults cleanly: a save from before them loads as a
/// character with nothing finished, every shard standing and every shelf full, which is what
/// that save knew.
/// </para>
/// </remarks>
public sealed class SaveGame
{
    public int SaveVersion { get; set; } = SaveCodec.CurrentVersion;
    public string CreatedUtc { get; set; } = "";
    public string Label { get; set; } = "";

    /// <summary><c>Wanderer</c>, <c>Disciple</c>, <c>Adept</c> or <c>Shardbound</c>.</summary>
    public string Difficulty { get; set; } = "Disciple";

    /// <summary>Seconds played in a map, not counting menus and pauses. Shard timers run on it.</summary>
    public double PlayTime { get; set; }

    public ulong Seed { get; set; }

    public SavedPlayer Player { get; set; } = new();
    public SavedWorld World { get; set; } = new();
    public List<string> CompletedQuests { get; set; } = [];

    /// <summary>The quest being pursued when the game was saved, or null.</summary>
    /// <remarks>
    /// Advisory: the chain works out which quest is active from what is finished, and uses
    /// this only to decide whether <see cref="QuestProgress"/> still applies.
    /// </remarks>
    public string? ActiveQuest { get; set; }

    /// <summary>Progress on each goal of the active quest, in goal order.</summary>
    public List<int> QuestProgress { get; set; } = [];
}

public sealed class SavedPlayer
{
    public int Level { get; set; } = 1;
    public long Experience { get; set; }
    public int AttributePoints { get; set; }
    public int SkillPoints { get; set; }

    /// <summary>Assigned points, in the order Str, Dex, Int, Vit.</summary>
    public int[] Assigned { get; set; } = [0, 0, 0, 0];

    public Dictionary<string, int> Skills { get; set; } = new(StringComparer.Ordinal);

    public double Health { get; set; } = 1.0;
    public double Mana { get; set; } = 1.0;
    public int FlaskCharges { get; set; } = -1;

    public long Yang { get; set; }

    /// <summary>
    /// Next uid the item factory hands out.
    /// </summary>
    /// <remarks>
    /// Saved so that an item made after loading can never share an id with one that was
    /// already in the bag — the equipment slots refer to items by uid, and two swords with
    /// the same one is a bug that only shows up when you take the wrong one off.
    /// </remarks>
    public long NextUid { get; set; } = 1;

    public long NextGrantUid { get; set; } = -1;

    /// <summary>Every item the character owns, worn or carried, once each.</summary>
    public List<SavedItem> Items { get; set; } = [];

    /// <summary>Slot name → uid of the item worn there.</summary>
    public Dictionary<string, long> Worn { get; set; } = new(StringComparer.Ordinal);
}

public sealed class SavedItem
{
    public long Uid { get; set; }
    public string Def { get; set; } = "";
    public int Count { get; set; } = 1;
    public int Upgrade { get; set; }
    public int Failures { get; set; }
    public bool Locked { get; set; }

    /// <summary>Grid position in the bag, or null when the item is worn.</summary>
    public int[]? At { get; set; }

    public List<SavedLine> Lines { get; set; } = [];

    /// <summary>One entry per socket: null closed, "" open and empty, otherwise the stone.</summary>
    public List<string?> Sockets { get; set; } = [];
}

/// <summary>
/// A rolled bonus line, by the pool entry it came from.
/// </summary>
/// <remarks>
/// The line id and the magnitude, not the stat. The stat is looked up again from the pool on
/// load, so retuning what a line does changes old items too — which is what the player
/// expects, since the tooltip describes the line, not the day it was rolled.
/// </remarks>
public sealed class SavedLine
{
    public string Id { get; set; } = "";
    public double Magnitude { get; set; }
    public bool Locked { get; set; }
}

public sealed class SavedWorld
{
    public string Zone { get; set; } = "";

    /// <summary>Where the player was standing, or null to use the zone's own start.</summary>
    public double[]? Position { get; set; }

    public List<string> DiscoveredShrines { get; set; } = [];
    public string? Anchor { get; set; }

    /// <summary>Zone id → deepest checkpoint floor reached (FR-7.20).</summary>
    public Dictionary<string, int> Depths { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Zone id → explored map cells.</summary>
    public Dictionary<string, List<long>> Explored { get; set; } = new(StringComparer.Ordinal);

    /// <summary>"zone:node" of each broken shard → seconds of play until it stands again.</summary>
    public Dictionary<string, double> ShardRespawns { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Merchant id → what has been bought off the shelf and what can be bought back.</summary>
    public Dictionary<string, SavedVendor> Vendors { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// A merchant's memory.
/// </summary>
/// <remarks>
/// The shelf itself is not saved: it is rebuilt from the seed and the level it was stocked for,
/// and comes back identical. What is saved is what the player took off it, and the items they
/// sold, which exist nowhere else.
/// </remarks>
public sealed class SavedVendor
{
    public int StockedFor { get; set; }

    /// <summary>Item ids bought off the shelf since it was last stocked, one entry per piece.</summary>
    public List<string> Bought { get; set; } = [];

    public List<SavedItem> Buyback { get; set; } = [];

    /// <summary>What each buyback item costs, in the same order.</summary>
    public List<long> BuybackPrices { get; set; } = [];
}
