using Kiln.Core.Combat;
using Kiln.Core.Foundation;
using Kiln.Data.Definitions;
using Kiln.Data.Loading;

namespace Kiln.Data.Validation;

/// <summary>
/// Enforces the rules in doc 06 §10. Runs in CI; any error fails the build.
/// <para>
/// The point is that a few thousand data rows cannot rot silently: a quest pointing at a
/// deleted NPC, an enemy citing a missing drop table, or a telegraph the player physically
/// cannot escape are all caught before anyone plays the build.
/// </para>
/// </summary>
public static class ContentValidator
{
    public const int MaxLevel = 60;

    public static ValidationReport Validate(ContentDatabase db)
    {
        var report = new ValidationReport();

        IdFormat(db, report);
        LocalisationKeys(db, report);
        CrossReferences(db, report);
        UpgradeSafety(db, report);
        QuestGraph(db, report);
        SideQuestRewards(db, report);
        TelegraphEscape(db, report);
        Weights(db, report);
        LevelRanges(db, report);

        return report;
    }

    // -- R1 -----------------------------------------------------------------
    private static void IdFormat(ContentDatabase db, ValidationReport report)
    {
        foreach (var def in db.All())
        {
            if (!ContentId.IsValid(def.Id))
            {
                report.Error("id-format", def.SourceFile,
                    $"'{def.Id}' is not a valid id.",
                    "Use lowercase prefix_name, e.g. 'mob_corrupted_wolf'.");
            }
        }
    }

    // -- R2 -----------------------------------------------------------------
    private static void LocalisationKeys(ContentDatabase db, ValidationReport report)
    {
        void Check(string where, string id, string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (value.StartsWith('$')) return;

            report.Error("localisation", where,
                $"'{id}' has a literal name \"{value}\".",
                "Player-facing text must be a $localisation.key (NFR-L.1) so it can be translated.");
        }

        foreach (var d in db.Items.Values) Check(d.SourceFile, d.Id, d.Name);
        foreach (var d in db.Enemies.Values) Check(d.SourceFile, d.Id, d.Name);
        foreach (var d in db.Skills.Values) Check(d.SourceFile, d.Id, d.Name);
        foreach (var d in db.Quests.Values) Check(d.SourceFile, d.Id, d.Name);
    }

    // -- R3 -----------------------------------------------------------------
    private static void CrossReferences(ContentDatabase db, ValidationReport report)
    {
        void Ref<T>(string where, string owner, string? id, IReadOnlyDictionary<string, T> registry, string kind)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (registry.ContainsKey(id)) return;

            report.Error("cross-ref", where,
                $"'{owner}' references {kind} '{id}', which does not exist.",
                $"Add '{id}' or correct the reference.");
        }

        foreach (var item in db.Items.Values)
        {
            Ref(item.SourceFile, item.Id, item.BonusPool, db.BonusPools, "bonus pool");
            Ref(item.SourceFile, item.Id, item.UpgradePath, db.UpgradePaths, "upgrade path");
            Ref(item.SourceFile, item.Id, item.Visual, db.Visuals, "visual");
        }

        foreach (var enemy in db.Enemies.Values)
        {
            Ref(enemy.SourceFile, enemy.Id, enemy.DropTable, db.DropTables, "drop table");
            Ref(enemy.SourceFile, enemy.Id, enemy.Visual, db.Visuals, "visual");
        }

        foreach (var table in db.DropTables.Values)
        {
            foreach (var entry in table.Entries)
            {
                Ref(table.SourceFile, table.Id, entry.Item, db.Items, "item");
            }
        }

        foreach (var path in db.UpgradePaths.Values)
        {
            foreach (var step in path.Steps)
            {
                foreach (var mat in step.Mats.Keys)
                {
                    Ref(path.SourceFile, $"{path.Id} (+{step.To})", mat, db.Items, "material item");
                }
            }
        }

