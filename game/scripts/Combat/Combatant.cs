using Godot;
using Kiln.Core.Combat;
using Kiln.Core.Foundation;
using Kiln.Data.Definitions;

namespace Kiln.Game.Combat;

/// <summary>
/// Anything that can fight: holds the Core stat block, health pool and statuses, and is the
/// single place damage is applied.
/// <para>
/// A component rather than a base class, so the player keeps <c>PlayerMotor</c> on its body
/// and enemies keep their brain, without a script-inheritance tangle.
/// </para>
/// </summary>
public partial class Combatant : Node
{
    [Signal] public delegate void DiedEventHandler();

    [Signal] public delegate void DamagedEventHandler(int amount, bool critical, bool evaded);

    [Signal] public delegate void HealthChangedEventHandler(float fraction);

    private double _statusAccumulator;
    private double _sinceCombat = 999;

    /// <summary>Seconds without taking or dealing damage before regeneration goes back to full rate.</summary>
    private const double OutOfCombatSeconds = 5.0;

    public StatBlock Stats { get; private set; } = new();
    public Pool Health { get; private set; } = new(100);
    public Pool Mana { get; private set; } = new(100);
    public StatusEffectSet Statuses { get; } = new();

    public bool IsAlive => !Health.IsEmpty;

    /// <summary>The body this component belongs to — used for positioning effects.</summary>
    public Node3D Body => GetParent<Node3D>();

    /// <summary>Display name key, for the target frame.</summary>
    public string DisplayName { get; set; } = "";

    public bool IsPlayer { get; set; }

    /// <summary>
    /// Multiplier on incoming damage, set by defensive abilities such as Guard Stance.
    /// Separate from resistances and mitigation so it is never capped by them — a 70%
    /// reduction must actually be 70%.
    /// </summary>
    public double IncomingDamageMultiplier { get; set; } = 1.0;

    public override void _Ready() => AddToGroup("combatants");

    public void Configure(StatBlock stats, string displayName)
    {
        Stats = stats;
        DisplayName = displayName;
        Health = new Pool(stats.MaxHp);
        Mana = new Pool(stats.MaxMana);
        EmitSignal(SignalName.HealthChanged, (float)Health.Fraction);
    }

    /// <summary>Builds an enemy from its data definition, scaled by the active difficulty.</summary>
    public void ConfigureFromEnemy(EnemyDef def)
    {
        var difficulty = GameSession.Difficulty;

        Configure(
            new StatBlock
            {
                Level = def.Level,
                Family = def.Family,
                // Difficulty scales enemy HP and damage only — never drops or XP (doc 02 §1).
                FlatMaxHp = def.Stats.Hp * difficulty.EnemyHpMultiplier,
                FlatAttackPower = def.Stats.AttackPower,
                FlatDefense = def.Stats.Defense,
            },
            def.Name);
    }

    /// <summary>
    /// Resolves an attack from <paramref name="attacker"/> against this combatant and
    /// applies the result. Returns it so the caller can drive feedback.
    /// </summary>
    public DamageResult TakeAttack(Combatant attacker, double weaponCoef = 1.0, double skillCoef = 1.0)
    {
        if (!IsAlive) return DamageResult.Miss;

        var request = new DamageRequest
        {
            Attacker = attacker.Stats,
            Defender = Stats,
            WeaponCoef = weaponCoef,
            SkillCoef = skillCoef,
            // Difficulty raises what the player takes, never what they deal.
            DifficultyDamageMultiplier = attacker.IsPlayer ? 1.0 : GameSession.Difficulty.EnemyDamageMultiplier,
        };

        attacker._sinceCombat = 0;

        var result = DamagePipeline.Resolve(request, GameSession.CombatRng);

        if (result.Evaded)
        {
            EmitSignal(SignalName.Damaged, 0, false, true);
            return result;
        }

        var scaled = (int)Math.Round(
            result.Amount
            * Statuses.DamageTakenMultiplier
            * attacker.Statuses.DamageDealtMultiplier
            * IncomingDamageMultiplier);

        ApplyDamage(Math.Max(1, scaled), result.Critical);
        return result with { Amount = scaled };
    }

    /// <summary>Applies raw damage, bypassing the pipeline. Used by status ticks and scripted effects.</summary>
    public void ApplyDamage(int amount, bool critical = false)
    {
        if (!IsAlive || amount <= 0) return;

        _sinceCombat = 0;
        Health.Remove(amount);

        EmitSignal(SignalName.Damaged, amount, critical, false);
        EmitSignal(SignalName.HealthChanged, (float)Health.Fraction);

        if (Health.IsEmpty)
        {
            EmitSignal(SignalName.Died);
        }
    }

    public void Heal(int amount)
    {
        if (!IsAlive || amount <= 0) return;

        Health.Add(amount);
        EmitSignal(SignalName.HealthChanged, (float)Health.Fraction);
    }

    public void Revive()
    {
        Health.Fill();
        Mana.Fill();
        Statuses.Clear();
        EmitSignal(SignalName.HealthChanged, (float)Health.Fraction);
    }

    public override void _Process(double delta)
    {
        if (!IsAlive) return;

        // Statuses tick at 10 Hz rather than per frame: damage-over-time does not need
        // frame precision and this keeps the cost flat as enemy counts grow.
        _statusAccumulator += delta;
        if (_statusAccumulator < 0.1) return;

        var step = _statusAccumulator;
        _statusAccumulator = 0;

        var tick = Statuses.Tick(step);
        if (tick.Damage > 0)
        {
            ApplyDamage((int)Math.Round(tick.Damage));
        }

        _sinceCombat += step;

        // In-combat regeneration is deliberately throttled so resources pace a fight
        // (doc 06 §2). Out of combat it returns to the full rate, otherwise recovering
        // after a pull would mean standing still for minutes.
        var inCombat = _sinceCombat < OutOfCombatSeconds;

        Mana.Add((inCombat ? Stats.ManaRegenInCombat : Stats.ManaRegenPerSecond) * step);
        Health.Add((inCombat ? Stats.HpRegenInCombat : Stats.HpRegenPerSecond) * step);
    }
}
