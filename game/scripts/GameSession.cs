using Kiln.Core.Combat;
using Kiln.Core.Foundation;

namespace Kiln.Game;

/// <summary>
/// Session-wide state that is not part of the save file yet: the active difficulty and the
/// RNG root. Difficulty is changeable at any time (FR-11.4), so nothing may cache its
/// multipliers — read them through <see cref="Difficulty"/> each time.
/// </summary>
public static class GameSession
{
    private static DeterministicRng? _rng;

    public static DifficultySettings Difficulty { get; private set; } = DifficultySettings.Disciple;

    /// <summary>Combat RNG stream. Forked so loot rolls can never shift damage rolls.</summary>
    public static DeterministicRng CombatRng => _rng ??= new DeterministicRng(Seed).Fork("combat");

    public static ulong Seed { get; private set; } = 20260918;

    /// <summary>
    /// Drawn afresh each time a character is started or loaded (2026-10-09). The saved seed alone
    /// started loot and the anvil at the same roll on every load, so the same kills in the same
    /// order dropped the same things — one Kiln relic in three never fell. What must come back the
    /// same (a merchant's stock) reads <see cref="Seed"/>; the dice read the two together.
    /// </summary>
    public static ulong Salt { get; private set; } = Fresh();

    public static void SetDifficulty(DifficultySettings difficulty) => Difficulty = difficulty;

    public static void SetSeed(ulong seed)
    {
        Seed = seed;
        Salt = Fresh();
        _rng = null;
        Items.GameItems.ResetDice();
    }

    private static ulong Fresh() => (ulong)System.DateTime.UtcNow.Ticks * 0x9E3779B97F4A7C15UL ^ (ulong)System.Environment.TickCount64;
}