        foreach (var quest in db.Quests.Values)
        {
            foreach (var prereq in quest.Prerequisites)
            {
                Ref(quest.SourceFile, quest.Id, prereq, db.Quests, "prerequisite quest");
            }

            foreach (var reward in quest.Rewards.Items)
            {
                Ref(quest.SourceFile, quest.Id, reward, db.Items, "reward item");
            }

            foreach (var objective in quest.Objectives)
            {
                // Kill and shard objectives must name something that exists; location and
                // NPC targets are validated once world data lands in Phase 6.
                if (objective.Type == ObjectiveType.Kill)
                {
                    Ref(quest.SourceFile, quest.Id, objective.Target, db.Enemies, "enemy");
                }
                else if (objective.Type == ObjectiveType.Collect)
                {
                    Ref(quest.SourceFile, quest.Id, objective.Target, db.Items, "item");
                }
            }
        }
    }

    // -- R4 -----------------------------------------------------------------
    private static void UpgradeSafety(ContentDatabase db, ValidationReport report)
    {
        foreach (var path in db.UpgradePaths.Values)
        {
            // FR-5.5 / decision D7: failure costs resources, never progress.
            if (path.OnFailure != "consume_materials_only")
            {
                report.Error("upgrade-safety", path.SourceFile,
                    $"'{path.Id}' has on_failure '{path.OnFailure}'.",
                    "Standard upgrade paths must be 'consume_materials_only' — no destruction, "
                    + "no downgrade. The opt-in Gambler's Anvil is a separate Shardbound-only path.");
            }

            var previousTo = 0;
            foreach (var step in path.Steps)
            {
                if (step.To <= previousTo)
                {
                    report.Error("upgrade-safety", path.SourceFile,
                        $"'{path.Id}' step to +{step.To} is not above the previous step (+{previousTo}).",
                        "Steps must ascend.");
                }

                previousTo = step.To;

                if (step.Chance is <= 0 or > 1)
                {
                    report.Error("upgrade-safety", path.SourceFile,
                        $"'{path.Id}' step +{step.To} has chance {step.Chance}.",
                        "Chance must be in (0,1].");
                }

                // A step that can fail must have a pity counter, or the player can be
                // stuck indefinitely — the exact failure mode the design removes.
                if (step.Chance < 1.0 && step.Pity <= 0)
                {
                    report.Error("upgrade-safety", path.SourceFile,
                        $"'{path.Id}' step +{step.To} can fail ({step.Chance:P0}) but has no pity counter.",
                        "Set pity to the number of failures after which the upgrade is guaranteed.");
                }

                if (step.To > MaxUpgradeLevel)
                {
                    report.Error("upgrade-safety", path.SourceFile,
                        $"'{path.Id}' goes to +{step.To}, above the +{MaxUpgradeLevel} cap.");
                }
            }
        }
    }

    public const int MaxUpgradeLevel = 9;

    // -- R5 -----------------------------------------------------------------
    private static void QuestGraph(ContentDatabase db, ValidationReport report)
    {
        if (db.Quests.Count == 0) return;

        // Acyclic?
        var state = new Dictionary<string, int>(StringComparer.Ordinal); // 0 unvisited, 1 in-stack, 2 done

        bool HasCycle(string id, List<string> stack)
        {
            state.TryGetValue(id, out var s);
            if (s == 1)
            {
                var from = stack.IndexOf(id);
                var cycle = string.Join(" -> ", stack.Skip(from < 0 ? 0 : from).Append(id));
                report.Error("quest-graph", db.Quests[id].SourceFile,
                    $"prerequisite cycle: {cycle}.",
                    "Break the cycle — a quest cannot transitively require itself.");
                return true;
            }

            if (s == 2) return false;

            state[id] = 1;
            stack.Add(id);

            foreach (var prereq in db.Quests[id].Prerequisites)
            {
                if (db.Quests.ContainsKey(prereq) && HasCycle(prereq, stack)) return true;
            }

            stack.RemoveAt(stack.Count - 1);
            state[id] = 2;
            return false;
        }

        foreach (var id in db.Quests.Keys.Order(StringComparer.Ordinal))
        {
            if (HasCycle(id, [])) break; // one cycle report is enough to act on
        }

        // Reachable from a root (a quest with no prerequisites)?
        var roots = db.Quests.Values.Where(q => q.Prerequisites.Length == 0).Select(q => q.Id).ToHashSet(StringComparer.Ordinal);
        if (roots.Count == 0)
        {
            report.Error("quest-graph", "game/data/quests",
                "no quest has an empty prerequisite list, so nothing can ever start.",
                "Give the prologue quest no prerequisites.");
            return;
        }

        var reachable = new HashSet<string>(roots, StringComparer.Ordinal);
        bool grew;
        do
        {
            grew = false;
            foreach (var quest in db.Quests.Values)
            {
                if (reachable.Contains(quest.Id)) continue;
                if (quest.Prerequisites.All(reachable.Contains))
                {
                    reachable.Add(quest.Id);
                    grew = true;
                }
            }
        } while (grew);

        foreach (var quest in db.Quests.Values.OrderBy(q => q.Id, StringComparer.Ordinal))
        {
            if (!reachable.Contains(quest.Id))
            {
                report.Error("quest-graph", quest.SourceFile,
                    $"'{quest.Id}' is unreachable — its prerequisites can never all complete.",
                    "Check the prerequisite chain back to a quest with no prerequisites.");
            }
        }
    }

    // -- R6 -----------------------------------------------------------------
    private static void SideQuestRewards(ContentDatabase db, ValidationReport report)
    {
        foreach (var quest in db.Quests.Values)
        {
            if (quest.Type != QuestType.Side) continue;

            var hasMechanicalReward =
                !string.IsNullOrEmpty(quest.Rewards.Unlock) || quest.Rewards.Items.Length > 0;

            if (!hasMechanicalReward)
            {
                // FR-8.4: no pure "kill 20 wolves for xp and gold" filler.
                report.Error("side-quest-reward", quest.SourceFile,
                    $"'{quest.Id}' rewards only xp/yang.",
                    "Every side quest must grant a mechanical reward: an unlock (recipe, feature, "
                    + "vendor, companion ability) or an item. Otherwise it is MMO filler.");
            }
        }
    }

    // -- R7: BAL-03 ---------------------------------------------------------
    private static void TelegraphEscape(ContentDatabase db, ValidationReport report)
    {
        foreach (var enemy in db.Enemies.Values)
        {
            foreach (var ability in enemy.Abilities)
            {
                if (ability.Telegraph is not { } tel) continue;

                var escape = PlayerConstants.TimeToEscape(tel.Radius);

                foreach (var tier in DifficultySettings.All)
                {
                    var actual = ability.Windup * tier.TelegraphScale;
                    if (actual >= escape) continue;

                    var required = escape / tier.TelegraphScale;
                    var maxRadius =
                        ((actual - PlayerConstants.ClickReactionSeconds) * PlayerConstants.BaseMoveSpeed)
                        - PlayerConstants.SafetyMarginMetres;

                    // When the wind-up is shorter than the reaction budget alone, no radius
                    // works — suggesting one would be nonsense.
                    var radiusAdvice = maxRadius > 0.1
                        ? $", or shrink the radius to {maxRadius:F1}m"
                        : " (no radius works at this wind-up — it is shorter than the player's reaction budget)";

                    report.Error("telegraph-escape", enemy.SourceFile,
                        $"'{enemy.Id}' ability '{ability.Id}': on {tier.Tier} the telegraph lasts "
                        + $"{actual:F2}s but escaping a {tel.Radius:F1}m {tel.Shape} takes {escape:F2}s.",
                        $"Raise windup to at least {required:F2}s{radiusAdvice}. "
                        + "Under click-to-move the player must have time to aim, click and walk out.");
                }
            }
        }
    }

    // -- R8 -----------------------------------------------------------------
    private static void Weights(ContentDatabase db, ValidationReport report)
    {
        foreach (var table in db.DropTables.Values)
        {
            if (table.Entries.Length > 0 && table.Entries.All(e => e.Weight <= 0))
            {
                report.Error("weights", table.SourceFile,
                    $"'{table.Id}' has no entry with a positive weight — it can never drop anything.");
            }

            foreach (var entry in table.Entries.Where(e => e.Weight < 0))
            {
                report.Error("weights", table.SourceFile,
                    $"'{table.Id}' entry '{entry.Item}' has negative weight {entry.Weight}.");
            }
        }

        foreach (var pool in db.BonusPools.Values)
        {
            if (pool.Lines.Length == 0)
            {
                report.Error("weights", pool.SourceFile, $"'{pool.Id}' has no bonus lines.");
                continue;
            }

            if (pool.Lines.All(l => l.Weight <= 0))
            {
                report.Error("weights", pool.SourceFile,
                    $"'{pool.Id}' has no line with a positive weight — nothing can ever roll.");
            }

            foreach (var line in pool.Lines.Where(l => l.Range.Length == 2 && l.Range[0] > l.Range[1]))
            {
                report.Error("weights", pool.SourceFile,
                    $"'{pool.Id}' line '{line.Id}' has an inverted range [{line.Range[0]}, {line.Range[1]}].");
            }
        }
    }

    // -- R9 -----------------------------------------------------------------
    private static void LevelRanges(ContentDatabase db, ValidationReport report)
    {
        foreach (var item in db.Items.Values.Where(i => i.LevelReq is < 0 or > MaxLevel))
        {
            report.Error("level-range", item.SourceFile,
                $"'{item.Id}' has level_req {item.LevelReq}, outside 0..{MaxLevel}.");
        }

        foreach (var enemy in db.Enemies.Values.Where(e => e.Level is < 1 or > MaxLevel + 5))
        {
            report.Error("level-range", enemy.SourceFile,
                $"'{enemy.Id}' has level {enemy.Level}, outside 1..{MaxLevel + 5}.");
        }

        foreach (var skill in db.Skills.Values.Where(s => s.UnlockLevel is < 1 or > MaxLevel))
        {
            report.Error("level-range", skill.SourceFile,
                $"'{skill.Id}' unlocks at level {skill.UnlockLevel}, outside 1..{MaxLevel}.");
        }

        foreach (var quest in db.Quests.Values.Where(q => q.LevelReq is < 0 or > MaxLevel))
        {
            report.Error("level-range", quest.SourceFile,
                $"'{quest.Id}' has level_req {quest.LevelReq}, outside 0..{MaxLevel}.");
        }
    }
}
