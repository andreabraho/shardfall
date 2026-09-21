using System;
using System.Linq;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.Quests;
using Kiln.Game.Items;

namespace Kiln.Game.Quests;

/// <summary>
/// Where the world tells the quest chain what the player did, and where finishing a quest
/// pays out (FR-8).
/// </summary>
/// <remarks>
/// Static, because the things that report — a creature dying, a shard breaking, a zone
/// loading, the tower's last floor opening — live in scenes that come and go, and none of
/// them should have to find a quest node to talk to. The chain itself lives on
/// <see cref="PlayerProfile"/>, so it survives every border.
/// </remarks>
public static class QuestTracker
{
    /// <summary>Raised whenever the active quest or its progress changes. The HUD listens.</summary>
    public static event Action? Changed;

    public static void Report(SceneTree tree, ObjectiveType type, string target)
    {
        if (!GameContent.IsLoaded || string.IsNullOrEmpty(target)) return;

        var events = PlayerProfile.Quests.Report(type, target);

        if (events.Count == 0) return;

        var finished = events.FirstOrDefault(e => e.Kind == QuestEventKind.Completed);
        var next = events.FirstOrDefault(e => e.Kind == QuestEventKind.Started);

        if (finished is not null)
        {
            // Guarded, because this runs inside a creature's death and a shard's break. A throw
            // here would abort whatever called it, and a half-finished death is worse than a
            // reward that failed and said so in the log.
            string paid;

            try
            {
                paid = Pay(tree, finished.Quest);
            }
            catch (System.Exception ex)
            {
                GD.PushError($"[quest] paying {finished.Quest.Id} failed: {ex.Message}");
                paid = L10n.T("reward failed — see the log");
            }

            GD.Print($"[quest] finished {finished.Quest.Id} — {paid}"
                + (next is null ? "" : $"; next {next.Quest.Id}"));

            // One line for both halves. Two notices in the same frame would overwrite each
            // other, and "what do I do now" is the half that must not be lost.
            UI.WorldNotice.Show(tree, next is null
                ? L10n.F("Quest complete: {0}  ({1})", Name(finished.Quest), paid)
                : L10n.F("Quest complete: {0}  ({1})   ·   Next: {2}", Name(finished.Quest), paid, Name(next.Quest)));

            Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndQuestComplete);
            Saving.SaveService.Autosave($"Finished {finished.Quest.Id}");
        }

        Changed?.Invoke();
    }

    /// <summary>Tells listeners the chain changed underneath them — a load, or the F9 debug key.</summary>
    public static void Touch() => Changed?.Invoke();

    public static string Name(QuestStep quest) => GameItems.Localise(quest.Name);

    /// <summary>
    /// Hands over the reward and says what it was.
    /// </summary>
    /// <remarks>
    /// An item that does not fit in the bag lands at the player's feet rather than vanishing:
    /// a quest reward lost to a full bag is a reward the player never knew they had.
    /// </remarks>
    private static string Pay(SceneTree tree, QuestStep quest)
    {
        var player = tree.GetFirstNodeInGroup("player") as Node3D;
        var reward = quest.Reward;

        player?.GetNodeOrNull<Player.PlayerCharacter>("PlayerCharacter")?.GrantExperience(reward.Xp);
        PlayerProfile.Bag?.AddYang(reward.Yang);

        foreach (var id in reward.Items)
        {
            var item = GameItems.Factory.Create(id, GameItems.EncounterRng);

            if (PlayerProfile.Bag?.TryAdd(item) == true || player is null) continue;

            World.LootDrop.Place(tree.CurrentScene, item, player.GlobalPosition);
        }

        var parts = new System.Collections.Generic.List<string>();

        if (reward.Xp > 0) parts.Add(L10n.F("+{0:N0} xp", reward.Xp));
        if (reward.Yang > 0) parts.Add(L10n.F("+{0:N0} yang", reward.Yang));
        if (reward.Items.Count > 0) parts.Add(string.Join(", ", reward.Items.Select(GameItems.NameOfId)));

        return string.Join("  ", parts);
    }
}
