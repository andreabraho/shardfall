using Kiln.Core.Items;
using Kiln.Data.Definitions;
using Kiln.Data.Items;
using Kiln.Data.Loading;

namespace Kiln.Tools.Commands;

/// <summary>
/// Walks the campaign's yang budget and checks the shape doc 02 §4.4 promises: the player is
/// mildly money-constrained through the first half and comfortable after (ITM-12).
/// </summary>
/// <remarks>
/// The claim is a real design decision, not a platitude. Money pressure that never lets up
/// turns a 20-hour game into a grind; money pressure that never exists makes every cost in
/// the game decorative, including the upgrade ladder the whole itemisation rests on. Only a
/// simulation catches the drift, because the symptom — "I never had to think about yang" —
/// is invisible until someone has played twenty hours.
/// <para>
/// Costs come from the real upgrade paths and item values, so retuning a ladder in JSON moves
/// this report immediately.
/// </para>
/// </remarks>
public static class EconomyCommand
{
    /// <summary>Yang a story quest pays at a given band. Planned values until quests are complete.</summary>
    private static long QuestYang(int band) => (long)Math.Round(260 * Math.Pow(band, 1.25));

    /// <summary>A shard encounter pays roughly eight ordinary kills, plus its own drop.</summary>
    private const double ShardYangMultiplier = 8.0;

    /// <summary>Not every drop is sold; the rest is kept, used, or left behind.</summary>
    private const double SellThroughRate = 0.6;

    /// <summary>Zones at or below this band are "the first half" for the purposes of money pressure.</summary>
    private const int EarlyBandLimit = 12;

    /// <summary>
    /// Share of first-half income the planned gear cadence should consume. Below this the
    /// player never has to choose between upgrading the weapon and upgrading the armour,
    /// which is the only decision the yang economy exists to create.
    /// </summary>
    private const double EarlyPressureTarget = 0.55;

    /// <summary>Ending balance above this many upgrades means the sink has stopped working.</summary>
    private const double RichCeiling = 8.0;

    public static int Run(string[] args)
    {
        var dataRoot = Program.ResolveDataRoot(args);
        var loaded = ContentLoader.LoadFromDirectory(dataRoot);

        if (loaded.Errors.Count > 0)
        {
            foreach (var error in loaded.Errors) Console.Error.WriteLine($"  {error}");
            return 1;
        }

        var db = loaded.Database;
        var verbose = Program.HasFlag(args, "--verbose");

        var weapon = Ladder(db, "upg_standard_weapon");
        var armor = Ladder(db, "upg_standard_armor");

        if (weapon is null || armor is null)
        {
            Console.Error.WriteLine("kiln: economy needs upg_standard_weapon and upg_standard_armor.");
            return 1;
        }

        Console.WriteLine("Campaign economy simulation");
        Console.WriteLine("  zone                 income      spend     balance  pressure  gear  verdict");
        Console.WriteLine("  ---------------------------------------------------------------------------");

        long balance = 0;
        long earlyIncome = 0;
        long earlySpend = 0;
        var failures = 0;
        var upgradeLevel = 0;

        foreach (var zone in CampaignModel.Zones)
        {
            var income = ZoneIncome(zone, db);

            // The player brings both pieces of gear to the zone's target level, bores the
            // planned sockets, and buys the planned rerolls. This is unhurried play, not a
            // completionist: anyone who upgrades harder than this is choosing to be poor.
            long spend = 0;

            for (var level = upgradeLevel + 1; level <= zone.TargetUpgrade; level++)
            {
                spend += ExpectedStepCost(weapon, level, db);
                spend += ExpectedStepCost(armor, level, db);
            }

            upgradeLevel = Math.Max(upgradeLevel, zone.TargetUpgrade);

            spend += zone.Bores * (ItemEconomy.SocketBoreYang + Value(db, SocketBench.BoringStoneId));
            spend += zone.Rerolls * (ItemEconomy.RerollYang + Value(db, RerollTable.MutationInkId));

            balance += income - spend;

            if (zone.Band <= EarlyBandLimit)
            {
                earlyIncome += income;
                earlySpend += spend;
            }

            var pressure = income == 0 ? 0 : spend / (double)income;
            var verdict = balance < 0 ? "BROKE" : "ok";

            if (balance < 0) failures++;

            Console.WriteLine(
                $"  {zone.Name,-20} {income,8:N0}  {spend,9:N0}  {balance,10:N0}    {pressure,5:P0}   +{zone.TargetUpgrade}  {verdict}");

            if (verbose)
            {
                Console.WriteLine($"      trash {zone.TrashKills} kills, {zone.Shards} shards, "
                    + $"{zone.StoryQuests + zone.SideQuests} quests");
            }
        }

        // "Comfortable" is measured against the rung the player has been climbing, not the +9
        // capstone. +9 is deliberately aspirational — the thing you are still saving for when
        // the credits roll — so using its cost here would define comfort as having already
        // finished the chase, which is the opposite of what the design wants.
        var nextStep = ExpectedStepCost(weapon, upgradeLevel, db);
        var earlyPressure = earlyIncome == 0 ? 0 : earlySpend / (double)earlyIncome;

        Console.WriteLine();
        Console.WriteLine($"  Ending balance: {balance:N0} yang "
            + $"({balance / (double)Math.Max(1, nextStep):F1}x a +{upgradeLevel} upgrade)");
        Console.WriteLine($"  First-half pressure: {earlyPressure:P0} of income spent (target {EarlyPressureTarget:P0}+)");
        Console.WriteLine();

        // Constrained early. Measured as the share of income the planned gear cadence eats,
        // not as a low balance: a balance is low at the start of any game, which is why the
        // first version of this check passed a campaign where money never mattered at all.
        if (earlyPressure < EarlyPressureTarget)
        {
            Console.Error.WriteLine(
                $"  Never money-constrained: the first half spends only {earlyPressure:P0} of its income "
                + $"on gear (target {EarlyPressureTarget:P0}+). Yang costs are decorative — either income "
                + "is too high or the upgrade ladder is too cheap.");
            failures++;
        }

        // Comfortable late: the endgame should not be a second job.
        if (balance < nextStep)
        {
            Console.Error.WriteLine(
                $"  Still poor at the end: {balance:N0} yang against a {nextStep:N0} upgrade. "
                + "The back half would force farming.");
            failures++;
        }

        // ...but not so comfortable that the last act has nothing to spend on.
        if (balance > nextStep * RichCeiling)
        {
            Console.Error.WriteLine(
                $"  Awash at the end: {balance:N0} yang is {balance / (double)nextStep:F1}x an upgrade. "
                + "Every remaining cost is a formality.");
            failures++;
        }

        if (failures > 0)
        {
            Console.Error.WriteLine($"FAILED: {failures} economy problem(s).");
            return 1;
        }

        Console.WriteLine("OK: yang is tight early and comfortable late, as designed.");
        return 0;
    }

