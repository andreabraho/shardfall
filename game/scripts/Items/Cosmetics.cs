using System.Linq;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Data.Definitions;

namespace Kiln.Game.Items;

/// <summary>
/// The looks the bosses give (REF-23): winning them, and finding what is worn.
/// </summary>
/// <remarks>
/// A boss's death rolls each of its cosmetics on its own, apart from the loot table, so the
/// drops the game already has are untouched. One already owned pays its gan instead: a second
/// copy of a look is worth nothing, and a kill that paid nothing would feel like a bug.
/// </remarks>
public static class Cosmetics
{
    public const string Sword = "sword";
    public const string Armour = "armour";
    public const string Aura = "aura";
    public const string Companion = "companion";

    public static CosmeticDef? Def(string? id) =>
        id is not null && GameContent.IsLoaded && GameContent.Database.Cosmetics.TryGetValue(id, out var def) ? def : null;

    /// <summary>The cosmetic worn in <paramref name="kind"/>, or null.</summary>
    public static CosmeticDef? Worn(string kind) => Def(PlayerProfile.Wardrobe.WornIn(kind));

    /// <summary>Every cosmetic of a kind, in the order the bosses fall.</summary>
    public static CosmeticDef[] OfKind(string kind) =>
        !GameContent.IsLoaded
            ? []
            : [.. GameContent.Database.Cosmetics.Values
                .Where(c => c.Kind == kind)
                .OrderBy(c => GameContent.Database.Enemies.TryGetValue(c.Boss, out var boss) ? boss.Level : 0)
                .ThenBy(c => c.Id, System.StringComparer.Ordinal)];

    /// <summary>Rolls a boss's cosmetics as it dies.</summary>
    public static void BossKilled(SceneTree tree, string bossId)
    {
        if (!GameContent.IsLoaded) return;

        foreach (var look in GameContent.Database.Cosmetics.Values.Where(c => c.Boss == bossId).OrderBy(c => c.Id, System.StringComparer.Ordinal))
        {
            if (!GameItems.LootRng.Chance(look.Chance)) continue;

            var name = GameItems.Localise(look.Name);

            if (PlayerProfile.Wardrobe.Unlock(look.Id))
            {
                GD.Print($"[cosmetic] {bossId} gave {look.Id}");
                UI.WorldNotice.Show(tree, L10n.F("New look: {0}!  Wear it from the wardrobe (C).", name));
                UI.ChatLog.Post(L10n.F("You won {0}. It is in your wardrobe.", name), new Color(0.98f, 0.82f, 0.45f));
                Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndRareDrop);
                continue;
            }

            // Already owned: its worth in gan.
            if (look.DuplicateGan > 0 && PlayerProfile.Bag is { } bag)
            {
                bag.AddYang(look.DuplicateGan);
                UI.ChatLog.Post(L10n.F("{0} again — you have it already: {1} gan instead.", name, look.DuplicateGan), new Color(0.85f, 0.80f, 0.60f));
            }

            GD.Print($"[cosmetic] {bossId} gave {look.Id} again — {look.DuplicateGan} gan");
        }
    }

    /// <summary>Debug: every cosmetic, owned at once.</summary>
    public static void UnlockAll()
    {
        if (!GameContent.IsLoaded) return;

        foreach (var id in GameContent.Database.Cosmetics.Keys) PlayerProfile.Wardrobe.Unlock(id);

        GD.Print($"[debug] every cosmetic unlocked ({GameContent.Database.Cosmetics.Count})");
    }
}
