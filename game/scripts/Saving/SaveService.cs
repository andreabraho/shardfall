using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Kiln.Core.Combat;
using Kiln.Core.Foundation;
using Kiln.Core.Items;
using Kiln.Core.Saving;
using Kiln.Game.Input;
using Kiln.Game.Items;
using Kiln.Game.World;

namespace Kiln.Game.Saving;

/// <summary>
/// Writes the game to disk and reads it back (UIX-01, FR-11.1).
/// </summary>
/// <remarks>
/// Lives on the autoload, not in a scene, because loading replaces the scene — a service
/// owned by the thing it is about to tear down cannot finish its own job.
/// <para>
/// Two kinds of file: the quick save, which is the player's, and a ring of five autosaves
/// written at every shrine and every border, which is the game's. The ring is what makes an
/// autosave safe to take without asking: the one about to be overwritten is always the
/// oldest, so a bad autosave can never be the only one.
/// </para>
/// <para>
/// Every write goes to a temporary file first and is renamed over the real one only once it
/// is complete (doc 06 §9). A crash in the middle of saving leaves the previous save intact
/// rather than half of a new one.
/// </para>
/// </remarks>
public partial class SaveService : Node
{
    /// <summary>
    /// Where saves live. <c>KILN_SAVE_DIR</c> overrides it.
    /// </summary>
    /// <remarks>
    /// The override exists for headless runs and probes. Without it, every automated run of a
    /// scene would autosave into the player's own folder, and the next launch would continue
    /// from wherever the probe happened to leave its character.
    /// </remarks>
    public static string Folder =>
        OS.GetEnvironment("KILN_SAVE_DIR") is { Length: > 0 } dir ? dir : ProjectSettings.GlobalizePath("user://saves");
    public const string QuickSlot = "quick";
    public const int AutosaveRing = 5;

    /// <summary>Set by a load, consumed by the zone that arrives: where to put the player.</summary>
    private static Vector3? _pendingPosition;

    private static SaveService? _instance;

    /// <summary>
    /// True from the moment a load starts until the loaded zone has placed the player.
    /// </summary>
    /// <remarks>
    /// The scene being replaced still runs its deferred work after the load has swapped the
    /// character out underneath it, and its arrival autosave would record the loaded
    /// character standing at the old scene's start.
    /// </remarks>
    private static bool _loading;

    public override void _Ready()
    {
        _instance = this;
        ProcessMode = ProcessModeEnum.Always;

        // Deferred so the main scene has started loading. Continuing replaces it, and doing
        // that from inside the autoload's own _Ready races the engine's first scene change.
        CallDeferred(nameof(ContinueOnBoot));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // New game first: plain F12 also matches Shift+F12, and the other order would load a
        // save when the player asked to throw the session away.
        if (@event.IsActionPressed(GameActions.NewGame))
        {
            NewGame();
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed(GameActions.QuickSave))
        {
            Save(QuickSlot, "Quick save");
            GetViewport().SetInputAsHandled();
        }
        else if (@event.IsActionPressed(GameActions.QuickLoad))
        {
            LoadLatest();
            GetViewport().SetInputAsHandled();
        }
    }

    // ------------------------------------------------------------------ writing

    /// <summary>Writes an autosave into the oldest slot of the ring.</summary>
    /// <remarks>Static so the world can ask without holding a reference to the autoload.</remarks>
    public static void Autosave(string why)
    {
        if (_loading) return;

        _instance?.Save(OldestAutosave(), why, quiet: true);
    }

    public bool Save(string slot, string label, bool quiet = false)
    {
        var tree = GetTree();

        if (!CanSave(tree, out var reason))
        {
            if (!quiet) UI.WorldNotice.Show(tree, $"Cannot save: {reason}.");
            return false;
        }

        var save = Capture(tree, label);
        var path = PathOf(slot);

        try
        {
            Directory.CreateDirectory(Folder);
            WriteAtomically(path, SaveCodec.Encode(save));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            GD.PushError($"[save] could not write {path}: {ex.Message}");
            UI.WorldNotice.Show(tree, "The save could not be written. See the log.");

            return false;
        }

        GD.Print($"[save] wrote {slot} — {save.World.Zone}, level {save.Player.Level}");

        if (!quiet) UI.WorldNotice.Show(tree, "Saved.");

        return true;
    }

