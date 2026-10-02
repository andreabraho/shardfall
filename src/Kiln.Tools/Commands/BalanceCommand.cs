using Kiln.Core.Combat;
using Kiln.Core.Foundation;
using Kiln.Core.Progression;
using Kiln.Data.Definitions;
using Kiln.Data.Loading;

namespace Kiln.Tools.Commands;

/// <summary>
/// Fights each class against act 1 on paper (CLS-07): the pack and the boss of every map and of the
/// tower, at the level the campaign brings the player there, in the gear that level can wear.
/// </summary>
/// <remarks>
/// A fight of averages, not a playthrough: every blow deals its expected damage, the player casts
/// every skill it knows as it comes ready and strikes between casts, and whatever stands in front
/// strikes back without being dodged. What it says is therefore how the classes stand
/// <em>next to one another</em> — who kills faster, who is hurt more — and not how hard the game
/// is: a player who steps out of a telegraph takes less than this, whatever the class.
/// <para>
/// The rules are the game's own (the damage pipeline, the stat block, the skills as the caster
/// resolves them); where the fight has a shape the tool cannot see — how many of a pack a cone
/// catches — the guess is one function, <see cref="Reach"/>, the same for every class.
/// </para>
/// </remarks>
public static class BalanceCommand
{
    /// <summary>How far a class may stand from the middle of the six before the check fails.</summary>
    private const double Band = 0.15;

    /// <summary>The creatures of a pack.</summary>
    private const int PackSize = 8;

    /// <summary>Packs fought one after the other, so that what a class has left after its first salvo counts.</summary>
    private const int Packs = 3;

    /// <summary>What a flask gives back, and how many the Disciple carries (HealthFlask, DifficultySettings).</summary>
    private const double FlaskHeal = 0.45;

    private const double Step = 0.05;

    /// <summary>Seconds a creature that strikes from close needs to reach one who strikes from afar.</summary>
    private const double Approach = 2.0;

    /// <summary>The upgrade level of what is worn, unless <c>--upgrade</c> says otherwise: the middle of the ladder.</summary>
    private static int _upgrade = 4;

    private sealed record Build(string Name, CharacterClass Class, string Tree, int Hands, string Shift,
        double Str, double Dex, double Int, double Vit);

    /// <summary>
    /// The six ways to play, and where each puts its three points a level: two in what it strikes
    /// with and one in vitality, the split the game makes for a new character — but the Mental
    /// Warrior, whose skills take from vitality, and the Asura, whose take from intelligence.
    /// </summary>
    private static readonly Build[] Builds =
    [
        new("Body Warrior", CharacterClass.Warrior, "body", 1, "skl_guard_stance", 2 / 3.0, 0, 0, 1 / 3.0),
        new("Mental Warrior", CharacterClass.Warrior, "mental", 2, "skl_guard_stance", 0.5, 0, 0, 0.5),
        new("Ninja", CharacterClass.Blade, "", 1, "skl_shadow_step", 0, 2 / 3.0, 0, 1 / 3.0),
        new("Archer", CharacterClass.Archer, "", 1, "skl_backstep", 0, 2 / 3.0, 0, 1 / 3.0),
        new("Asura", CharacterClass.Sura, "", 1, "skl_backlash", 0.45, 0, 0.25, 0.30),
        new("Dragonseer", CharacterClass.Shaman, "", 1, "skl_pure_seal", 0, 0, 2 / 3.0, 1 / 3.0),
    ];

    private sealed record Stage(string Name, string Pack, string Boss);

    private static readonly Stage[] Stages =
    [
        new("Hollow", "mob_grey_wolf", "mob_greymane"),
        new("Approach", "mob_ninja_cutthroat", "mob_bandit_chief"),
        new("Vale Floor", "mob_orc_grunt", "mob_orc_warlord"),
        new("Ridge", "mob_sand_spider", "mob_spider_queen"),
        new("Gate", "mob_frostbone_warrior", "mob_frost_lich"),
        new("Tower I", "mob_zombie_ghoul", "mob_rotting_colossus"),
        new("Tower II", "mob_demon_warrior", "mob_blue_demon_sorcerer"),
        new("Tower III", "mob_demon_warrior", "mob_demon_lord"),
    ];

