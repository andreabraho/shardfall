using Godot;
using Kiln.Core.Combat;
using Kiln.Core.Foundation;
using Kiln.Core.Progression;
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

    /// <summary>Knocked down instead of killed — the one death <see cref="CheatDeath"/> buys.</summary>
    [Signal] public delegate void CollapsedEventHandler();

    private double _statusAccumulator;
    private double _sinceCombat = 999;

    /// <summary>Seconds without taking or dealing damage before regeneration goes back to full rate.</summary>
    private const double OutOfCombatSeconds = 5.0;

    public StatBlock Stats { get; private set; } = new();
    public Pool Health { get; private set; } = new(100);
    public Pool Mana { get; private set; } = new(100);
    public StatusEffectSet Statuses { get; } = new();

    /// <summary>
    /// Alive while at least one whole hit point is left.
    /// </summary>
    /// <remarks>
    /// Health is fractional — regeneration and the proportional rescale on a level-up both
    /// leave fractions — and the bar shows whole numbers. A player on 0.4 read "0" on the bar
    /// and was still standing and fighting, because death waited for exactly zero.
    /// </remarks>
    public bool IsAlive => Health.Current >= 1;

    private bool _deathSignalled;

    /// <summary>The body this component belongs to — used for positioning effects.</summary>
    public Node3D Body => GetParent<Node3D>();

    /// <summary>Whether this combatant is standing inside a village (WLD-12).</summary>
    public bool OnSafeGround =>
        GetParent() is Node3D body && World.GameWorld.IsLoaded && World.GameWorld.IsSafe(body.GlobalPosition);

    /// <summary>Display name key, for the target frame.</summary>
    public string DisplayName { get; set; } = "";

    public bool IsPlayer { get; set; }

    /// <summary>A designed encounter — a shard — rather than an ordinary enemy.</summary>
    public bool IsEncounter { get; set; }

    /// <summary>
    /// Multiplier on incoming damage, set by defensive abilities such as Guard Stance.
    /// Separate from resistances and mitigation so it is never capped by them — a 70%
    /// reduction must actually be 70%.
    /// </summary>
    public double IncomingDamageMultiplier { get; set; } = 1.0;

    /// <summary>Scales every blow this combatant lands. An enraged creature's is above one.</summary>
    public double OutgoingDamageMultiplier { get; set; } = 1.0;

    /// <summary>Fraction of a hit taken from close by that is turned back on the attacker.</summary>
    public double Thorns { get; set; }

    /// <summary>Metres within which a hit counts as close enough to prick on thorns.</summary>
    private const float ThornsReach = 4.5f;

    /// <summary>
    /// The next killing blow knocks it down instead (a bone minion's rise). Spent when used.
    /// </summary>
    public bool CheatDeath { get; set; }

    /// <summary>Down and taking nothing, until its owner stands it back up.</summary>
    public bool Downed { get; private set; }

    /// <summary>Stands a downed combatant back up with this fraction of its health.</summary>
    public void StandUp(double fraction)
    {
        if (!Downed) return;

        Downed = false;
        Health.SetCurrent(System.Math.Max(1, Health.Max * fraction));
        EmitSignal(SignalName.HealthChanged, (float)Health.Fraction);
    }

    public override void _Ready() => AddToGroup("combatants");

    public void Configure(StatBlock stats, string displayName)
    {
        Stats = stats;
        DisplayName = displayName;
        Health = new Pool(stats.MaxHp);
        Mana = new Pool(stats.MaxMana);

        // A shard or pylon configured again for its next life may die again. The player never
        // comes back through here — a level-up reconfigures a living character, and clearing
        // the flag mid-respawn would kill them twice.
        if (!IsPlayer) _deathSignalled = false;

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
    /// <param name="skill">A skill or a special ability, not a basic attack: it takes skill damage, not average damage.</param>
    /// <param name="pierce">Ignores this combatant's defence (REF-21, the Piercing Blow).</param>
    public DamageResult TakeAttack(Combatant attacker, double weaponCoef = 1.0, double skillCoef = 1.0, bool skill = false, bool pierce = false)
    {
        if (!IsAlive) return DamageResult.Miss;

        // Safe ground (WLD-12). Returned as a miss rather than silently dropped so the feedback
        // the player sees matches what happened: the blow arrived and did nothing.
        if (IsPlayer && !attacker.IsPlayer && OnSafeGround) return DamageResult.Miss;

        var request = new DamageRequest
        {
            Attacker = attacker.Stats,
            Defender = Stats,
            WeaponCoef = weaponCoef,
            SkillCoef = skillCoef,
            IsSkill = skill,
            AlwaysPierces = pierce,
            // Difficulty raises what the player takes, never what they deal.
            DifficultyDamageMultiplier = attacker.IsPlayer ? 1.0 : GameSession.Difficulty.EnemyDamageMultiplier,
        };

        attacker._sinceCombat = 0;

        // No one-hit kill for enemies far below the player (removed 2026-09-24): they are
        // outmatched already, and a trivial kill pays half its drops instead.

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
            * attacker.OutgoingDamageMultiplier
            * IncomingDamageMultiplier);

        ApplyDamage(Math.Max(1, scaled), result.Critical);

        // Thorns prick only what stands close: a thrown blade or a spell from across the field
        // is not touching the spines.
        if (Thorns > 0 && attacker.IsPlayer && attacker.IsAlive
            && attacker.Body.GlobalPosition.DistanceTo(Body.GlobalPosition) <= ThornsReach)
        {
            attacker.ApplyDamage((int)Math.Max(1, Math.Round(scaled * Thorns)));
        }
        return result with { Amount = scaled };
    }

    /// <summary>Applies raw damage, bypassing the pipeline. Used by status ticks and scripted effects.</summary>
    public void ApplyDamage(int amount, bool critical = false)
    {
        if (!IsAlive || amount <= 0 || Downed) return;

        // The backstop for everything that does not come through the pipeline — a bleed
        // carried in from the field, a shard pulse, a scripted effect. Walking into the
        // village with a stack of poison on and dying at the well would make the promise
        // technically kept and completely broken.
        if (IsPlayer && OnSafeGround) return;

        _sinceCombat = 0;
        Health.Remove(amount);

        EmitSignal(SignalName.Damaged, amount, critical, false);
        EmitSignal(SignalName.HealthChanged, (float)Health.Fraction);

        if (Health.Current < 1)
        {
            if (CheatDeath)
            {
                CheatDeath = false;
                Downed = true;
                Statuses.Clear();
                Health.SetCurrent(1);
                EmitSignal(SignalName.Collapsed);
                return;
            }

            Die();
        }
    }

    public void Heal(int amount)
    {
        if (!IsAlive || amount <= 0) return;

        Health.Add(amount);
        EmitSignal(SignalName.HealthChanged, (float)Health.Fraction);
    }

    /// <summary>
    /// Empties the pool and says so, once.
    /// </summary>
    /// <remarks>
    /// The one way a death is announced. A stat refresh that sets health straight to zero — a
    /// level-up or a gear change landing at the wrong moment — used to leave the player at
    /// nothing without a death ever being raised: not alive, never respawned. The watchdog in
    /// <see cref="_Process"/> sends such a player through here too.
    /// </remarks>
    private void Die()
    {
        if (_deathSignalled) return;

        _deathSignalled = true;
        Health.SetCurrent(0);

        if (IsPlayer) GD.Print($"[combat] player health reached zero ({Health.Max:0} max)");

        EmitSignal(SignalName.Died);
    }

    public void Revive()
    {
        _deathSignalled = false;
        Health.Fill();
        Mana.Fill();
        Statuses.Clear();
        EmitSignal(SignalName.HealthChanged, (float)Health.Fraction);
    }

    public override void _Process(double delta)
    {
        if (!IsAlive)
        {
            // Nothing left, and no death was ever raised for it: raise it now.
            if (IsPlayer && !_deathSignalled) Die();

            return;
        }

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
