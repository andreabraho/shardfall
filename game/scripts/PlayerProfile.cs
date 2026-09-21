using System;
using System.Collections.Generic;
using Kiln.Core.Items;
using Kiln.Core.Progression;

namespace Kiln.Game;

/// <summary>
/// The character, for as long as the game is running: what they have learned, what they are
/// carrying, and how hurt they are.
/// </summary>
/// <remarks>
/// Walking through a gate replaces the whole scene tree, and everything the player had was
/// owned by nodes in it. Rather than snapshot those objects and rebuild them, the session
/// owns the live <c>Kiln.Core</c> objects and each new scene's nodes adopt them — so there is
/// no copy to keep in step, and nothing to forget to copy.
/// <para>
/// This is not the save file; <see cref="Saving.SaveService"/> is. What this does is make the
/// save a matter of serialising objects that already exist in one place, rather than
/// gathering them from across a scene tree.
/// </para>
/// </remarks>
public static class PlayerProfile
{
    private static CharacterProgression? _progression;
    private static SkillBook? _skills;
    private static Inventory? _bag;
    private static Equipment? _gear;

    /// <summary>
    /// True once a scene has built the character, so the next scene adopts instead.
    /// </summary>
    /// <remarks>
    /// Set deliberately rather than inferred from whether the progression object exists. When
    /// it was inferred, the first thing to so much as read <see cref="Progression"/> created
    /// it and the character then counted as already built — so the scene that was supposed to
    /// grant the starting level skipped it, and the player began at level one however the map
    /// was configured. Whoever happens to touch a lazy field first is not a design.
    /// </remarks>
    public static bool Exists { get; private set; }

    /// <summary>Says the character has been set up, so later scenes adopt rather than rebuild.</summary>
    public static void MarkCreated() => Exists = true;

    public static CharacterProgression Progression => _progression ??= new CharacterProgression();

    public static SkillBook Skills => _skills ??= new SkillBook();

    /// <summary>
    /// Health carried across a gate, as a fraction.
    /// </summary>
    /// <remarks>
    /// A fraction rather than a number because maximum health changes with gear and level, and
    /// the two ends of a journey should not disagree about what "nearly dead" meant. Walking
    /// out of a fight at a sliver and arriving at full would make a zone boundary a free heal,
    /// which is the cheapest exploit a connected world can offer.
    /// </remarks>
    public static double HealthFraction { get; set; } = 1.0;

    public static double ManaFraction { get; set; } = 1.0;

    /// <summary>
    /// Quests the character has finished, for the gates that ask.
    /// </summary>
    /// <remarks>
    /// Empty until the quest runtime lands in Phase 7. A story-gated exit therefore refuses
    /// everybody, which is the correct answer to "is this quest done" while no quest can be
    /// done — and far better than a gate that quietly ignores its own requirement.
    /// </remarks>
    public static HashSet<string> CompletedQuests { get; } = new(StringComparer.Ordinal);

    /// <summary>Flask charges carried across. Refilled at a shrine, not at a border.</summary>
    public static int FlaskCharges { get; set; } = -1;

    /// <summary>
    /// Ground the character has walked, per zone, as a coarse grid of cells (WLD-08).
    /// </summary>
    /// <remarks>
    /// Here rather than on the map panel because the panel is rebuilt with every scene, and a
    /// map that forgets the zone the moment you leave it would be redrawn from nothing on the
    /// way back — which turns a returning trip through familiar country into a first visit.
    /// </remarks>
    public static Dictionary<string, HashSet<long>> Explored { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// How far down each tower the player has reached (FR-7.20).
    /// </summary>
    /// <remarks>
    /// The deepest floor, not the current one. Walking back up to leave must not cost the
    /// descent, and the only number anybody wants restored is the furthest they got.
    /// </remarks>
    private static readonly Dictionary<string, int> Depths = new(StringComparer.Ordinal);

    /// <summary>Every tower and its deepest checkpoint, for the save file.</summary>
    public static IReadOnlyDictionary<string, int> AllDepths => Depths;

    /// <summary>The floor to resume on, or the first.</summary>
    public static int DepthIn(string zoneId) => Depths.GetValueOrDefault(zoneId, 1);

    public static void ReachedDepth(string zoneId, int depth) =>
        Depths[zoneId] = System.Math.Max(DepthIn(zoneId), depth);

    /// <summary>The bag, if a scene has made one yet. For the save file, which must not create one.</summary>
    public static Inventory? Bag => _bag;

    public static Equipment? Gear => _gear;

    public static Inventory AdoptBag(System.Func<Inventory> create) => _bag ??= create();

    public static Equipment AdoptGear(System.Func<Equipment> create) => _gear ??= create();

    /// <summary>Drops the character. Used by a new game, and by tests that need a clean slate.</summary>
    public static void Reset()
    {
        Exists = false;
        _progression = null;
        _skills = null;
        _bag = null;
        _gear = null;
        HealthFraction = 1.0;
        ManaFraction = 1.0;
        FlaskCharges = -1;
        CompletedQuests.Clear();
        Explored.Clear();
        Depths.Clear();
    }
}