    public static int Run(string[] args)
    {
        var loaded = ContentLoader.LoadFromDirectory(Program.ResolveDataRoot(args));

        if (loaded.Errors.Count > 0)
        {
            foreach (var error in loaded.Errors) Console.Error.WriteLine($"  {error}");
            return 1;
        }

        var db = loaded.Database;
        var verbose = Program.HasFlag(args, "--verbose");

        if (Array.IndexOf(args, "--upgrade") is >= 0 and var at && at + 1 < args.Length && int.TryParse(args[at + 1], out var plus)) _upgrade = plus;

        if (Program.OptionValue(args, "--save") is { } path) return Reference(db, path, double.TryParse(Program.OptionValue(args, "--family"), System.Globalization.CultureInfo.InvariantCulture, out var share) ? share : 0.5, Program.OptionValue(args, "--only"), args);

        Console.WriteLine($"Gear at +{_upgrade}, no bonus lines, no stones, no jewellery.");
        var drift = new List<string>();
        var power = Builds.ToDictionary(b => b.Name, _ => new List<double>());
        var grit = Builds.ToDictionary(b => b.Name, _ => new List<double>());
        var packs = Builds.ToDictionary(b => b.Name, _ => new List<double>());
        var bosses = Builds.ToDictionary(b => b.Name, _ => new List<double>());

        foreach (var stage in Stages)
        {
            var boss = db.Enemies[stage.Boss];
            var mob = db.Enemies[stage.Pack];
            var level = boss.Level;

            Console.WriteLine();
            Console.WriteLine($"{stage.Name} — level {level}: {Packs} × {PackSize} {mob.Id} ({mob.Stats.Hp:0} hp), then {boss.Id} ({boss.Stats.Hp:0} hp)");
            Console.WriteLine($"  {"",-15} {"atk",5} {"hp",5} {"def",4} {"blow/s",7} | {"pack s",7} {"hurt",5} | {"boss s",7} {"dps",6} {"hurt",5} {"flasks",6}");

            var rows = new List<(Build Build, Fight Pack, Fight Boss)>();

            foreach (var build in Builds)
            {
                var hero = Hero.Make(db, build, level);
                var pack = new Fight(db, hero, mob, PackSize, Packs).Run();
                var duel = new Fight(db, hero, boss, 1, 1).Run();

                rows.Add((build, pack, duel));

                Console.WriteLine($"  {build.Name,-15} {hero.Stats.AttackPower,5:0} {hero.Stats.MaxHp,5:0} {hero.Stats.Defense,4:0} {hero.BlowPerSecond(boss),7:0} | "
                    + $"{pack.Time,7:0.0} {pack.Hurt,5:0.00} | {duel.Time,7:0.0} {boss.Stats.Hp / duel.Time,6:0} {duel.Hurt,5:0.00} {Math.Max(0, (duel.Hurt - 1) / FlaskHeal),6:0.0}");

                if (verbose) Console.WriteLine($"      {string.Join(", ", hero.Skills.Select(s => $"{s.Def.Id[4..]}:{s.Points}"))}");
            }

            // How fast it kills: the pack and the boss, each against the middle of the six.
            var packMid = Middle(rows.Select(r => r.Pack.Time));
            var bossMid = Middle(rows.Select(r => r.Boss.Time));
            var packHurt = Middle(rows.Select(r => r.Pack.Hurt));
            var bossHurt = Middle(rows.Select(r => r.Boss.Hurt));

            foreach (var (build, pack, duel) in rows)
            {
                packs[build.Name].Add(packMid / pack.Time);
                bosses[build.Name].Add(bossMid / duel.Time);
                power[build.Name].Add(Math.Sqrt(packMid / pack.Time * (bossMid / duel.Time)));
                grit[build.Name].Add(Math.Sqrt(Math.Max(Floor, pack.Hurt) / packHurt * (Math.Max(Floor, duel.Hurt) / bossHurt)));
            }
        }

        Console.WriteLine();
        Console.WriteLine("Against the middle of the six, over the whole act (1.00 is the middle):");
        Console.WriteLine($"  {"",-15} {"kills",6} {"packs",6} {"bosses",6} {"is hurt",8}   by stage: kills");

        foreach (var build in Builds)
        {
            var kills = Middle(power[build.Name]);
            var hurt = Middle(grit[build.Name]);

            Console.WriteLine($"  {build.Name,-15} {kills,6:0.00} {Middle(packs[build.Name]),6:0.00} {Middle(bosses[build.Name]),6:0.00} {hurt,8:0.00}   {string.Join(" ", power[build.Name].Select(p => $"{p:0.00}"))}");

            if (Math.Abs(kills - 1) > Band) drift.Add($"{build.Name} kills at {kills:0.00} of the middle (allowed {1 - Band:0.00}–{1 + Band:0.00})");
            if (hurt > 1 + (Band * 2)) drift.Add($"{build.Name} is hurt {hurt:0.00} times the middle (allowed {1 + (Band * 2):0.00})");
        }

        Console.WriteLine();

        if (drift.Count == 0)
        {
            Console.WriteLine($"Balance holds: every class within {Band:P0} of the middle.");
            return 0;
        }

        foreach (var line in drift) Console.Error.WriteLine($"  drift: {line}");

        return 1;
    }

