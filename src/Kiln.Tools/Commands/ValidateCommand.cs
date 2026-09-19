using System.Diagnostics;
using Kiln.Data.Loading;
using Kiln.Data.Validation;

namespace Kiln.Tools.Commands;

public static class ValidateCommand
{
    public static int Run(string[] args)
    {
        var dataRoot = Program.ResolveDataRoot(args);
        var sw = Stopwatch.StartNew();

        Console.WriteLine($"Loading content from {dataRoot}");
        var load = ContentLoader.LoadFromDirectory(dataRoot);

        if (load.Errors.Count > 0)
        {
            Console.Error.WriteLine($"\n{load.Errors.Count} load error(s):");
            foreach (var error in load.Errors)
            {
                Console.Error.WriteLine($"  ERROR [load] {error}");
            }

            Console.Error.WriteLine("\nContent could not be loaded. Fix the above and re-run.");
            return 1;
        }

        var db = load.Database;
        var report = ContentValidator.Validate(db);
        sw.Stop();

        Console.WriteLine(
            $"Loaded {db.TotalDefinitions} definitions " +
            $"({db.Items.Count} items, {db.Enemies.Count} enemies, {db.Skills.Count} skills, " +
            $"{db.Quests.Count} quests, {db.DropTables.Count} drop tables, {db.BonusPools.Count} bonus pools, " +
            $"{db.UpgradePaths.Count} upgrade paths, {db.Visuals.Count} visuals, "
            + $"{db.Shards.Count} shards, {db.Zones.Count} zones) in {sw.ElapsedMilliseconds} ms\n");

        if (report.Findings.Count > 0)
        {
            Console.WriteLine(report.Format());
            Console.WriteLine();
        }

        if (report.HasErrors)
        {
            Console.Error.WriteLine($"FAILED: {report.ErrorCount} error(s), {report.WarningCount} warning(s).");
            return 1;
        }

        Console.WriteLine($"OK: content valid ({report.WarningCount} warning(s)).");
        return 0;
    }
}
