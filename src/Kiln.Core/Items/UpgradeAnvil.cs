using Kiln.Core.Foundation;

namespace Kiln.Core.Items;

/// <summary>One rung of the ladder: what it costs to reach <paramref name="To"/> and how likely it is.</summary>
/// <param name="Pity">Guaranteed success once this many consecutive attempts at this rung have failed. 0 = no pity needed.</param>
public sealed record UpgradeStep(int To, long Yang, IReadOnlyDictionary<string, int> Materials, double Chance, int Pity);

/// <summary>The full +0 → +9 ladder for a family of items.</summary>
public sealed record UpgradeLadder(string Id, IReadOnlyList<UpgradeStep> Steps)
{
    public int MaxLevel => Steps.Count == 0 ? 0 : Steps.Max(s => s.To);

    public UpgradeStep? StepTo(int level) => Steps.FirstOrDefault(s => s.To == level);
}

public enum UpgradeOutcome
{
    Success,

    /// <summary>The roll missed. Materials are gone; the item is untouched and the pity counter moved.</summary>
    Failed,

    AtMaxLevel,
    NoLadder,
    CannotAfford,
}

/// <summary>
/// What the UI shows before the player commits (FR-5.5).
/// </summary>
/// <param name="Guaranteed">True when pity has already been reached, so this attempt cannot fail.</param>
/// <param name="AttemptsToGuarantee">Failures still needed before the next attempt is guaranteed. 0 when already guaranteed.</param>
public sealed record UpgradeQuote(
    UpgradeStep Step,
    double DisplayedChance,
    bool Guaranteed,
    int FailuresSoFar,
    int AttemptsToGuarantee,
    bool Affordable);

public sealed record UpgradeResult(UpgradeOutcome Outcome, int Level, int Failures, bool WasGuaranteed)
{
    public bool Attempted => Outcome is UpgradeOutcome.Success or UpgradeOutcome.Failed;
}

/// <summary>
/// The signature system of the redesign (doc 02 §4.1, FR-5.5): a +0 → +9 ladder where
/// failure costs materials and never costs progress.
/// </summary>
/// <remarks>
/// The original's upgrade screen is a slot machine that can delete hours of play, and the
/// rational response to it is to close the game and restore a backup. Removing destruction
/// and downgrade — and showing the pity counter — is what makes the risk real without making
/// save-scumming the correct move. Nothing in here may ever lower <see cref="ItemInstance.UpgradeLevel"/>.
/// </remarks>
public static class UpgradeAnvil
{
    public static UpgradeQuote? Quote(ItemInstance item, UpgradeLadder? ladder, IResourceStore store)
    {
        var step = ladder?.StepTo(item.UpgradeLevel + 1);

        if (step is null) return null;

        var guaranteed = IsGuaranteed(step, item.UpgradeFailures);

        return new UpgradeQuote(
            Step: step,
            DisplayedChance: guaranteed ? 1.0 : step.Chance,
            Guaranteed: guaranteed,
            FailuresSoFar: item.UpgradeFailures,
            AttemptsToGuarantee: guaranteed ? 0 : Math.Max(0, step.Pity - item.UpgradeFailures),
            Affordable: store.CanAfford(step.Yang, step.Materials));
    }

    private static bool IsGuaranteed(UpgradeStep step, int failures) =>
        step.Chance >= 1.0 || (step.Pity > 0 && failures >= step.Pity);

    public static UpgradeResult Attempt(ItemInstance item, UpgradeLadder? ladder, IResourceStore store, DeterministicRng rng)
    {
        if (ladder is null) return new UpgradeResult(UpgradeOutcome.NoLadder, item.UpgradeLevel, item.UpgradeFailures, false);

        var step = ladder.StepTo(item.UpgradeLevel + 1);

        if (step is null) return new UpgradeResult(UpgradeOutcome.AtMaxLevel, item.UpgradeLevel, item.UpgradeFailures, false);

        if (!store.TrySpend(step.Yang, step.Materials))
        {
            return new UpgradeResult(UpgradeOutcome.CannotAfford, item.UpgradeLevel, item.UpgradeFailures, false);
        }

        var guaranteed = IsGuaranteed(step, item.UpgradeFailures);

        if (guaranteed || rng.Chance(step.Chance))
        {
            item.UpgradeLevel = step.To;
            item.UpgradeFailures = 0;

            return new UpgradeResult(UpgradeOutcome.Success, item.UpgradeLevel, 0, guaranteed);
        }

        item.UpgradeFailures++;

        return new UpgradeResult(UpgradeOutcome.Failed, item.UpgradeLevel, item.UpgradeFailures, false);
    }

    /// <summary>
    /// Worst-case attempts to clear a rung, which is what the pity counter actually promises.
    /// Used by the economy simulator to bound the cost of reaching +9.
    /// </summary>
    public static int MaxAttempts(UpgradeStep step) =>
        step.Chance >= 1.0 ? 1 : step.Pity > 0 ? step.Pity + 1 : int.MaxValue;
}