    /// <summary>
    /// A saved character against every stage (A2-05): its points, its skills and what it wears, bonus lines and
    /// stones and all. Its best bonus against a family is counted against every creature, by <paramref name="share"/> —
    /// a player who has re-stoned for the map, more or less.
    /// </summary>
    private static int Reference(ContentDatabase db, string path, double share, string? only, string[] args)
    {
        var read = Kiln.Core.Saving.SaveCodec.Decode(File.ReadAllText(path));

        if (!read.Ok)
        {
            Console.Error.WriteLine($"  {path}: {read.Message}");
            return 1;
        }

        var hero = Hero.From(db, read.Save!, share, int.TryParse(Program.OptionValue(args, "--levels"), out var more) ? more : 0);
        var stats = hero.Stats;

        Console.WriteLine($"{read.Save!.CharacterName}, level {stats.Level}: attack {stats.AttackPower:0}, health {stats.MaxHp:0}, defence {stats.Defense:0}, "
            + $"{stats.AttacksPerSecond:0.00} blows/s, crit {stats.CritChance:P0}, +{stats.Modifiers.DamagePct:P0} blows, +{stats.Modifiers.SkillDamagePct:P0} skills, "
            + $"+{stats.Modifiers.VsFamilyBonus(MonsterFamily.Animal):P0} against the family ({share:P0} of its best).");
        Console.WriteLine($"  {string.Join(", ", hero.Skills.Select(s => $"{s.Def.Id[4..]}:{s.Points} {s.Skill.Rank}"))}");
        Console.WriteLine();
        Console.WriteLine($"  {"",-28} {"lvl",3} {"hp",7} {"one s",6} {"pack s",7} {"hurt",5}");

        foreach (var enemy in db.Enemies.Values.Where(e => only is null || e.Id.Contains(only, StringComparison.Ordinal)).OrderBy(e => e.Level).ThenBy(e => e.Stats.Hp))
        {
            var boss = enemy.Boss || enemy.BossBar;
            var one = new Fight(db, hero, enemy, 1, 1).Run();
            var pack = boss ? one : new Fight(db, hero, enemy, PackSize, Packs).Run();

            Console.WriteLine($"  {enemy.Id,-28} {enemy.Level,3} {enemy.Stats.Hp,7:0} {one.Time,6:0.0} {(boss ? "boss" : $"{pack.Time:0.0}"),7} {pack.Hurt,5:0.00}");
        }

        return 0;
    }

    /// <summary>The least hurt that counts: below a twentieth of a health bar the difference is noise.</summary>
    private const double Floor = 0.05;

    /// <summary>The middle of a set of ratios: their geometric mean, so that twice as fast and half as fast weigh the same.</summary>
    private static double Middle(IEnumerable<double> values)
    {
        var all = values.Select(v => Math.Max(Floor, v)).ToArray();

        return Math.Exp(all.Sum(Math.Log) / all.Length);
    }

    /// <summary>
    /// How many of a pack a blow catches — the one guess of the tool, the same for every class. A pack
    /// stands round the player at a blade's length: a turn all round catches it whole, a cone a share
    /// by how wide it opens, a line the few that stand in a row.
    /// </summary>
    private static double Reach(ResolvedSkill skill, SkillDef def, int alive)
    {
        double caught = skill.Targeting switch
        {
            SkillTargeting.SelfAoe => skill.Radius >= 3 ? alive : alive - 1,
            SkillTargeting.GroundAoe => skill.Radius >= 4 ? alive : alive - 1,
            SkillTargeting.Cone => skill.ConeAngle >= 140 ? 4 : skill.ConeAngle >= 105 ? 3 : 2.5,
            SkillTargeting.Line => skill.Width >= 2.4 ? 3 : 2,
            SkillTargeting.SingleTarget when def.Bounces > 0 => 1 + def.Bounces,
            SkillTargeting.SingleTarget when skill.Range > 0 && skill.Radius > 0 => 1 + skill.Radius,
            _ => 1,
        };

        if (skill.MaxTargets > 0) caught = Math.Min(caught, skill.MaxTargets);

        return Math.Clamp(caught, 1, alive);
    }

    /// <summary>What the ordinary blow catches: a cone of 120° at arm's length, or one arrow.</summary>
    private static double BlowReach(CharacterClass cls, int alive) => cls == CharacterClass.Archer ? 1 : Math.Min(2.5, alive);