    /// <summary>Yang earned in a zone: kills, shards, quests, and what the drops sell for.</summary>
    private static long ZoneIncome(Zone zone, ContentDatabase db)
    {
        var tables = db.Enemies.Values
            .Where(e => Math.Abs(e.Level - zone.Band) <= 3 && e.DropTable is not null)
            .Select(e => e.DropTable!)
            .Distinct()
            .Select(id => db.DropTables.GetValueOrDefault(id))
            .Where(t => t is not null)
            .ToList();

        // Zones beyond the enemies that exist today fall back to the closest table, so the
        // report stays meaningful while content is still being written.
        if (tables.Count == 0) tables = [.. db.DropTables.Values.Take(1)];
        if (tables.Count == 0) return 0;

        var perKill = tables.Average(t => PerKillYang(t!, db));

        // Enemy yang has to grow with the band or late zones would pay prologue wages. The
        // drop tables themselves are per-zone data; this scales the ones that exist.
        var bandScale = Math.Pow(zone.Band, 1.15);

        var trash = zone.TrashKills * perKill * bandScale;
        var shards = zone.Shards * perKill * bandScale * ShardYangMultiplier;
        var quests = (zone.StoryQuests * QuestYang(zone.Band)) + (zone.SideQuests * QuestYang(zone.Band) / 2);

        return (long)Math.Round(trash + shards + quests);
    }

    /// <summary>Average yang from one kill: the coin drop plus the sale value of what falls.</summary>
    private static double PerKillYang(DropTableDef table, ContentDatabase db)
    {
        var coin = table.YangRange.Length > 1
            ? (table.YangRange[0] + table.YangRange[1]) / 2.0
            : 0;

        var loot = 0.0;

        foreach (var entry in table.Entries)
        {
            var count = entry.CountRange.Length > 1
                ? (entry.CountRange[0] + entry.CountRange[1]) / 2.0
                : 1;

            loot += entry.Chance * count * Value(db, entry.Item) * ItemEconomy.VendorBuybackRate * SellThroughRate;
        }

        return coin + loot;
    }

    private static UpgradeLadder? Ladder(ContentDatabase db, string pathId) =>
        db.UpgradePaths.TryGetValue(pathId, out var def) ? ItemCatalogue.ToLadder(def) : null;

    private static long Value(ContentDatabase db, string itemId) =>
        db.Items.TryGetValue(itemId, out var item) ? item.SellValue : 0;

    /// <summary>
    /// What one rung costs on average, in yang, counting the materials at their sale value and
    /// the attempts the pity counter allows.
    /// </summary>
    private static long ExpectedStepCost(UpgradeLadder ladder, int level, ContentDatabase db)
    {
        if (ladder.StepTo(level) is not { } step) return 0;

        var materials = step.Materials.Sum(m => (long)m.Value * Value(db, m.Key));
        var perAttempt = step.Yang + materials;

        return (long)Math.Round(perAttempt * ExpectedAttempts(step));
    }

    /// <summary>
    /// Expected attempts to clear a rung, with the pity counter capping the tail.
    /// <para>
    /// This is the number that makes the no-gambling ladder costable at all. Without pity the
    /// expectation is 1/p with an unbounded tail, and no budget can be planned around it.
    /// </para>
    /// </summary>
    private static double ExpectedAttempts(UpgradeStep step)
    {
        if (step.Chance >= 1.0) return 1;
        if (step.Pity <= 0) return 1.0 / step.Chance;

        var expected = 0.0;
        var miss = 1.0;

        for (var k = 1; k <= step.Pity; k++)
        {
            expected += k * step.Chance * miss;
            miss *= 1 - step.Chance;
        }

        // The guaranteed attempt, taken only if every earlier one missed.
        return expected + ((step.Pity + 1) * miss);
    }
}