    /// <summary>
    /// Whether this is a moment a save would make sense of.
    /// </summary>
    /// <remarks>
    /// Refused with no player on the ground (mid scene change) and with a dead one: loading a
    /// save of the frame the character died is a save that kills you on load.
    /// </remarks>
    private static bool CanSave(SceneTree tree, out string reason)
    {
        reason = "";

        if (!PlayerProfile.Exists || GameWorld.CurrentZoneId.Length == 0)
        {
            reason = "nothing to save yet";
            return false;
        }

        if (tree.GetFirstNodeInGroup("player")?.GetNodeOrNull<Combat.Combatant>("Combatant") is { IsAlive: false })
        {
            reason = "you are dead";
            return false;
        }

        return true;
    }

    private static void WriteAtomically(string path, string text)
    {
        var full = path;
        var temp = full + ".tmp";

        using (var stream = new FileStream(temp, FileMode.Create, System.IO.FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(text);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }

        File.Move(temp, full, overwrite: true);
    }

    private static SaveGame Capture(SceneTree tree, string label)
    {
        var player = tree.GetFirstNodeInGroup("player") as Node3D;

        // Refreshes the carried health first: the profile only records it at a border, and a
        // save made halfway through a fight must not remember the health you walked in with.
        player?.GetNodeOrNull<Player.PlayerCharacter>("PlayerCharacter")?.CarryOut();

        var progression = PlayerProfile.Progression;
        var assigned = progression.Assigned;
        var items = new List<SavedItem>();

        if (PlayerProfile.Bag is { } bag)
        {
            foreach (var placed in bag.Items)
            {
                items.Add(ItemSnapshots.Capture(placed.Item, [placed.X, placed.Y]));
            }
        }

        var worn = new Dictionary<string, long>(StringComparer.Ordinal);

        if (PlayerProfile.Gear is { } gear)
        {
            foreach (var (slot, item) in gear.Worn)
            {
                items.Add(ItemSnapshots.Capture(item, null));
                worn[slot.ToString()] = item.Uid;
            }
        }

        var position = player?.GlobalPosition;

        return new SaveGame
        {
            CreatedUtc = DateTime.UtcNow.ToString("O"),
            Label = label,
            Difficulty = GameSession.Difficulty.Tier.ToString(),
            Seed = GameSession.Seed,
            CompletedQuests = PlayerProfile.Quests.Completed.ToList(),
            ActiveQuest = PlayerProfile.Quests.Active?.Id,
            QuestProgress = PlayerProfile.Quests.Progress.ToList(),
            Player = new SavedPlayer
            {
                Level = progression.Level,
                Experience = progression.Experience,
                AttributePoints = progression.UnspentAttributePoints,
                SkillPoints = progression.UnspentSkillPoints,
                Assigned = [assigned.Str, assigned.Dex, assigned.Int, assigned.Vit],
                Skills = PlayerProfile.Skills.Save(),
                Health = PlayerProfile.HealthFraction,
                Mana = PlayerProfile.ManaFraction,
                FlaskCharges = PlayerProfile.FlaskCharges,
                Yang = PlayerProfile.Bag?.Yang ?? 0,
                NextUid = GameItems.Factory.NextUid,
                NextGrantUid = PlayerProfile.Bag?.NextGrantUid ?? -1,
                Items = items,
                Worn = worn,
            },
            World = new SavedWorld
            {
                Zone = GameWorld.CurrentZoneId,
                Position = position is { } at ? [at.X, at.Y, at.Z] : null,
                DiscoveredShrines = GameWorld.Travel.Discovered.ToList(),
                Anchor = GameWorld.Travel.Anchor,
                Depths = PlayerProfile.AllDepths.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal),
                Explored = PlayerProfile.Explored.ToDictionary(p => p.Key, p => p.Value.ToList(), StringComparer.Ordinal),
            },
        };
    }