    private sealed record Known(SkillDef Def, ResolvedSkill Skill, int Points);

    /// <summary>A character of a build at a level: its points placed, its gear worn, its skills learned.</summary>
    private sealed class Hero
    {
        public required Build Build { get; init; }
        public required StatBlock Stats { get; init; }
        public required List<Known> Skills { get; init; }
        public required Known? Shift { get; init; }

        public static Hero Make(ContentDatabase db, Build build, int level)
        {
            // Three points a level, and the three a new character starts with.
            var points = Attributes.PointsPerLevel * level;
            var str = (int)Math.Round(points * build.Str);
            var dex = (int)Math.Round(points * build.Dex);
            var mind = (int)Math.Round(points * build.Int);
            var vit = points - str - dex - mind;

            var stats = new StatBlock
            {
                Level = level,
                Class = build.Class,
                Attributes = Attributes.Starting + new Attributes(str, dex, mind, vit),
            };

            var cls = build.Class.ToString().ToLowerInvariant();

            ItemDef? Best(EquipSlot slot, string stat) => db.Items.Values
                .Where(i => i.Slot == slot && i.LevelReq <= level
                    && (i.ClassRestriction.Length == 0 || i.ClassRestriction.Contains(cls))
                    && (slot != EquipSlot.Weapon || build.Class != CharacterClass.Warrior || i.Hands == build.Hands))
                .OrderByDescending(i => i.BaseStats.GetValueOrDefault(stat))
                .FirstOrDefault();

            if (Best(EquipSlot.Weapon, "weapon_damage_max") is { } weapon)
            {
                stats.WeaponDamage = (weapon.BaseStats["weapon_damage_min"] + weapon.BaseStats["weapon_damage_max"]) / 2 * Kiln.Core.Items.UpgradeScaling.Multiplier(_upgrade);
                stats.Modifiers.AttackSpeedFlat += weapon.BaseStats.GetValueOrDefault("attack_speed");
            }

            foreach (var slot in (EquipSlot[])[EquipSlot.Armor, EquipSlot.Helmet, EquipSlot.Shield, EquipSlot.Boots])
            {
                stats.ArmorValue += (Best(slot, "armor_value")?.BaseStats.GetValueOrDefault("armor_value") ?? 0) * Kiln.Core.Items.UpgradeScaling.Multiplier(_upgrade);
            }

            stats.SkillAttackBonus = build.Class == CharacterClass.Warrior && build.Tree == "mental" ? stats.Attributes.Vit * Attributes.MentalSkillPerVit
                : build.Class == CharacterClass.Sura ? stats.Attributes.Int * Attributes.AsuraSkillPerInt : 0;

            // A point a level: a skill learned with each of the first six, in the order the book lists them —
            // a new skill is worth more than a sixth more of an old one — and the rest poured in, seven to each. A skill at seven is taken as Perfect — the
            // fifty-five casts that takes are a quarter of an hour of play.
            var mine = db.Skills.Values
                .Where(s => s.Class == build.Class && s.Id != build.Shift && (build.Tree.Length == 0 || s.Tree == build.Tree))
                .OrderBy(s => s.SuggestedLevel)
                .Take(Math.Min(level, SkillBook.MaxSkills))
                .ToList();

            var spent = mine.ToDictionary(s => s.Id, _ => 1);
            var left = level - mine.Count;

            // What strikes is filled before what is raised: points make a blow harder and a blessing only longer.
            foreach (var skill in mine.OrderByDescending(s => s.DamageCoef * Math.Max(1, s.Hits) * Reach(ResolvedSkill.For(s, MasteryRank.Normal), s, 3) / Math.Max(1, s.Cooldown)))
            {
                var more = Math.Clamp(left, 0, SkillBook.MaxPoints - 1);

                spent[skill.Id] += more;
                left -= more;
            }

            Known Learn(SkillDef def, int at) =>
                new(def, ResolvedSkill.For(def, at >= SkillBook.MaxPoints ? MasteryRank.Perfect : MasteryRank.Normal, at), at);

            return new Hero
            {
                Build = build,
                Stats = stats,
                Skills = mine.Select(s => Learn(s, spent[s.Id])).ToList(),
                Shift = db.Skills.TryGetValue(build.Shift, out var shift) ? Learn(shift, 1) : null,
            };
        }

