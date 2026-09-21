using Kiln.Core.Combat;
using Kiln.Core.Encounters;
using Kiln.Core.Foundation;
using Kiln.Core.Items;
using Kiln.Core.World;
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
        QuestChainRules(db, report);
        TelegraphEscape(db, report);
        Weights(db, report);
        LevelRanges(db, report);
        StatKeys(db, report);
        RarityShape(db, report);
        ShardEncounters(db, report);
        WorldGraph(db, report);
        GreyboxKit(db, report);
        Villagers(db, report);
        Sounds(db, report);

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
        foreach (var d in db.Npcs.Values)
        {
            Check(d.SourceFile, d.Id, d.Name);
            Check(d.SourceFile, d.Id, d.Title);

            foreach (var line in d.Lines.Concat(d.QuestLines.Values)) Check(d.SourceFile, d.Id, line);
        }

        // Every key has English. Checked only when an English table exists at all, so rule
        // tests built from a handful of definitions need not carry one.
        if (!db.Strings.TryGetValue(L10n.English, out var english)) return;

        foreach (var def in db.All())
        {
            foreach (var key in KeysIn(def).Distinct(StringComparer.Ordinal).Where(k => !english.ContainsKey(k)))
            {
                report.Error("localisation", def.SourceFile,
                    $"'{def.Id}' uses {key}, which data/strings/en.json does not define.",
                    "Add the English, or run: dotnet run --project src/Kiln.Tools -- strings --seed-en");
            }
        }
    }

    /// <summary>
    /// Every <c>$key</c> anywhere inside a definition, however deeply nested.
    /// </summary>
    /// <remarks>
    /// Walks the object rather than listing fields, so a new text field added to a definition
    /// next month is checked without anyone remembering to add it here.
    /// </remarks>
    private static IEnumerable<string> KeysIn(object? value, int depth = 0)
    {
        if (value is null || depth > 8) yield break;

        switch (value)
        {
            case string text:
                if (text.StartsWith('$')) yield return text;
                yield break;

            case System.Collections.IDictionary map:
                foreach (var entry in map.Values)
                {
                    foreach (var key in KeysIn(entry, depth + 1)) yield return key;
                }

                yield break;

            case System.Collections.IEnumerable list:
                foreach (var entry in list)
                {
                    foreach (var key in KeysIn(entry, depth + 1)) yield return key;
                }

                yield break;
        }

        var type = value.GetType();

        if (type.Namespace != typeof(ItemDef).Namespace) yield break;

        foreach (var property in type.GetProperties())
        {
            if (property.GetIndexParameters().Length > 0 || property.Name == nameof(IContentDef.SourceFile)) continue;

            foreach (var key in KeysIn(property.GetValue(value), depth + 1)) yield return key;
        }
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

    /// <summary>
    /// The main quest chain (FR-8, doc 02 §9): a line, mostly hunts, every quest finishable.
    /// </summary>
    /// <remarks>
    /// Each rule here is something that loads and runs fine and then strands a player hours in.
    /// A kill quest for a creature no map spawns can never be finished, and on a chain that is
    /// the whole rest of the game. Two quests sharing a prerequisite means two active quests,
    /// which the HUD has no room for and the design has no place for.
    /// </remarks>
    private static void QuestChainRules(ContentDatabase db, ValidationReport report)
    {
        if (db.Quests.Count == 0) return;

        var spawned = Spawnable(db);
        var allowed = new[] { ObjectiveType.Kill, ObjectiveType.Reach, ObjectiveType.Shard, ObjectiveType.ClearTower };
        var followers = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var quest in db.Quests.Values.OrderBy(q => q.Id, StringComparer.Ordinal))
        {
            if (quest.Type == QuestType.Side)
            {
                report.Error("quest-chain", quest.SourceFile,
                    $"'{quest.Id}' is a side quest.",
                    "There are no side quests (FR-8.4). Put it on the main chain or drop it.");
            }

            if (quest.Prerequisites.Length > 1)
            {
                report.Error("quest-chain", quest.SourceFile,
                    $"'{quest.Id}' has {quest.Prerequisites.Length} prerequisites.",
                    "The chain is a line: each quest follows exactly one other, or none for the first.");
            }

            foreach (var prereq in quest.Prerequisites)
            {
                if (followers.TryGetValue(prereq, out var other))
                {
                    report.Error("quest-chain", quest.SourceFile,
                        $"'{quest.Id}' and '{other}' both follow '{prereq}'.",
                        "Finishing one quest must start exactly one more (FR-8.3: one active quest).");
                }
                else
                {
                    followers[prereq] = quest.Id;
                }
            }

            if (quest.Objectives.Length == 0)
            {
                report.Error("quest-chain", quest.SourceFile, $"'{quest.Id}' has no objectives.",
                    "A quest with nothing to do can never finish, and the chain stops there.");
            }

            foreach (var objective in quest.Objectives)
            {
                QuestObjective(db, quest, objective, allowed, spawned, report);
            }
        }
    }

    private static void QuestObjective(
        ContentDatabase db, QuestDef quest, ObjectiveDef objective, ObjectiveType[] allowed,
        HashSet<string> spawned, ValidationReport report)
    {
        var where = $"'{quest.Id}'";

        if (!allowed.Contains(objective.Type))
        {
            report.Error("quest-chain", quest.SourceFile,
                $"{where} uses objective '{objective.Type}'.",
                "The chain uses kill, reach, shard and clear_tower. The rest went with the side quests (doc 02 §9).");
            return;
        }

        if (objective.Count < 1 || (objective.Type != ObjectiveType.Kill && objective.Count != 1))
        {
            report.Error("quest-chain", quest.SourceFile,
                $"{where} asks for {objective.Count} of a '{objective.Type}' objective.",
                "Kills take a count of one or more; everything else is done once.");
        }

        switch (objective.Type)
        {
            case ObjectiveType.Kill when db.Enemies.ContainsKey(objective.Target) && !spawned.Contains(objective.Target):
                report.Error("quest-chain", quest.SourceFile,
                    $"{where} hunts '{objective.Target}', which no camp or tower floor ever spawns.",
                    "Add it to a spawn field or a floor's waves, or hunt something that exists in the world.");
                break;

            case ObjectiveType.Reach when !db.Zones.ContainsKey(objective.Target):
                report.Error("cross-ref", quest.SourceFile, $"{where} sends the player to unknown zone '{objective.Target}'.");
                break;

            case ObjectiveType.Shard when !db.Shards.ContainsKey(objective.Target):
                report.Error("cross-ref", quest.SourceFile, $"{where} asks for unknown shard '{objective.Target}'.");
                break;

            case ObjectiveType.ClearTower when !db.Zones.TryGetValue(objective.Target, out var tower) || tower.Floors.Length == 0:
                report.Error("quest-chain", quest.SourceFile,
                    $"{where} asks to clear '{objective.Target}', which is not a tower.",
                    "clear_tower names a dungeon zone that declares floors.");
                break;
        }
    }

    /// <summary>Every creature the world can put in front of the player: camps and tower waves.</summary>
    /// <remarks>Bosses count — a floor's boss is as findable as a camp.</remarks>
    private static HashSet<string> Spawnable(ContentDatabase db)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);

        foreach (var zone in db.Zones.Values)
        {
            foreach (var field in zone.SpawnFields)
            {
                foreach (var entry in field.Entries) found.Add(entry.Enemy);
            }

            foreach (var floor in zone.Floors)
            {
                foreach (var enemy in floor.Waves) found.Add(enemy);

                if (floor.Boss.Length > 0) found.Add(floor.Boss);
            }
        }

        return found;
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

    // -- R10 ----------------------------------------------------------------
    /// <summary>
    /// Every stat a bonus line or an item names must resolve to a real modifier.
    /// <para>
    /// This is the highest-value rule in the file per line of code. A mistyped stat key does
    /// not crash and does not look wrong: the item drops, the tooltip lists the line, and it
    /// simply does nothing. Without this check that survives to release.
    /// </para>
    /// </summary>
    private static void StatKeys(ContentDatabase db, ValidationReport report)
    {
        foreach (var pool in db.BonusPools.Values)
        {
            foreach (var line in pool.Lines)
            {
                if (BonusStat.TryParse(line.Stat, out _, out var error)) continue;

                report.Error("stat-key", pool.SourceFile,
                    $"'{pool.Id}' line '{line.Id}': {error}.",
                    "See BonusStat for the accepted keys, e.g. damage_pct, max_hp_flat, vs_family.undead.");
            }
        }

        // base_stats may name the item frame or any modifier the item grants outright.
        string[] frameKeys = ["weapon_damage_min", "weapon_damage_max", "armor_value"];

        foreach (var item in db.Items.Values)
        {
            foreach (var key in item.BaseStats.Keys)
            {
                if (frameKeys.Contains(key)) continue;
                if (BonusStat.TryParse(key, out _, out var error)) continue;

                report.Error("stat-key", item.SourceFile,
                    $"'{item.Id}' base_stats '{key}': {error}.",
                    "Use a frame key (weapon_damage_min/max, armor_value) or a modifier key.");
            }
        }
    }

    // -- R11 ----------------------------------------------------------------
    /// <summary>
    /// Sockets and bonus-line counts must follow the rarity table (doc 06 §5, FR-5.6).
    /// <para>
    /// Rarity is the player's one-glance promise about what an item is worth. It only works
    /// if it is never contradicted, so the table is enforced rather than remembered.
    /// </para>
    /// </summary>
    private static void RarityShape(ContentDatabase db, ValidationReport report)
    {
        foreach (var item in db.Items.Values.Where(i => i.Slot is not null))
        {
            var (sockets, minLines, maxLines) = item.Rarity switch
            {
                Rarity.Common => (0, 0, 1),
                Rarity.Fine => (1, 2, 2),
                Rarity.Rare => (2, 3, 3),
                Rarity.Epic => (2, 4, 4),
                Rarity.Relic => (3, 5, 5),
                _ => (0, 0, 0),
            };

            if (item.Sockets != sockets)
            {
                report.Error("rarity-shape", item.SourceFile,
                    $"'{item.Id}' is {item.Rarity} with {item.Sockets} sockets; the table says {sockets}.");
            }

            var low = item.BonusLineCount.Length > 0 ? item.BonusLineCount[0] : 0;
            var high = item.BonusLineCount.Length > 1 ? item.BonusLineCount[1] : low;

            if (low < minLines || high > maxLines)
            {
                report.Error("rarity-shape", item.SourceFile,
                    $"'{item.Id}' is {item.Rarity} with bonus_line_count [{low}, {high}]; "
                    + $"the table allows {minLines}..{maxLines}.");
            }
        }
    }

    // -- R12 ----------------------------------------------------------------
    /// <summary>
    /// Shard fights must be survivable and completable (SHD-02/03, doc 02 §3).
    /// <para>
    /// The radial pulse is a telegraph like any other and gets the same BAL-03 treatment: on
    /// every difficulty the player must have time to see it, aim and walk out. A shard is the
    /// one fight the player cannot simply leave, so an unescapable pulse is not a hard fight,
    /// it is an unwinnable one.
    /// </para>
    /// </summary>
    private static void ShardEncounters(ContentDatabase db, ValidationReport report)
    {
        foreach (var shard in db.Shards.Values)
        {
            var escape = PlayerConstants.TimeToEscape(shard.PulseRadius);

            foreach (var tier in DifficultySettings.All)
            {
                var actual = shard.PulseWindup * tier.TelegraphScale;

                if (actual >= escape) continue;

                report.Error("telegraph-escape", shard.SourceFile,
                    $"'{shard.Id}' pulse: on {tier.Tier} the telegraph lasts {actual:F2}s but "
                    + $"escaping a {shard.PulseRadius:F1}m ring takes {escape:F2}s.",
                    $"Raise pulse_windup to at least {escape / tier.TelegraphScale:F2}s, or shrink the radius.");
            }

            // The rhythm the fight is built on — clear adds, hit the shard, walk out — needs
            // a window between pulses. An interval shorter than the wind-up means the next
            // telegraph starts before the last one lands and the player never gets a turn.
            if (shard.PulseInterval <= shard.PulseWindup)
            {
                report.Error("shard-pacing", shard.SourceFile,
                    $"'{shard.Id}' pulses every {shard.PulseInterval:F1}s with a {shard.PulseWindup:F1}s wind-up.",
                    "Leave a window between pulses or there is no time to attack the shard.");
            }

            foreach (var phase in (ShardPhase[])[ShardPhase.One, ShardPhase.Two, ShardPhase.Three])
            {
                var wave = ToWave(shard, phase);

                if (wave is null)
                {
                    report.Error("shard-waves", shard.SourceFile,
                        $"'{shard.Id}' has no wave for phase {phase.ToString().ToLowerInvariant()}.");
                    continue;
                }

                // Exactly one anchor in phase three. None means the reclamation cast cannot be
                // interrupted and the fight becomes a pure damage race; more than one halves
                // the pressure the beat exists to create.
                if (phase != ShardPhase.Three) continue;

                if (!WaveComposer.HasSingleAnchor(wave))
                {
                    report.Error("shard-waves", shard.SourceFile,
                        $"'{shard.Id}' phase three does not name exactly one anchor add.",
                        "The anchor is the interrupt; without it the reclamation cast cannot be stopped.");
                }
            }

            foreach (var name in shard.Modifiers)
            {
                if (Enum.TryParse<ShardModifier>(name, ignoreCase: true, out var modifier)
                    && Enum.IsDefined(modifier)
                    && modifier != ShardModifier.None)
                {
                    continue;
                }

                report.Error("shard-modifier", shard.SourceFile,
                    $"'{shard.Id}' lists unknown modifier '{name}'.",
                    "Use frenzied, warded, venomous or twin.");
            }

            if (shard.DropTable is not null && !db.DropTables.ContainsKey(shard.DropTable))
            {
                report.Error("cross-ref", shard.SourceFile,
                    $"'{shard.Id}' references drop table '{shard.DropTable}', which does not exist.");
            }
        }
    }

    private static ShardWave? ToWave(ShardDef shard, ShardPhase phase)
    {
        foreach (var wave in shard.Waves)
        {
            if (!Encounters.ShardCatalogue.TryParsePhase(wave.Phase, out var parsed) || parsed != phase) continue;

            var slots = new List<WaveSlot>();

            foreach (var slot in wave.Slots)
            {
                if (Enum.TryParse<EnemyRole>(slot.Role, ignoreCase: true, out var role))
                {
                    slots.Add(new WaveSlot(role, Math.Max(1, slot.Count), slot.Anchor));
                }
            }

            return new ShardWave(phase, slots);
        }

        return null;
    }

    // -- R13 ----------------------------------------------------------------
    /// <summary>
    /// The world has to be a world (WLD-01/02/03).
    /// </summary>
    /// <remarks>
    /// Every failure here is invisible in a scene file and obvious in play: a zone no path
    /// reaches, a door to a zone that was renamed, a level nothing is built for, a zone you
    /// can die in with nowhere to come back to, a camp of creatures six levels above the band
    /// they were placed in. The graph is small enough to check exhaustively, so there is no
    /// reason to find any of them by walking there.
    /// </remarks>
    private static void WorldGraph(ContentDatabase db, ValidationReport report)
    {
        if (db.Zones.Count == 0) return;

        var catalogue = new World.WorldCatalogue(db);
        var graph = catalogue.Graph;
        var hubs = db.Zones.Values.Where(z => World.WorldCatalogue.KindOf(z.Kind) == ZoneKind.Hub).ToList();

        // At least one, not exactly one (WLD-13). The world is a chain of villages with fields
        // between them; the rule that said "exactly one" was written when one village was the
        // whole plan, and would now reject the shape the game is being built towards.
        if (hubs.Count == 0)
        {
            report.Error("world-graph", "zones/",
                "the world has no hub zone.",
                "At least one zone must be kind \"hub\": it is where the player starts and where death leads.");
        }

        foreach (var id in graph.Orphans())
        {
            report.Error("world-graph", db.Zones[id].SourceFile,
                $"'{id}' cannot be reached from the hub by any chain of exits.",
                "Add an exit leading to it, or delete it.");
        }

        var gaps = graph.BandGaps();

        if (gaps.Count > 0)
        {
            report.Error("world-bands", "zones/",
                $"no zone is built for level{(gaps.Count > 1 ? "s" : "")} {string.Join(", ", gaps)}.",
                "Widen a neighbouring band. A gap leaves the player with nothing to do but grind a zone that has stopped paying.");
        }

        var shrineIds = new HashSet<string>(StringComparer.Ordinal);
        var safeIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var zone in db.Zones.Values)
        {
            var kind = World.WorldCatalogue.KindOf(zone.Kind);

            // Declared but not yet laid out. A warning rather than an error so the graph can be
            // designed ahead of the greybox, and so the count of unbuilt zones stays visible in
            // every CI run instead of living in someone's head.
            if (string.IsNullOrEmpty(zone.Scene))
            {
                report.Warn("world-graph", zone.SourceFile,
                    $"'{zone.Id}' has no scene yet (WLD-05/06/07).");
            }

            ZoneExits(db, zone, report);
            ZoneShrines(zone, kind, shrineIds, report);
            ZoneSafeRegions(zone, kind, safeIds, report);
            ZoneFields(db, zone, report);
            ZoneFloors(db, zone, kind, report);

            foreach (var shard in zone.Shards)
            {
                if (!db.Shards.ContainsKey(shard))
                {
                    report.Error("cross-ref", zone.SourceFile,
                        $"'{zone.Id}' hosts shard '{shard}', which does not exist.");
                }
            }
        }
    }

    private static void ZoneExits(ContentDatabase db, ZoneDef zone, ValidationReport report)
    {
        foreach (var exit in zone.Exits)
        {
            if (!db.Zones.TryGetValue(exit.To, out var target))
            {
                report.Error("cross-ref", zone.SourceFile,
                    $"'{zone.Id}' has an exit to '{exit.To}', which does not exist.");
                continue;
            }

            if (exit.RequiredQuest is { } quest && !db.Quests.ContainsKey(quest))
            {
                report.Error("cross-ref", zone.SourceFile,
                    $"'{zone.Id}' gates its exit to '{exit.To}' behind quest '{quest}', which does not exist.");
            }

            // A one-way door out of open country is almost always a missing line rather than a
            // design choice; a dungeon mouth legitimately is one, so only the wilds are warned.
            if (World.WorldCatalogue.KindOf(target.Kind) == ZoneKind.Dungeon) continue;

            if (!target.Exits.Any(back => back.To == zone.Id))
            {
                report.Warn("world-graph", zone.SourceFile,
                    $"'{zone.Id}' exits to '{exit.To}' but '{exit.To}' has no way back.",
                    "Add the return exit, unless the one-way trip is deliberate.");
            }
        }
    }

    private static void ZoneShrines(
        ZoneDef zone, ZoneKind kind, HashSet<string> seen, ValidationReport report)
    {
        // The hub is a shrine-shaped place in its own right, but everywhere the player can die
        // needs somewhere to come back to, or death has no defined answer.
        if (zone.Shrines.Length == 0 && kind != ZoneKind.Hub)
        {
            report.Error("world-shrines", zone.SourceFile,
                $"'{zone.Id}' has no shrine.",
                "Every zone the player can die in needs a respawn point.");
        }

        foreach (var shrine in zone.Shrines)
        {
            if (!ContentId.IsValid(shrine.Id))
            {
                report.Error("id-format", zone.SourceFile,
                    $"shrine '{shrine.Id}' in '{zone.Id}' is not a valid id.",
                    "Use lowercase prefix_name, e.g. 'shr_vale_gate'.");
            }

            if (!seen.Add(shrine.Id))
            {
                report.Error("world-shrines", zone.SourceFile,
                    $"duplicate shrine id '{shrine.Id}'.");
            }

            if (!string.IsNullOrEmpty(shrine.Name) && !shrine.Name.StartsWith('$'))
            {
                report.Error("localisation", zone.SourceFile,
                    $"shrine '{shrine.Id}' has a literal name \"{shrine.Name}\".",
                    "Player-facing text must be a $localisation.key (NFR-L.1).");
            }
        }
    }

    /// <summary>
    /// Safe ground: a village is a region inside a map, and the rule has to be checkable
    /// (WLD-12).
    /// </summary>
    /// <remarks>
    /// A hub zone is required to declare one rather than being safe by virtue of its kind. Two
    /// ways of being safe would mean two implementations of "am I inside the village", and the
    /// day they disagreed the player would find out by dying somewhere the map called safe.
    /// </remarks>
    private static void ZoneSafeRegions(
        ZoneDef zone, ZoneKind kind, HashSet<string> seen, ValidationReport report)
    {
        // The upper bound is not arbitrary taste: a region wider than this covers a field map
        // whole, and the reachable world quietly stops having anywhere dangerous in it.
        const double MaxRadius = 120.0;

        if (kind == ZoneKind.Hub && zone.SafeRegions.Length == 0)
        {
            report.Error("world-safe", zone.SourceFile,
                $"the hub '{zone.Id}' declares no safe region.",
                "A village is safe ground with a radius, not a zone kind. Add one covering it.");
        }

        if (kind == ZoneKind.Dungeon && zone.SafeRegions.Length > 0)
        {
            report.Error("world-safe", zone.SourceFile,
                $"the dungeon '{zone.Id}' declares safe ground.",
                "A dungeon offers checkpoints, not sanctuary; somewhere to stand and heal would undo the run.");
        }

        foreach (var region in zone.SafeRegions)
        {
            if (!ContentId.IsValid(region.Id))
            {
                report.Error("id-format", zone.SourceFile,
                    $"safe region '{region.Id}' in '{zone.Id}' is not a valid id.",
                    "Use lowercase prefix_name, e.g. 'saf_ember_hollow'.");
            }

            if (!seen.Add(region.Id))
            {
                report.Error("world-safe", zone.SourceFile,
                    $"duplicate safe region id '{region.Id}'.");
            }

            if (!string.IsNullOrEmpty(region.Name) && !region.Name.StartsWith('$'))
            {
                report.Error("localisation", zone.SourceFile,
                    $"safe region '{region.Id}' has a literal name \"{region.Name}\".",
                    "Player-facing text must be a $localisation.key (NFR-L.1).");
            }

            if (region.Radius <= 0 || region.Radius > MaxRadius)
            {
                report.Error("world-safe", zone.SourceFile,
                    $"safe region '{region.Id}' has radius {region.Radius}.",
                    $"Use a radius between 0 and {MaxRadius} metres.");
            }
        }
    }


    /// <summary>
    /// The rules that make a tower a tower (FR-7.11–7.20).
    /// </summary>
    /// <remarks>
    /// Checked in data rather than left to review because every one of these fails silently.
    /// Nine floors that all ask the same thing still load, still run, and still ship; the
    /// player is the one who finds out, four floors in, that the dungeon has one idea.
    /// <para>
    /// FR-7.12 is the load-bearing one and it is the reason this method exists at all. The
    /// rest are cheap to check once the list is in front of you.
    /// </para>
    /// </remarks>
    private static void ZoneFloors(ContentDatabase db, ZoneDef zone, ZoneKind kind, ValidationReport report)
    {
        if (zone.Floors.Length == 0)
        {
            if (kind == ZoneKind.Dungeon)
            {
                report.Warn("world-floors", zone.SourceFile,
                    $"the dungeon '{zone.Id}' declares no floors (FR-7.11).");
            }

            return;
        }

        if (kind != ZoneKind.Dungeon)
        {
            report.Error("world-floors", zone.SourceFile,
                $"'{zone.Id}' declares floors but is not a dungeon.",
                "Floors are a dungeon's format. Set \"kind\": \"dungeon\", or drop the floors.");
        }

        var verbs = new[] { "break", "hold", "find", "carry", "race", "fight" };
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var shrines = zone.Shrines.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var previous = "";

        for (var i = 0; i < zone.Floors.Length; i++)
        {
            var floor = zone.Floors[i];
            var depth = i + 1;
            var where = $"floor {depth} ('{floor.Id}') of '{zone.Id}'";

            if (!ContentId.IsValid(floor.Id))
            {
                report.Error("id-format", zone.SourceFile,
                    $"{where} is not a valid id.",
                    "Use lowercase prefix_name, e.g. 'flr_catacombs_01'.");
            }

            if (!ids.Add(floor.Id))
            {
                report.Error("world-floors", zone.SourceFile, $"duplicate floor id '{floor.Id}'.");
            }

            if (!string.IsNullOrEmpty(floor.Name) && !floor.Name.StartsWith('$'))
            {
                report.Error("localisation", zone.SourceFile,
                    $"{where} has a literal name \"{floor.Name}\".",
                    "Player-facing text must be a $localisation.key (NFR-L.1).");
            }

            if (!verbs.Contains(floor.Task))
            {
                report.Error("world-floors", zone.SourceFile,
                    $"{where} has unknown task \"{floor.Task}\".",
                    $"One of: {string.Join(", ", verbs)}. There is deliberately no \"clear\" (FR-7.17).");

                previous = floor.Task;
                continue;
            }

            // FR-7.12, the half of it that is local. The other half — five distinct verbs
            // across the tower — is counted below.
            if (floor.Task == previous)
            {
                report.Error("world-floors", zone.SourceFile,
                    $"{where} repeats the task of the floor above it (\"{floor.Task}\").",
                    "FR-7.12: no floor may ask for the same thing as the one before it.");
            }

            previous = floor.Task;

            // FR-7.13. Stated as an equivalence rather than two rules, because a boss on the
            // fourth floor breaks the pulse just as surely as a missing one on the third.
            var shouldFight = depth % 3 == 0;

            if (shouldFight && floor.Task != "fight")
            {
                report.Error("world-floors", zone.SourceFile,
                    $"{where} is every third floor but its task is \"{floor.Task}\".",
                    "FR-7.13: a boss floor every third floor. It is what gives the climb a pulse.");
            }
            else if (!shouldFight && floor.Task == "fight")
            {
                report.Error("world-floors", zone.SourceFile,
                    $"{where} is a boss floor out of step with the third-floor pulse.",
                    "FR-7.13: bosses belong on floors 3, 6, 9 and so on.");
            }

            FloorShape(db, zone, floor, where, shrines, report);
        }

        var distinct = zone.Floors.Select(f => f.Task).Distinct().Count();

        if (distinct < 5)
        {
            report.Error("world-floors", zone.SourceFile,
                $"'{zone.Id}' uses only {distinct} distinct floor task{(distinct == 1 ? "" : "s")}.",
                "FR-7.12: at least five. Fewer, and the tower is one floor repeated.");
        }

        BossAftermath(zone, report);

        // SHOULD, not MUST — and cheap enough that a missing one is worth saying out loud.
        if (!zone.Floors.Any(f => f.Refuge))
        {
            report.Warn("world-floors", zone.SourceFile,
                $"'{zone.Id}' has no floor with a refuge (FR-7.18).");
        }
    }

    /// <summary>The fields a floor's task does and does not make sense of.</summary>
    private static void FloorShape(
        ContentDatabase db, ZoneDef zone, FloorDef floor, string where,
        HashSet<string> shrines, ValidationReport report)
    {
        var clocked = floor.Task is "hold" or "race";

        if (clocked && floor.Seconds <= 0)
        {
            report.Error("world-floors", zone.SourceFile,
                $"{where} is a \"{floor.Task}\" floor with no duration.",
                "A hold needs something to survive until; a race needs something to beat.");
        }

        if (!clocked && floor.Seconds > 0)
        {
            report.Error("world-floors", zone.SourceFile,
                $"{where} sets seconds on a \"{floor.Task}\" floor, which has no clock.");
        }

        if (floor.Task == "find" && floor.Decoys < 1)
        {
            report.Error("world-floors", zone.SourceFile,
                $"{where} is a \"find\" floor with no lookalikes.",
                "Without decoys there is nothing to tell apart.");
        }

        if (floor.Task != "find" && floor.Decoys > 0)
        {
            report.Error("world-floors", zone.SourceFile,
                $"{where} sets decoys on a \"{floor.Task}\" floor.");
        }

        // A carry floor with one key is a break floor that made you walk.
        if (floor.Task == "carry" && floor.Targets < 2)
        {
            report.Error("world-floors", zone.SourceFile,
                $"{where} is a \"carry\" floor asking for {floor.Targets} key.",
                "Carry needs at least two, or it is a break floor with extra steps.");
        }

        if (floor.Task == "fight")
        {
            if (string.IsNullOrEmpty(floor.Boss) || !db.Enemies.ContainsKey(floor.Boss))
            {
                report.Error("cross-ref", zone.SourceFile,
                    $"{where} is a boss floor whose boss '{floor.Boss}' does not exist.");
            }

            // FR-7.13 says alone on an otherwise empty floor. Adds would make it a hold.
            if (floor.Waves.Length > 0)
            {
                report.Error("world-floors", zone.SourceFile,
                    $"{where} is a boss floor that also sends waves.",
                    "FR-7.13: the boss stands alone.");
            }
        }
        else if (!string.IsNullOrEmpty(floor.Boss))
        {
            report.Error("world-floors", zone.SourceFile,
                $"{where} names a boss but its task is \"{floor.Task}\".");
        }

        foreach (var enemy in floor.Waves)
        {
            if (!db.Enemies.ContainsKey(enemy))
            {
                report.Error("cross-ref", zone.SourceFile,
                    $"{where} sends '{enemy}', which does not exist.");
            }
        }

        if (!string.IsNullOrEmpty(floor.Shrine) && !shrines.Contains(floor.Shrine))
        {
            report.Error("cross-ref", zone.SourceFile,
                $"{where} stands shrine '{floor.Shrine}', which '{zone.Id}' does not declare.");
        }
    }

    /// <summary>
    /// A shrine and a bench immediately after every boss floor (FR-7.14).
    /// </summary>
    /// <remarks>
    /// The original's best idea, and the reason is worth keeping in the error text: it is a
    /// reward that is a decision, handed over at the exact moment the player has just learned
    /// which piece of their kit is holding them back.
    /// <para>
    /// A boss on the last floor is exempt. There is no floor after it, and the way out of the
    /// tower is not a floor.
    /// </para>
    /// </remarks>
    private static void BossAftermath(ZoneDef zone, ValidationReport report)
    {
        for (var i = 0; i < zone.Floors.Length - 1; i++)
        {
            if (zone.Floors[i].Task != "fight") continue;

            var after = zone.Floors[i + 1];

            if (string.IsNullOrEmpty(after.Shrine))
            {
                report.Error("world-floors", zone.SourceFile,
                    $"floor {i + 2} ('{after.Id}') of '{zone.Id}' follows a boss but has no shrine.",
                    "FR-7.14: a shrine and a bench immediately after every boss floor.");
            }

            if (!after.Bench)
            {
                report.Error("world-floors", zone.SourceFile,
                    $"floor {i + 2} ('{after.Id}') of '{zone.Id}' follows a boss but has no bench.",
                    "FR-7.14: the upgrade decision is the reward, and this is the moment it means most.");
            }
        }
    }
    private static void ZoneFields(ContentDatabase db, ZoneDef zone, ValidationReport report)
    {
        // A hub's camps used to be an error, back when "hub" meant the whole map was safe.
        // Since WLD-12 safety is a region with a radius, so a village map is expected to have
        // creatures around the village — and the distance between the two is enforced against
        // real coordinates by ZoneRoot.Audit, which is the only place that can see them.
        var band = zone.LevelBand.Length > 1 ? zone.LevelBand : [1, 1];

        foreach (var field in zone.SpawnFields)
        {
            if (!ContentId.IsValid(field.Id))
            {
                report.Error("id-format", zone.SourceFile,
                    $"spawn field '{field.Id}' in '{zone.Id}' is not a valid id.");
            }

            if (field.Entries.Length == 0)
            {
                report.Error("world-spawns", zone.SourceFile,
                    $"spawn field '{field.Id}' lists no enemies.");
            }

            if (field.Count < 1 || field.RespawnSeconds <= 0)
            {
                report.Error("world-spawns", zone.SourceFile,
                    $"spawn field '{field.Id}' has count {field.Count} and respawn {field.RespawnSeconds}s.",
                    "Both must be positive or the field produces nothing.");
            }

            // Creatures scattered outside the radius that governs whether the field is awake
            // would pop in and out of existence around its edge.
            if (field.Radius >= field.ActivationRadius)
            {
                report.Error("world-spawns", zone.SourceFile,
                    $"spawn field '{field.Id}' scatters {field.Radius:F0}m but only activates within "
                    + $"{field.ActivationRadius:F0}m.",
                    "The activation radius must comfortably exceed the scatter radius.");
            }

            foreach (var entry in field.Entries)
            {
                if (!db.Enemies.TryGetValue(entry.Enemy, out var enemy))
                {
                    report.Error("cross-ref", zone.SourceFile,
                        $"spawn field '{field.Id}' places '{entry.Enemy}', which does not exist.");
                    continue;
                }

                if (entry.Weight <= 0)
                {
                    report.Error("weights", zone.SourceFile,
                        $"spawn field '{field.Id}' gives '{entry.Enemy}' weight {entry.Weight}.",
                        "A non-positive weight means it can never be chosen; remove the entry instead.");
                }

                // The band is the only difficulty signal a single-player world has. An enemy
                // placed outside it makes the band a lie wherever that field happens to sit.
                if (enemy.Level < band[0] - 1 || enemy.Level > band[1] + 1)
                {
                    report.Error("world-bands", zone.SourceFile,
                        $"spawn field '{field.Id}' places level-{enemy.Level} '{entry.Enemy}' in "
                        + $"'{zone.Id}', a level {band[0]}–{band[1]} zone.",
                        "Move the field, or widen the zone's band to match what stands in it.");
                }
            }
        }
    }

    // -- R14 ----------------------------------------------------------------
    /// <summary>
    /// The greybox kit (WLD-04).
    /// </summary>
    /// <remarks>
    /// Cheap rules for an expensive mistake. A piece with a zero dimension is invisible, and
    /// a scene full of invisible walls is debugged by walking into them; a shape the registry
    /// does not know silently falls back to a box, so a ramp becomes a step the player cannot
    /// climb and the level looks finished while being impassable.
    /// </remarks>
    private static void GreyboxKit(ContentDatabase db, ValidationReport report)
    {
        string[] shapes = ["box", "ramp", "cylinder", "sphere", "arch", "model"];

        foreach (var piece in db.KitPieces.Values)
        {
            if (!shapes.Contains(piece.Shape))
            {
                report.Error("kit-shape", piece.SourceFile,
                    $"'{piece.Id}' has shape '{piece.Shape}'.",
                    $"Use one of: {string.Join(", ", shapes)}.");
            }

            if (piece.Shape == "model" && string.IsNullOrEmpty(piece.ModelPath))
            {
                report.Error("kit-shape", piece.SourceFile,
                    $"'{piece.Id}' is a model with no model_path.");
            }

            if (piece.Size.Length != 3 || piece.Size.Any(v => v <= 0))
            {
                report.Error("kit-size", piece.SourceFile,
                    $"'{piece.Id}' has size [{string.Join(", ", piece.Size)}].",
                    "Every piece needs three positive dimensions in metres.");
            }

            // Baking geometry the player walks through would carve holes in the navmesh
            // around decoration, and enemies would path into shrubbery and stop.
            if (piece.Navigation && !piece.Solid)
            {
                report.Error("kit-shape", piece.SourceFile,
                    $"'{piece.Id}' is navigation geometry but not solid.",
                    "Navigation is baked from collision, so a piece cannot be one without the other.");
            }

            if (!piece.Color.StartsWith('#') || piece.Color.Length is not (4 or 7))
            {
                report.Error("kit-colour", piece.SourceFile,
                    $"'{piece.Id}' has colour \"{piece.Color}\".",
                    "Use #rgb or #rrggbb.");
            }
        }
    }

    // -- villagers (QST-06) ---------------------------------------------------

    /// <summary>
    /// A villager stands somewhere real, says something, and — if a merchant — sells things
    /// that exist and are not better than what the world hands out.
    /// </summary>
    private static void Villagers(ContentDatabase db, ValidationReport report)
    {
        foreach (var npc in db.Npcs.Values.OrderBy(n => n.Id, StringComparer.Ordinal))
        {
            var at = npc.SourceFile;

            if (!db.Zones.ContainsKey(npc.Zone))
            {
                report.Error("npc", at, $"'{npc.Id}' stands in zone '{npc.Zone}', which does not exist.",
                    "Name the zone whose scene places this villager.");
            }

            if (!db.Visuals.ContainsKey(npc.Visual))
            {
                report.Error("npc", at, $"'{npc.Id}' uses visual '{npc.Visual}', which does not exist.",
                    "Add it to tables/visuals.json or pick an existing one.");
            }

            if (npc.Lines.Length == 0)
            {
                report.Error("npc", at, $"'{npc.Id}' has nothing to say.",
                    "Give every villager at least one line (FR-8.2).");
            }

            foreach (var quest in npc.QuestLines.Keys.Where(q => !db.Quests.ContainsKey(q)))
            {
                report.Error("npc", at, $"'{npc.Id}' has a line for quest '{quest}', which does not exist.",
                    "Correct the quest id or drop the line.");
            }

            if (npc.Role != NpcRole.Merchant)
            {
                if (npc.Stock is not null)
                {
                    report.Error("npc-stock", at, $"'{npc.Id}' has stock but is not a merchant.",
                        "Set role to merchant, or remove the stock.");
                }

                continue;
            }

            if (npc.Stock is not { } stock || (stock.Staples.Length == 0 && stock.Rotating <= 0))
            {
                report.Error("npc-stock", at, $"'{npc.Id}' is a merchant with nothing to sell.",
                    "Give it staples, a rotating shelf, or both.");
                continue;
            }

            foreach (var staple in stock.Staples)
            {
                if (!db.Items.TryGetValue(staple, out var item))
                {
                    report.Error("npc-stock", at, $"'{npc.Id}' sells '{staple}', which does not exist.",
                        "Correct the item id.");
                }
                else if (item.Slot is not null)
                {
                    report.Error("npc-stock", at, $"'{npc.Id}' sells '{staple}' as a staple, but it is equipment.",
                        "Staples are sold unrolled and in any number; gear belongs on the rotating shelf.");
                }
                else if (item.SellValue <= 0)
                {
                    report.Error("npc-stock", at, $"'{npc.Id}' sells '{staple}', which has no sell_value to price it by.",
                        "Give the item a sell_value.");
                }
            }

            if (stock.MaxRarity > Rarity.Fine)
            {
                report.Error("npc-stock", at, $"'{npc.Id}' can stock {stock.MaxRarity} gear.",
                    "Merchants carry common and fine at most: the best things in the game are found, not bought.");
            }
        }
    }

    // -- sounds (AUD-01) ------------------------------------------------------

    /// <summary>
    /// Every sound is on a real bus with room for at least one voice, lists only audio files the
    /// engine can play from under <c>game/audio/</c>, and every map's music is a looping track.
    /// </summary>
    private static void Sounds(ContentDatabase db, ValidationReport report)
    {
        string[] buses = ["effects", "ui", "music"];
        string[] kinds = [".ogg", ".wav", ".mp3"];

        foreach (var sound in db.Sounds.Values.OrderBy(s => s.Id, StringComparer.Ordinal))
        {
            if (!buses.Contains(sound.Bus))
            {
                report.Error("sound", sound.SourceFile, $"'{sound.Id}' is on bus '{sound.Bus}'.",
                    $"Use one of: {string.Join(", ", buses)}.");
            }

            if (sound.MaxVoices < 1)
            {
                report.Error("sound", sound.SourceFile, $"'{sound.Id}' allows {sound.MaxVoices} voices.",
                    "At least one, or it can never play.");
            }

            if (sound.Loop != (sound.Bus == "music"))
            {
                report.Error("sound", sound.SourceFile, $"'{sound.Id}' loops on the {sound.Bus} bus, or is music that does not loop.",
                    "Music loops and lives on the music bus; nothing else does either.");
            }

            foreach (var file in sound.Files)
            {
                if (!file.StartsWith("res://audio/", StringComparison.Ordinal)
                    || !kinds.Any(k => file.EndsWith(k, StringComparison.OrdinalIgnoreCase)))
                {
                    report.Error("sound", sound.SourceFile, $"'{sound.Id}' lists '{file}'.",
                        "Sound files live under res://audio/ and are .ogg, .wav or .mp3.");
                }
            }
        }

        foreach (var zone in db.Zones.Values.Where(z => z.Music.Length > 0))
        {
            if (!db.Sounds.TryGetValue(zone.Music, out var music) || !music.Loop)
            {
                report.Error("sound", zone.SourceFile, $"'{zone.Id}' plays '{zone.Music}', which is not a looping track.",
                    "Point music at a sound with \"loop\": true on the music bus.");
            }
        }
    }
}