    // ------------------------------------------------------------------ reading

    /// <summary>Loads the newest save of any kind, quick or auto.</summary>
    public bool LoadLatest()
    {
        var newest = Slots()
            .Select(slot => (Slot: slot, File: new FileInfo(PathOf(slot))))
            .Where(s => s.File.Exists)
            .OrderByDescending(s => s.File.LastWriteTimeUtc)
            .Select(s => s.Slot)
            .ToList();

        // Newest first, falling back through the older ones. A damaged newest save is the
        // case the ring exists for, and stopping at it would throw the ring away.
        foreach (var slot in newest)
        {
            if (Load(slot)) return true;
        }

        if (newest.Count == 0) UI.WorldNotice.Show(GetTree(), "There is no save to load.");

        return false;
    }

    public bool Load(string slot)
    {
        var path = PathOf(slot);
        string text;

        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"[save] could not read {path}: {ex.Message}");
            return false;
        }

        var result = SaveCodec.Decode(text);

        if (!result.Ok)
        {
            // FR-11.3: said plainly, and the file is left alone. Deleting a save the player
            // might still recover by hand would turn a warning into a loss.
            GD.PushWarning($"[save] {slot} was not loaded: {result.Message}");
            UI.WorldNotice.Show(GetTree(), $"The save '{slot}' is damaged and was skipped.");

            return false;
        }

        var save = result.Save!;
        var scene = GameWorld.Graph[save.World.Zone]?.Scene;

        if (string.IsNullOrEmpty(scene))
        {
            GD.PushWarning($"[save] {slot} is in '{save.World.Zone}', which has no scene.");
            return false;
        }

        Apply(save);
        _loading = true;

        GD.Print($"[save] loaded {slot} — {save.World.Zone}, level {save.Player.Level}");

        GetTree().Paused = false;
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, scene);

        return true;
    }

    /// <summary>Replaces the whole session with what the save describes.</summary>
    private static void Apply(SaveGame save)
    {
        PlayerProfile.Reset();

        if (Enum.TryParse<Difficulty>(save.Difficulty, out var tier))
        {
            GameSession.SetDifficulty(DifficultySettings.For(tier));
        }

        if (save.Seed != 0) GameSession.SetSeed(save.Seed);

        var p = save.Player;
        var assigned = p.Assigned.Length == 4
            ? new Attributes(p.Assigned[0], p.Assigned[1], p.Assigned[2], p.Assigned[3])
            : default;

        PlayerProfile.Progression.Load(p.Level, p.Experience, p.AttributePoints, p.SkillPoints, assigned);
        PlayerProfile.Skills.Load(p.Skills);

        // A save taken at a sliver of health loads at a sliver. A save somehow taken at none
        // loads at full, rather than as a corpse.
        PlayerProfile.HealthFraction = p.Health > 0 ? Math.Clamp(p.Health, 0.05, 1.0) : 1.0;
        PlayerProfile.ManaFraction = Math.Clamp(p.Mana, 0.0, 1.0);
        PlayerProfile.FlaskCharges = p.FlaskCharges;

        var catalogue = GameItems.Catalogue;
        var restored = new Dictionary<long, (ItemInstance Item, int[]? At)>();

        foreach (var saved in p.Items)
        {
            if (ItemSnapshots.Restore(saved, catalogue, catalogue) is { } item)
            {
                restored[item.Uid] = (item, saved.At);
            }
            else
            {
                GD.PushWarning($"[save] dropped '{saved.Def}': it no longer exists.");
            }
        }

        var worn = new Dictionary<EquipSlot, ItemInstance>();

        foreach (var (slotName, uid) in p.Worn)
        {
            if (Enum.TryParse<EquipSlot>(slotName, out var slot) && restored.TryGetValue(uid, out var entry))
            {
                worn[slot] = entry.Item;
            }
        }

        var wornUids = worn.Values.Select(i => i.Uid).ToHashSet();

        // An item neither worn nor placed — a worn item whose slot no longer exists, say — goes
        // into the bag rather than into nothing.
        var carried = restored.Values
            .Where(e => !wornUids.Contains(e.Item.Uid))
            .Select(e => (e.Item, X: e.At is { Length: 2 } at ? at[0] : -1, Y: e.At is { Length: 2 } at2 ? at2[1] : -1));

        var bag = PlayerProfile.AdoptBag(() => new Inventory(catalogue, 10, 8));
        var homeless = bag.Restore(p.Yang, p.NextGrantUid, carried);

        foreach (var lost in homeless)
        {
            GD.PushWarning($"[save] '{lost.DefId}' did not fit back in the bag.");
        }

        PlayerProfile.AdoptGear(() => new Equipment(catalogue)).Load(worn);

        // Past every uid in the save, whatever the file claims: an item minted after loading
        // must never share an id with one already owned.
        var highest = restored.Count == 0 ? 0 : restored.Keys.Max();
        GameItems.Factory.NextUid = Math.Max(p.NextUid, highest + 1);

        PlayerProfile.MarkCreated();

        PlayerProfile.Quests.Load(save.CompletedQuests, save.ActiveQuest, save.QuestProgress);

        foreach (var (zone, depth) in save.World.Depths) PlayerProfile.ReachedDepth(zone, depth);

        foreach (var (zone, cells) in save.World.Explored)
        {
            PlayerProfile.Explored[zone] = cells.ToHashSet();
        }

        GameWorld.Travel.Load(save.World.DiscoveredShrines, save.World.Anchor);

        _pendingPosition = save.World.Position is { Length: 3 } at
            ? new Vector3((float)at[0], (float)at[1], (float)at[2])
            : null;
    }

    /// <summary>
    /// Where a load wants the player, once. Read by the arriving zone.
    /// </summary>
    public static Vector3? TakePendingPosition()
    {
        _loading = false;

        var at = _pendingPosition;
        _pendingPosition = null;

        return at;
    }

    // ------------------------------------------------------------------ boot and new game

    /// <summary>
    /// Picks up where the last session left off.
    /// </summary>
    /// <remarks>
    /// Automatic, because the thing a tester wants on launch is to be back where they were;
    /// a fresh start is one key away and says so on the way in.
    /// </remarks>
    private void ContinueOnBoot()
    {
        if (!Slots().Any(slot => File.Exists(PathOf(slot)))) return;

        if (LoadLatest())
        {
            GetTree().CreateTimer(1.2).Timeout += () =>
                UI.WorldNotice.Show(GetTree(), "Continued from your last save.  F10 save · F12 load · Shift+F12 new game");
        }
    }

    /// <summary>
    /// Starts a new character in the village. Saves on disk are left alone.
    /// </summary>
    private void NewGame()
    {
        PlayerProfile.Reset();
        GameWorld.Travel.Load([], null);
        _pendingPosition = null;

        GD.Print("[save] new game");

        GetTree().Paused = false;
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile,
            (string)ProjectSettings.GetSetting("application/run/main_scene"));
    }

    // ------------------------------------------------------------------ slots

    private static IEnumerable<string> Slots() =>
        Enumerable.Range(0, AutosaveRing).Select(i => $"auto_{i}").Prepend(QuickSlot);

    private static string PathOf(string slot) => System.IO.Path.Combine(Folder, $"{slot}.sav");

    /// <summary>The autosave slot to overwrite next: an empty one, else the oldest.</summary>
    private static string OldestAutosave() =>
        Enumerable.Range(0, AutosaveRing)
            .Select(i => $"auto_{i}")
            .OrderBy(slot =>
            {
                var file = new FileInfo(PathOf(slot));
                return file.Exists ? file.LastWriteTimeUtc : DateTime.MinValue;
            })
            .First();
}
