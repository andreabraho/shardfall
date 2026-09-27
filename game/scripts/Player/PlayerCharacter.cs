using System.Linq;
using Godot;
using Kiln.Core.Combat;
using Kiln.Core.Foundation;
using Kiln.Core.Progression;

namespace Kiln.Game.Player;

/// <summary>
/// Sets the player up and handles death and respawn (FR-3.9).
/// <para>
/// Stats are hard-coded for the Phase 2 slice. Levelling and gear replace this in Phases 3
/// and 4 — the numbers here exist so combat can be felt, not to be balanced.
/// </para>
/// </summary>
public partial class PlayerCharacter : Node
{
    private PlayerMotor _motor = null!;
    private Combat.Combatant _combatant = null!;
    private Vector3 _spawnPoint;


    /// <summary>
    /// The level the character begins at. One, because the game begins in the first village
    /// and its band is 1–3.
    /// </summary>
    /// <remarks>
    /// This was 10 for as long as the only playable scene was the test arena, whose band is
    /// 11–15. Once the village became the entry point that default made every creature
    /// outside it trivial, and FR-2.7 had them ignore the player entirely — which reads,
    /// from the outside, exactly like broken aggro. Test scenes above the starting band
    /// override it on their own Player node.
    /// </remarks>
    [Export] public int StartingLevel { get; set; } = 1;

    /// <summary>Respawn delay. Kept short: a long death screen makes a hard game feel unfair.</summary>
    [Export] public double RespawnSeconds { get; set; } = 1.5;

    /// <summary>Level, experience and unspent points (PRG-01/02). Owned by the session.</summary>
    /// <remarks>
    /// Taken from <see cref="PlayerProfile"/> rather than constructed, because walking through
    /// a zone gate replaces the scene tree and this node with it. A character that belonged to
    /// the node would be a new character on the far side of every gate.
    /// </remarks>
    public CharacterProgression Progression => PlayerProfile.Progression;

    /// <summary>Which skills are learned, and their mastery (PRG-03/05).</summary>
    public SkillBook Skills => PlayerProfile.Skills;

    [Signal] public delegate void LeveledUpEventHandler(int level);

    [Signal] public delegate void ExperienceChangedEventHandler();

    public override void _Ready()
    {
        _motor = GetParent<PlayerMotor>();
        _combatant = GetParent().GetNode<Combat.Combatant>("Combatant");

        _spawnPoint = _motor.GlobalPosition;
        _motor.AddToGroup("player");

        // Only for a character that does not exist yet. Arriving through a gate must not
        // re-grant the starting level, or every border would be a reset.
        var isNewCharacter = !PlayerProfile.Exists;

        if (isNewCharacter)
        {
            Progression.Grant(ExperienceTable.CumulativeTo(StartingLevel));
            SpendStartingPoints();
            PlayerProfile.MarkCreated();
        }

        _combatant.IsPlayer = true;
        ApplyStats();

        if (!isNewCharacter) RestoreCarriedHealth();

        _combatant.Damaged += OnDamaged;
        _combatant.Died += OnDied;

        // Skills the starting level already allows.
        CallDeferred(nameof(LearnSkills));
    }

    /// <summary>Puts back the condition the character walked through the gate in.</summary>
    private void RestoreCarriedHealth()
    {
        _combatant.Health.SetCurrent(_combatant.Health.Max * PlayerProfile.HealthFraction);
        _combatant.Mana.SetCurrent(_combatant.Mana.Max * PlayerProfile.ManaFraction);
    }

    /// <summary>
    /// Records the character's condition just before the scene is replaced.
    /// </summary>
    /// <remarks>
    /// Called by the gate rather than from <c>_ExitTree</c>: a scene change tears everything
    /// down, and reading a half-dismantled node's health is how a border quietly becomes a
    /// free heal.
    /// </remarks>
    public void CarryOut()
    {
        PlayerProfile.HealthFraction = _combatant.Health.Fraction;
        PlayerProfile.ManaFraction = _combatant.Mana.Fraction;
    }

    private void OnDamaged(int amount, bool critical, bool evaded)
    {
        var at = _motor.GlobalPosition + (Vector3.Up * 2.0f);
        Combat.CombatFeedback.Number(at, amount, critical, evaded, onPlayer: true);

        if (evaded) return;

        Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndPlayerHurt);