        /// <summary>The character of a save, as the game would build it.</summary>
        public static Hero From(ContentDatabase db, Kiln.Core.Saving.SaveGame save, double share, int levels = 0)
        {
            var catalogue = new Kiln.Data.Items.ItemCatalogue(db);
            var gear = new Kiln.Core.Items.Equipment(catalogue);
            var cls = Enum.Parse<CharacterClass>(save.Class, ignoreCase: true);
            var worn = new Dictionary<EquipSlot, Kiln.Core.Items.ItemInstance>();

            foreach (var (slot, uid) in save.Player.Worn)
            {
                if (save.Player.Items.FirstOrDefault(i => i.Uid == uid) is { } saved
                    && Kiln.Core.Saving.ItemSnapshots.Restore(saved, catalogue, catalogue) is { } item)
                {
                    worn[Enum.Parse<EquipSlot>(slot)] = item;
                }
            }

            gear.Load(worn);

            var a = save.Player.Assigned;
            var stats = new StatBlock
            {
                // Levels still to come (--levels) go where the rest went: into what it strikes with.
                Level = save.Player.Level + levels,
                Class = cls,
                Attributes = Attributes.Starting + new Attributes(a[0], a[1], a[2], a[3]) + PrimaryPoints(cls, levels * Attributes.PointsPerLevel),
            };

            gear.ApplyTo(stats);
            stats.Attributes = stats.Modifiers.Raise(stats.Attributes);

            // Its best bonus against a family, by the share asked, against every family.
            var best = stats.Modifiers.VsFamily.Count > 0 ? stats.Modifiers.VsFamily.Values.Max() : 0;

            foreach (var family in Enum.GetValues<MonsterFamily>()) stats.Modifiers.VsFamily[family] = best * share;

            var mental = save.Path == "mental";

            stats.SkillAttackBonus = cls == CharacterClass.Warrior && mental ? stats.Attributes.Vit * Attributes.MentalSkillPerVit
                : cls == CharacterClass.Sura ? stats.Attributes.Int * Attributes.AsuraSkillPerInt : 0;

            var shift = cls switch
            {
                CharacterClass.Blade => "skl_shadow_step",
                CharacterClass.Archer => "skl_backstep",
                CharacterClass.Sura => "skl_backlash",
                CharacterClass.Shaman => "skl_pure_seal",
                _ => "skl_guard_stance",
            };

            Known Learn(SkillDef def, int points)
            {
                var uses = save.Player.Skills.GetValueOrDefault(def.Id);
                var rank = points < SkillBook.MaxPoints ? MasteryRank.Normal
                    : uses >= SkillBook.PerfectUses ? MasteryRank.Perfect
                    : uses >= SkillBook.GrandMasterUses ? MasteryRank.GrandMaster : MasteryRank.Master;

                return new Known(def, ResolvedSkill.For(def, rank, points), points);
            }

            return new Hero
            {
                Build = new Build(save.CharacterName, cls, save.Path, 1, shift, 0, 0, 0, 0),
                Stats = stats,
                Skills = save.Player.SkillPointsSpent
                    .Where(p => p.Key != shift && db.Skills.TryGetValue(p.Key, out var def) && def.Class == cls)
                    .Select(p => Learn(db.Skills[p.Key], p.Value))
                    .ToList(),
                Shift = db.Skills.TryGetValue(shift, out var guard) ? Learn(guard, 1) : null,
            };
        }

        private static Attributes PrimaryPoints(CharacterClass cls, int points) => cls switch
        {
            CharacterClass.Blade or CharacterClass.Archer => new Attributes(0, points, 0, 0),
            CharacterClass.Shaman => new Attributes(0, 0, points, 0),
            _ => new Attributes(points, 0, 0, 0),
        };

        /// <summary>The ordinary blow alone, against this creature, a second.</summary>
        public double BlowPerSecond(EnemyDef enemy) =>
            DamagePipeline.ExpectedDamage(new DamageRequest { Attacker = Stats, Defender = Foe(enemy) }) * Stats.AttacksPerSecond;
    }

    private static StatBlock Foe(EnemyDef def) => new()
    {
        Level = def.Level,
        Family = def.Family,
        FlatMaxHp = def.Stats.Hp,
        FlatAttackPower = def.Stats.AttackPower,
        FlatDefense = def.Stats.Defense,
    };

    private sealed class Creature
    {
        public double Hp;
        public double Vulnerable, VulnerableFor;
        public double Weak, WeakFor;
        public double StunnedFor;
        public double Poison, PoisonFor, Burn, BurnFor;
        public int PoisonStacks, BurnStacks;
    }

    /// <summary>One fight, to the death of everything in front.</summary>
    private sealed class Fight
    {
        private readonly ContentDatabase _db;
        private readonly Hero _hero;
        private readonly EnemyDef _def;
        private readonly StatBlock _foe;
        private readonly List<Creature> _alive = [];
        private readonly Dictionary<string, double> _cooldown = [];
        private readonly Dictionary<string, (double Magnitude, double Left)> _on = [];

