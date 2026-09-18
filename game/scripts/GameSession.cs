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

    public static void SetDifficulty(DifficultySettings difficulty) => Difficulty = difficulty;

    public static void SetSeed(ulong seed)
    {
        Seed = seed;
        _rng = null;
    }
}
