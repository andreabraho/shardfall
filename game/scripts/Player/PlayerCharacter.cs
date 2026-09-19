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

    [Export] public int StartingLevel { get; set; } = 10;

    /// <summary>Respawn delay. Kept short: a long death screen makes a hard game feel unfair.</summary>
    [Export] public double RespawnSeconds { get; set; } = 1.5;

    /// <summary>Level, experience and unspent points (PRG-01/02).</summary>
    public CharacterProgression Progression { get; } = new();

    /// <summary>Which skills are learned, and their mastery (PRG-03/05).</summary>
    public SkillBook Skills { get; } = new();

    [Signal] public delegate void LeveledUpEventHandler(int level);

    [Signal] public delegate void ExperienceChangedEventHandler();

    public override void _Ready()
    {
        _motor = GetParent<PlayerMotor>();
        _combatant = GetParent().GetNode<Combat.Combatant>("Combatant");

        _spawnPoint = _motor.GlobalPosition;
        _motor.AddToGroup("player");

        // Start partway up the curve so combat can be tested at a meaningful power level.
        // A real new game begins at 1; the prologue quest chain covers the early levels.
        Progression.Grant(ExperienceTable.CumulativeTo(StartingLevel));
        SpendStartingPoints();

        _combatant.IsPlayer = true;
        ApplyStats();

        _combatant.Damaged += OnDamaged;
        _combatant.Died += OnDied;

        Debug.DebugOverlay.Register("hp", () => $"{_combatant.Health} ({_combatant.Health.Fraction:P0})");
        Debug.DebugOverlay.Register("level", () =>
            $"{Progression.Level} — {Progression.Experience}/{Progression.ExperienceForNextLevel} xp");

        // Skills the starting level already allows.
        CallDeferred(nameof(LearnSkills));
    }

    private void OnDamaged(int amount, bool critical, bool evaded)
    {
        var at = _motor.GlobalPosition + (Vector3.Up * 2.0f);
        Combat.CombatFeedback.Number(at, amount, critical, evaded, onPlayer: true);

        if (evaded) return;

        _motor.GetNodeOrNull<Visual.VisualRoot>("VisualRoot")?.Flash();

        // Weight the feedback by how much the hit actually mattered. A scratch gets a flash
        // and nothing more; only a real blow freezes the frame or moves the camera.
        var severity = (float)(amount / System.Math.Max(1.0, _combatant.Stats.MaxHp));

        if (severity >= 0.05f || critical)
        {
            Combat.CombatFeedback.HitStop(critical ? 0.06 : 0.04);
        }

        // Pushed away from whatever hit us, so the kick carries information rather than
        // just being motion. Only the player being hit moves the camera — reacting to every
        // blow the player lands would be constant noise.
        Camera.CameraRig.Kick(LastHitDirection(), critical ? severity * 1.4f : severity);
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
    /// Awards experience for a kill, adjusted for the level gap (FR-2.7).
    /// <para>
    /// Killing far below your level is worth almost nothing, so clearing an easy zone is
    /// never the efficient route — the campaign is meant to carry the player, not farming.
    /// </para>
    /// </summary>
    public void AwardKill(int enemyLevel, int baseXp)
    {
        if (baseXp <= 0 || Progression.IsMaxLevel) return;

        var scaled = (long)System.Math.Round(
            baseXp * ExperienceTable.CatchUpMultiplier(Progression.Level, enemyLevel));

        var levels = Progression.Grant(scaled);

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
        Combat.CombatFeedback.Number(
            _motor.GlobalPosition + (Vector3.Up * 2.6f), Progression.Level, critical: true, evaded: false);
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

    private async void OnDied()
    {
        GD.Print("[combat] player died — respawning at spawn point");
        _motor.Stop();

        await ToSignal(GetTree().CreateTimer(RespawnSeconds), SceneTreeTimer.SignalName.Timeout);

        if (!IsInstanceValid(this)) return;

        _motor.GlobalPosition = _spawnPoint;
        _motor.MovementLocked = false;
        _motor.Stop();
        _combatant.Revive();

        // Respawning with an empty flask would just feed the next death.
        GetParent().GetNodeOrNull<HealthFlask>("HealthFlask")?.Refill();
    }
}