        private double _mana, _busy, _swing, _shiftIn, _taken, _since;
        private int _waves;
        private readonly int _count;

        /// <summary>Seconds to the last death.</summary>
        public double Time { get; private set; }

        /// <summary>What the player took, in health bars of its own.</summary>
        public double Hurt { get; private set; }

        public Fight(ContentDatabase db, Hero hero, EnemyDef def, int count, int waves)
        {
            _db = db;
            _hero = hero;
            _def = def;
            _foe = Foe(def);
            _mana = hero.Stats.MaxMana;

            _count = count;
            _waves = waves;

            Arrive();
        }

        private void Arrive()
        {
            _waves--;
            _since = 0;

            for (var i = 0; i < _count; i++) _alive.Add(new Creature { Hp = _def.Stats.Hp });
        }

        private double Has(string kind) => _on.TryGetValue(kind, out var e) && e.Left > 0 ? e.Magnitude : 0;

        public Fight Run()
        {
            while ((_alive.Count > 0 || _busy > 0 || _waves > 0) && Time < 3600)
            {
                Act();
                Suffer();
                Tick();
                Time += Step;
                _since += Step;

                if (_alive.Count == 0 && _busy <= 0 && _waves > 0) Arrive();
            }

            Hurt = _taken / _hero.Stats.MaxHp;

            return this;
        }

        // ---------------------------------------------------------------- the player

        private void Act()
        {
            if (_busy > 0) return;

            var stats = _hero.Stats;

            // What it raises on itself first, then the hardest blow that is ready.
            var ready = _hero.Skills
                .Where(k => _cooldown.GetValueOrDefault(k.Def.Id) <= 0 && _mana >= k.Skill.ManaCost)
                .OrderByDescending(k => k.Skill.DamageCoef <= 0 && k.Def.Guards <= 0 ? double.MaxValue : Worth(k))
                .FirstOrDefault();

            if (ready is not null)
            {
                Cast(ready);
                return;
            }

            if (_swing > 0) return;

            _swing = 1 / (stats.AttacksPerSecond * (1 + Has("frenzy")));

            var veil = Has("stealth");

            _on.Remove("stealth");

            var caught = BlowReach(stats.Class, _alive.Count);
            var each = Blow(skill: false, coef: 1 + veil, pierces: false);

            Strike(caught, each, skill: false);
        }

        private double Worth(Known known) =>
            known.Skill.DamageCoef * Math.Max(1, known.Skill.Hits) * Reach(known.Skill, known.Def, _alive.Count);

        private static bool Charged(ResolvedSkill s) => s.Windup > 0 && s.Motion is "charge" or "wave" or "breath";

        private double Hold(ResolvedSkill s) => s.Targeting == SkillTargeting.Self
            ? (_hero.Stats.Class == CharacterClass.Shaman ? 1.2 : 0.6)
            : s.Windup + ((Math.Max(1, s.Hits) - 1) * s.HitInterval) + (s.Windup > 0 && !Charged(s) ? 0.5 : 0.35);

        private double Blow(bool skill, double coef, bool pierces) =>
            DamagePipeline.ExpectedDamage(new DamageRequest
            {
                Attacker = _hero.Stats,
                Defender = _foe,
                IsSkill = skill,
                SkillCoef = skill ? coef : 1,
                WeaponCoef = skill ? 1 : coef,
                BonusAttackPower = Has("might"),
                BonusCritChance = Has("focus"),
            }) / (pierces ? 1 - (_foe.DamageReductionAgainst(_hero.Stats.Level) * (1 - _hero.Stats.PierceChance)) : 1)
            * (skill ? 1 : 1 + Has("empower"));

        /// <summary>Lands a blow on the first of the pack, whole on as many as it catches and by its share on the next.</summary>
        private List<(Creature Who, double Dealt)> Strike(double caught, double each, bool skill)
        {
            var landed = new List<(Creature, double)>();

            for (var i = 0; i < _alive.Count && caught > 0; i++, caught--)
            {
                var who = _alive[i];
                var dealt = each * Math.Min(1, caught) * (1 + (who.VulnerableFor > 0 ? who.Vulnerable : 0));

                who.Hp -= dealt;
                landed.Add((who, dealt));

                // The Hexed Blade drinks from the ordinary blow.
                if (!skill) _taken -= dealt * Has("leech");
            }

            return landed;
        }

