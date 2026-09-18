using System.Globalization;
using Kiln.Core.Progression;

namespace Kiln.Tools.Commands;

/// <summary>
/// Walks the campaign's experience budget and reports whether the player arrives in each
/// zone at the intended level (PRG-09).
/// <para>
/// This is what turns "no grinding required" from an intention into a checked property.
/// Doc 02 §8 promises the campaign is completable without ever farming a zone for
/// experience; without a simulation that claim can only be tested by someone playing the
/// whole game, which means it would be tested approximately never.
/// </para>
/// </summary>
public static class SimulateCommand
{
    private static Zone[] Campaign => CampaignModel.Zones;

    /// <summary>Share of experience each source is meant to contribute (doc 06 §3).</summary>
    private const double QuestShareTarget = 0.45;
    private const double ShardShareTarget = 0.35;
    private const double TrashShareTarget = 0.20;

    public static int Run(string[] args)
    {
        var verbose = Program.HasFlag(args, "--verbose");
        var progression = new CharacterProgression();

        long questXp = 0, shardXp = 0, trashXp = 0;
        var failures = 0;

        Console.WriteLine("Campaign pacing simulation");
        Console.WriteLine("  zone                 band   level on entry   verdict");
        Console.WriteLine("  ----------------------------------------------------------");

        foreach (var zone in Campaign)
        {
            var entryLevel = progression.Level;
            var delta = entryLevel - zone.Band;

            // Arriving more than two levels under the band means the player would be forced
            // to farm to keep up; more than four over means the content is already trivial.
            var verdict = delta switch
            {
                < -2 => "UNDER-LEVELLED",
                > 4 => "OVER-LEVELLED",
                _ => "ok",
            };

            if (verdict != "ok") failures++;

            Console.WriteLine(
                $"  {zone.Name,-20} {zone.Band,4}   {entryLevel,6} ({delta,+3})   {verdict}");

            questXp += Award(progression, zone, Source.Quest, ref verbose);
            shardXp += Award(progression, zone, Source.Shard, ref verbose);
            trashXp += Award(progression, zone, Source.Trash, ref verbose);
        }

        var total = questXp + shardXp + trashXp;

        Console.WriteLine();
        Console.WriteLine($"  Final level: {progression.Level} ({progression.LevelProgress:P0} to next)");
        Console.WriteLine($"  Total experience: {total:N0}");
        Console.WriteLine();
        Console.WriteLine("  source   share    target");
        Console.WriteLine($"  quests   {Share(questXp, total),6:P0}   {QuestShareTarget,6:P0}");
        Console.WriteLine($"  shards   {Share(shardXp, total),6:P0}   {ShardShareTarget,6:P0}");
        Console.WriteLine($"  trash    {Share(trashXp, total),6:P0}   {TrashShareTarget,6:P0}");
        Console.WriteLine();

        // A wide drift in the mix means the game is quietly becoming a different kind of
        // game — mostly killing, or mostly questing — regardless of whether levels line up.
        failures += ReportShare("quests", Share(questXp, total), QuestShareTarget);
        failures += ReportShare("shards", Share(shardXp, total), ShardShareTarget);
        failures += ReportShare("trash", Share(trashXp, total), TrashShareTarget);

        if (failures > 0)
        {
            Console.Error.WriteLine($"FAILED: {failures} pacing problem(s).");
            return 1;
        }

        Console.WriteLine("OK: pacing within tolerance — the campaign needs no farming.");
        return 0;
    }

    private enum Source
    {
        Quest,
        Shard,
        Trash,
    }

    private static long Award(CharacterProgression progression, Zone zone, Source source, ref bool verbose)
    {
        long awarded = 0;

        void Give(long amount)
        {
            var scaled = (long)Math.Round(amount * ExperienceTable.CatchUpMultiplier(progression.Level, zone.Band));
            awarded += scaled;
            progression.Grant(scaled);
        }

        switch (source)
        {
            case Source.Quest:
                for (var i = 0; i < zone.StoryQuests; i++) Give(ExperienceTable.QuestXp(zone.Band));
                for (var i = 0; i < zone.SideQuests; i++) Give(ExperienceTable.QuestXp(zone.Band) / 2);
                break;

            case Source.Shard:
                for (var i = 0; i < zone.Shards; i++) Give(ExperienceTable.ShardXp(zone.ShardTier, zone.Band));
                break;

            case Source.Trash:
                for (var i = 0; i < zone.TrashKills; i++) Give(ExperienceTable.TrashXp(zone.Band));
                break;
        }

        if (verbose)
        {
            Console.WriteLine($"      {zone.Name} {source}: {awarded:N0} xp, now level {progression.Level}");
        }

        return awarded;
    }

    private static double Share(long part, long total) => total == 0 ? 0 : part / (double)total;

    private static int ReportShare(string name, double actual, double target)
    {
        const double tolerance = 0.12;

        if (Math.Abs(actual - target) <= tolerance) return 0;

        Console.Error.WriteLine(
            $"  {name} contribute {actual.ToString("P0", CultureInfo.InvariantCulture)} of experience, " +
            $"target {target.ToString("P0", CultureInfo.InvariantCulture)} (tolerance {tolerance:P0}).");

        return 1;
    }
}
