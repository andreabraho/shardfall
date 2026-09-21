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
        while (Progression.UnspentAttributePoints > 0)
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

    /// <summary>Learns whatever the current level unlocks.</summary>
    private void LearnSkills() =>
        GetParent().GetNodeOrNull<SkillCaster>("SkillCaster")?.LearnAvailable(Progression.Level);

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
    private Vector3 RespawnPoint()
    {
        var anchor = World.GameWorld.IsLoaded ? World.GameWorld.Travel.Anchor : null;

        if (anchor is null) return _spawnPoint;

        foreach (var node in GetTree().GetNodesInGroup("shrines"))
        {
            if (node is World.ShrineNode shrine && shrine.ShrineId == anchor)
            {
                return shrine.GlobalPosition + new Vector3(0, 0.1f, 2.5f);
            }
        }

        return _spawnPoint;
    }

    private async void OnDied()
    {
        var at = RespawnPoint();

        Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndPlayerDeath);

        GD.Print($"[combat] player died — respawning at {(World.GameWorld.IsLoaded ? World.GameWorld.Travel.Anchor ?? "start" : "start")}");
        _motor.Stop();

        await ToSignal(GetTree().CreateTimer(RespawnSeconds), SceneTreeTimer.SignalName.Timeout);

        if (!IsInstanceValid(this)) return;

        _motor.GlobalPosition = at;
        _motor.MovementLocked = false;
        _motor.Stop();
        _combatant.Revive();

        // Respawning with an empty flask would just feed the next death.
        GetParent().GetNodeOrNull<HealthFlask>("HealthFlask")?.Refill();
    }
}