        private void Cast(Known known)
        {
            var (def, skill) = (known.Def, known.Skill);
            var stats = _hero.Stats;
            var mind = stats.Attributes.Int;
            var asura = stats.Class == CharacterClass.Sura;

            _mana -= skill.ManaCost;
            _cooldown[def.Id] = skill.Cooldown;
            _busy = Hold(skill);

            if (def.Guards > 0) _on["dread"] = (def.Guards + (mind * Attributes.AsuraGuardPerInt), skill.Duration);

            if (skill.Targeting == SkillTargeting.Self)
            {
                var kind = skill.Applies?.Kind ?? "";
                var magnitude = skill.Magnitude;

                if (stats.Class == CharacterClass.Shaman && kind == "fortify") magnitude = Attributes.SeerScaled(magnitude, mind, Attributes.SeerBlessingPerInt, Attributes.SeerBlessingCap);
                if (stats.Class == CharacterClass.Shaman && kind == "focus") magnitude = Attributes.SeerScaled(magnitude, mind, Attributes.SeerEyePerInt, Attributes.SeerEyeCap);

                switch (kind)
                {
                    case "hex":
                        _on["might"] = (magnitude + (mind * Attributes.AsuraBladePerInt), skill.Duration);
                        _on["leech"] = (skill.Applies!.Magnitude, skill.Duration);
                        break;

                    case "mail":
                        _on["fortify"] = (asura ? Attributes.SeerScaled(magnitude, mind, Attributes.AsuraSharePerInt, Attributes.AsuraMailCap) : magnitude, skill.Duration);
                        _on["thorns"] = (asura ? Attributes.SeerScaled(skill.Applies!.Magnitude, mind, Attributes.AsuraThornsPerInt, Attributes.AsuraThornsCap) : skill.Applies!.Magnitude, skill.Duration);
                        break;

                    default:
                        _on[kind] = (magnitude, skill.Duration);
                        break;
                }

                return;
            }

            // Out of the veil: the first blow is worth more.
            var ambush = 1.0;

            if (Has("stealth") > 0)
            {
                ambush = 1 + Math.Max(Has("stealth"), skill.Applies is null && skill.Targeting == SkillTargeting.SingleTarget ? skill.Magnitude : 0);
                _on.Remove("stealth");
            }

            var caught = Reach(skill, def, _alive.Count);
            var hits = Math.Max(1, skill.Hits);

            for (var hit = 0; hit < hits; hit++)
            {
                var each = skill.DamageCoef > 0 ? Blow(skill: true, coef: skill.DamageCoef * (hit == 0 ? ambush : 1), pierces: skill.Pierces) : 0;

                foreach (var (who, dealt) in Strike(caught, each, skill: true))
                {
                    if (skill.Applies is not { } mark) continue;

                    switch (mark.Kind)
                    {
                        case "poison" when skill.Magnitude > 0:
                            who.PoisonStacks = Math.Min(who.PoisonStacks + 1, 5);
                            who.Poison = Math.Max(who.Poison, dealt * skill.Magnitude);
                            who.PoisonFor = Math.Max(who.PoisonFor, mark.Duration);
                            break;

                        case "burn" when skill.Magnitude > 0:
                            who.BurnStacks = Math.Min(who.BurnStacks + 1, 3);
                            who.Burn = Math.Max(who.Burn, dealt * skill.Magnitude);
                            who.BurnFor = Math.Max(who.BurnFor, mark.Duration);
                            break;

                        case "vulnerability":
                            who.Vulnerable = asura ? Attributes.SeerScaled(mark.Magnitude, mind, Attributes.AsuraSharePerInt, Attributes.AsuraBreakCap) : mark.Magnitude;
                            who.VulnerableFor = mark.Duration;
                            break;

                        case "weaken":
                            who.Weak = asura ? Attributes.SeerScaled(mark.Magnitude, mind, Attributes.AsuraSharePerInt, Attributes.AsuraDreadCap) : mark.Magnitude;
                            who.WeakFor = mark.Duration;
                            break;

                        // A boss shrugs a stun off by its resistance; what is left is time it does not strike.
                        case "stun":
                            who.StunnedFor = Math.Max(who.StunnedFor, mark.Duration * mark.Chance * (1 - _def.StunResist));
                            break;
                    }
                }
            }
        }

        // ---------------------------------------------------------------- what stands in front