        // A flash and nothing more (REF-01): no frame freeze, no camera shake on hits.
        _motor.GetNodeOrNull<Visual.VisualRoot>("VisualRoot")?.Flash();
    }

    /// <summary>
    /// Spends the level-up points for the test character. A real character sheet lets the
    /// player choose; until then a Warrior-shaped split keeps the numbers sensible.
    /// </summary>
    private void SpendStartingPoints()
    {
        // The three a new character starts with are the player's own to spend: the training's
        // first lesson (2026-09-27).
        while (Progression.UnspentAttributePoints > CharacterProgression.StartingAttributePoints)
        {
            var remaining = Progression.UnspentAttributePoints;

            Progression.SpendAttributePoint(AttributeKind.Str);
            if (remaining % 2 == 0) Progression.SpendAttributePoint(AttributeKind.Vit);
        }
    }

    /// <summary>Rebuilds the stat block from current attributes and gear. Called on every level.</summary>
    private void ApplyStats()
    {
        var fraction = _combatant.Health.Max > 0 ? _combatant.Health.Fraction : 1.0;

        var stats = new StatBlock
        {
            Level = Progression.Level,
            Class = CharacterClass.Warrior,
            Family = MonsterFamily.Human,
            Attributes = Progression.TotalAttributes,

            // Fallback for a character with nothing equipped, so a bare-handed player is
            // weak rather than harmless.
            WeaponDamage = 6,
            ArmorValue = 0,
        };

        GetParent().GetNodeOrNull<Items.PlayerInventory>("PlayerInventory")?.Gear?.ApplyTo(stats);

        _combatant.Configure(stats, "$player.name");

        // Keep the same proportion of health rather than a free full heal on every level.
        _combatant.Health.SetCurrent(_combatant.Health.Max * fraction);
    }

    /// <summary>Recomputes stats after a gear change.</summary>
    public void RefreshStats() => ApplyStats();

    /// <summary>
    /// Spends one unspent attribute point (PRG-10). Returns false when there are none left.
    /// </summary>
    /// <remarks>
    /// Deliberately free-form: nothing stops the player pouring every point into one
    /// attribute. The derived stats already diminish — crit caps at 50%, evasion at 30%,
    /// mitigation at 75% — so a single-stat build runs into the ceiling on its own. A
    /// per-level cap would be a second, redundant rule that only removes the option of
    /// trying it.
    /// </remarks>
    public bool SpendAttribute(AttributeKind kind)
    {
        if (!Progression.SpendAttributePoint(kind)) return false;

        ApplyStats();
        EmitSignal(SignalName.ExperienceChanged);
        Quests.Tutorial.Did(GetTree(), Kiln.Core.Quests.TutorialStep.SpendStat);

        return true;
    }

    /// <summary>
    /// Awards experience for a kill, adjusted for the level gap (FR-2.7).
    /// <para>
    /// Killing far below your level is worth almost nothing, so clearing an easy zone is
    /// never the efficient route — the campaign is meant to carry the player, not farming.
    /// </para>
    /// </summary>
    public void AwardKill(int enemyLevel, int baseXp)
    {
        // Mana first, and independently of experience: it is the fuel for the next fight, so
        // a character at the level cap must still be paid for killing things.
        RestoreManaForKill(enemyLevel);
        _sinceKill = 0;

        if (baseXp <= 0) return;

        GrantExperience((long)System.Math.Round(
            baseXp * ExperienceTable.CatchUpMultiplier(Progression.Level, enemyLevel)));
    }

    /// <summary>
    /// Adds experience and handles any levels it crosses. Shared by kills and quests.
    /// </summary>
    /// <remarks>
    /// Quest experience is not scaled by the level gap the way a kill is: a quest is the game
    /// telling the player "this step is done", and paying less because they were strong
    /// enough to do it quickly would read as a punishment.
    /// </remarks>
    public void GrantExperience(long amount)
    {
        if (amount <= 0 || Progression.IsMaxLevel) return;

        var levels = Progression.Grant(amount);

        EmitSignal(SignalName.ExperienceChanged);

        if (levels.Count == 0) return;

        ApplyStats();
        _combatant.Health.Fill();
        _combatant.Mana.Fill();
        LearnSkills();

        foreach (var level in levels)
        {
            GD.Print($"[progression] level {level.NewLevel} — {level.AttributePoints} attribute points, {level.SkillPoints} skill points");
            EmitSignal(SignalName.LeveledUp, level.NewLevel);

            UI.ChatLog.Post(
                Kiln.Core.Foundation.L10n.F("Level {0}! +{1} attribute points, +{2} skill point.", level.NewLevel, level.AttributePoints, level.SkillPoints),
                new Color("9fe07a"));
        }

        // Level-up restores and is announced loudly: it is the reward beat of the loop.
        Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndLevelUp);
        Combat.CombatFeedback.Number(
            _motor.GlobalPosition + (Vector3.Up * 2.6f), Progression.Level, critical: true, evaded: false);
    }

    /// <summary>
    /// Pays back mana for a kill (CBT-12). Trivial enemies give nothing, matching the
    /// experience rule: farming things far below you must never be the efficient play.
    /// </summary>
    private void RestoreManaForKill(int enemyLevel)
    {
        if (ExperienceTable.IsTrivial(Progression.Level, enemyLevel)) return;

        var amount = _combatant.Stats.MaxMana * PlayerConstants.ManaPerKillFraction;

        if (_combatant.Mana.IsFull || amount <= 0) return;

        var restored = _combatant.Mana.Add(amount);

        if (restored >= 1)
        {
            Combat.CombatFeedback.Mana(_motor.GlobalPosition + (Vector3.Up * 2.2f), restored);
        }
    }

    /// <summary>
    /// Nothing is learned on a level-up any more (REF-03): the level only makes a skill
    /// eligible, and the point is spent by the player from the skill screen.
    /// </summary>
    private void LearnSkills()
    {
    }

    /// <summary>
    /// Direction away from the nearest living enemy, as a stand-in for the true hit
    /// direction. Damage does not currently carry its source; when it does (needed anyway
    /// for threat and for directional blocking) this reads it from there instead.
    /// </summary>
    private Vector3 LastHitDirection()
    {
        Node3D? nearest = null;
        var best = float.MaxValue;

        foreach (var node in GetTree().GetNodesInGroup("combatants"))
        {
            if (node is not Combat.Combatant other || other == _combatant || !other.IsAlive) continue;

            var distance = _motor.GlobalPosition.DistanceSquaredTo(other.Body.GlobalPosition);
            if (distance >= best) continue;

            best = distance;
            nearest = other.Body;
        }

        return nearest is null
            ? Vector3.Zero
            : _motor.GlobalPosition - nearest.GlobalPosition;
    }

    /// <summary>
    /// Where death sends the player: the last shrine they touched, falling back to where they
    /// started. The fallback matters — a zone the player has crossed without finding a shrine
    /// must still have an answer to dying in it.
    /// </summary>
    private double _sinceKill = double.MaxValue;
    private double _regenTick;

    /// <summary>
    /// Kill regeneration: while something died to the player in the last ten seconds, a share
    /// of maximum health comes back every three (the earrings' base stat).
    /// </summary>
    public override void _Process(double delta)
    {
        _sinceKill = _sinceKill >= double.MaxValue / 2 ? _sinceKill : _sinceKill + delta;

        var pct = _combatant.Stats.Modifiers.KillRegenPct;

        if (pct <= 0 || !_combatant.IsAlive || _sinceKill > Kiln.Core.Items.KillRegen.WindowSeconds)
        {
            _regenTick = 0;
            return;
        }

        _regenTick += delta;

        if (_regenTick < Kiln.Core.Items.KillRegen.IntervalSeconds) return;

        _regenTick -= Kiln.Core.Items.KillRegen.IntervalSeconds;

        var amount = (int)System.Math.Round(_combatant.Health.Max * pct / 100.0);

        if (amount <= 0 || _combatant.Health.IsFull) return;

        _combatant.Heal(amount);
        Combat.CombatFeedback.Heal(_motor.GlobalPosition + (Vector3.Up * 2.0f), amount);
    }

    /// <summary>
    /// Death (REF-14): the cost is paid at once, then the player chooses where to get up — at
    /// the last shrine touched (the tower's last safe floor, inside the tower) or back in the
    /// village, as in the original.
    /// </summary>
    private void OnDied()
    {
        Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndPlayerDeath);

        PayDeathCost();

        var anchor = World.GameWorld.IsLoaded ? World.GameWorld.Travel.Anchor : null;
        var tower = GetTree().GetFirstNodeInGroup("tower") as World.TowerNode;
        var village = VillageShrine();

        GD.Print($"[combat] player died — last shrine {anchor ?? "none"}{(tower is null ? "" : ", in the tower")}");
        _motor.Stop();

        var here = tower is not null ? Kiln.Core.Foundation.L10n.T("the last safe floor")
            : anchor is not null ? ShrineName(anchor)
            : Kiln.Core.Foundation.L10n.T("the map's entrance");

        // The village is not offered twice: when the last shrine is the village's, one button.
        var offerVillage = village is not null && (tower is not null || anchor != village);

        var panel = UI.DeathPanel.Open(GetTree().CurrentScene, here, offerVillage ? ShrineName(village!) : null, RespawnSeconds);
        panel.Chosen += toVillage => GetUp(toVillage ? village : null, tower);
    }

    /// <summary>The village's shrine: the first travel shrine of the hub.</summary>
    private static string? VillageShrine()
    {
        if (!World.GameWorld.IsLoaded || World.GameWorld.Graph.Hub is not { } hub) return null;

        return World.GameWorld.Graph.ShrinesIn(hub.Id).FirstOrDefault(s => s.FastTravel)?.Id;
    }

    private static string ShrineName(string shrineId) =>
        World.GameWorld.Graph.Shrine(shrineId) is { } shrine ? Items.GameItems.Localise(shrine.Name) : shrineId;

    /// <summary>
    /// Brings the player back: at <paramref name="shrineId"/> when it is given (the village),
    /// otherwise at the last shrine. A shrine on another map loads that map.
    /// </summary>
    private void GetUp(string? shrineId, World.TowerNode? tower)
    {
        if (!IsInstanceValid(this)) return;

        // Inside the tower the tower decides where the player comes back, and puts them there.
        if (shrineId is null && tower is not null && IsInstanceValid(tower))
        {
            tower.RespawnPlayer();
            Stand();
            return;
        }

        var target = shrineId ?? (World.GameWorld.IsLoaded ? World.GameWorld.Travel.Anchor : null);

        if (target is null)
        {
            _motor.GlobalPosition = _spawnPoint;
            Stand();
            return;
        }

        foreach (var node in GetTree().GetNodesInGroup("shrines"))
        {
            if (node is not World.ShrineNode shrine || shrine.ShrineId != target) continue;

            _motor.GlobalPosition = shrine.GlobalPosition + World.ShrineNode.ArrivalOffset;
            Stand();
            return;
        }

        if (World.GameWorld.Graph.Shrine(target) is not { } far)
        {
            _motor.GlobalPosition = _spawnPoint;
            Stand();
            return;
        }

        // On another map: revived first, so the health that crosses over is full.
        _combatant.Revive();
        CarryOut();
        GD.Print($"[combat] player gets up at {target}, on {far.Zone}");
        World.ZoneTransition.BeginToShrine(GetTree(), World.GameWorld.CurrentZoneId, far.Zone, target);
    }

    private void Stand()
    {
        _motor.MovementLocked = false;
        _motor.Stop();
        _combatant.Revive();
    }

    /// <summary>
    /// What dying costs (REF-02): experience toward the current level, by difficulty. The
    /// flask is not refilled — charges are bought, and a free refill would make dying the
    /// cheapest way to fill it.
    /// </summary>
    private void PayDeathCost()
    {
        var share = GameSession.Difficulty.ExperienceLossOnDeath;
        var lost = Progression.LoseExperience(share);

        if (lost <= 0) return;

        EmitSignal(SignalName.ExperienceChanged);

        GD.Print($"[progression] death cost {lost} experience ({share:P0} of level {Progression.Level})");

        UI.WorldNotice.Show(GetTree(), Kiln.Core.Foundation.L10n.F("Dying cost you {0} experience.", lost));
    }
}