        /// <summary>
        /// What one creature deals a second to a player who stands and takes it: every ability as
        /// often as it comes round, one announced on the ground at half — it is there to be left.
        /// </summary>
        private double Threat()
        {
            var blows = _def.Abilities.Where(a => a.DamageCoef > 0).ToArray();

            // One thing at a time: the abilities share the creature's seconds.
            var busy = blows.Sum(a => a.Windup / (a.Cooldown + a.Windup));
            var crowd = busy > 1 ? 1 / busy : 1;

            return blows.Sum(a => a.DamageCoef * (a.Telegraph is null ? 1 : 0.5) / (a.Cooldown + a.Windup)) * crowd * _def.Stats.AttackPower;
        }

        private void Suffer()
        {
            var stats = _hero.Stats;
            var mitigation = stats.DamageReductionAgainst(_def.Level, Has("dread"));
            var perSecond = 0.0;

            foreach (var who in _alive)
            {
                if (who.StunnedFor > 0) continue;

                perSecond += Threat() * (who.WeakFor > 0 ? 1 - who.Weak : 1);
            }

            perSecond *= (1 - mitigation) * (1 - stats.Evasion)
                * (Has("fortify") > 0 ? 1 - Has("fortify") : 1)
                * (Has("frenzy") > 0 ? 1 + StatusEffectSet.FrenzyExposure : 1)
                * (Has("haste") > 0 ? 1 - StatusEffectSet.HasteEvasion : 1)
                * (Has("stealth") > 0 ? 0 : 1);

            var incoming = perSecond * Step;

            // The bow strikes before anything that has to walk up to it has arrived.
            if (stats.Class == CharacterClass.Archer && _since < Approach && _def.Abilities.All(a => a.Range <= 0)) incoming = 0;

            // On Shift, as it comes ready and there is something to answer.
            if (_hero.Shift is { } shift && _shiftIn <= 0 && incoming > 0 && _mana >= shift.Skill.ManaCost)
            {
                _mana -= shift.Skill.ManaCost;
                _shiftIn = shift.Skill.Cooldown;

                var seconds = shift.Skill.Duration;

                switch (shift.Def.Id)
                {
                    // The Guard: a share of what comes while it is held.
                    case "skl_guard_stance":
                        _on["guard"] = (shift.Skill.Magnitude, seconds);
                        break;

                    // A shell worth a share of the health, for as long as it holds.
                    case "skl_pure_seal":
                        _on["shell"] = (stats.MaxHp * Attributes.SeerScaled(shift.Skill.Magnitude, stats.Attributes.Int, Attributes.SeerSealPerInt, Attributes.SeerSealCap), seconds);
                        break;

                    // The Backlash: nothing lands while it waits, and what struck is answered.
                    case "skl_backlash":
                        _on["guard"] = (1.0 / _alive.Count, seconds);
                        Strike(Math.Min(_alive.Count, 4), Blow(skill: true, coef: shift.Skill.DamageCoef, pierces: false), skill: true);
                        break;

                    // A step aside: nothing lands for its moment.
                    default:
                        _on["guard"] = (1, seconds);
                        break;
                }
            }

            incoming *= 1 - Has("guard");

            if (Has("shell") is > 0 and var shell)
            {
                var held = Math.Min(shell, incoming);

                incoming -= held;
                _on["shell"] = (shell - held, _on["shell"].Left);
            }

            _taken += incoming;

            // The Arcane Mail throws a share back at what struck.
            if (Has("thorns") is > 0 and var thorns && _alive.Count > 0)
            {
                foreach (var who in _alive) who.Hp -= incoming * thorns / _alive.Count;
            }
        }

        private void Tick()
        {
            var stats = _hero.Stats;

            _busy -= Step;
            _swing -= Step;
            _shiftIn -= Step;
            _mana = Math.Min(stats.MaxMana, _mana + (stats.ManaRegenInCombat * Step));

            foreach (var id in _cooldown.Keys.ToArray()) _cooldown[id] -= Step;
            foreach (var kind in _on.Keys.ToArray()) _on[kind] = (_on[kind].Magnitude, _on[kind].Left - Step);

            foreach (var who in _alive)
            {
                who.VulnerableFor -= Step;
                who.WeakFor -= Step;
                who.StunnedFor -= Step;

                if (who.PoisonFor > 0) who.Hp -= who.Poison * who.PoisonStacks * Step;
                else who.PoisonStacks = 0;

                if (who.BurnFor > 0) who.Hp -= who.Burn * who.BurnStacks * Step;
                else who.BurnStacks = 0;

                who.PoisonFor -= Step;
                who.BurnFor -= Step;
            }

            var before = _alive.Count;

            _alive.RemoveAll(c => c.Hp <= 0);

            // A kill gives mana back.
            _mana = Math.Min(stats.MaxMana, _mana + ((before - _alive.Count) * stats.MaxMana * PlayerConstants.ManaPerKillFraction));
        }
    }
}
