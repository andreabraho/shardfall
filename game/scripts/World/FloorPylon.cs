using Godot;
using Kiln.Core.Combat;
using Kiln.Core.Foundation;
using Kiln.Game.Combat;
using Kiln.Game.Visual;

namespace Kiln.Game.World;

/// <summary>
/// The thing a floor asks you to break (FR-7.11). A seal, a lamp, a keystone — one node,
/// dressed by whichever floor placed it.
/// </summary>
/// <remarks>
/// Every non-boss verb in the tower is built out of this. Break is one of them; carry is
/// three that drop keys; find is one real one among lookalikes; race is four against a clock.
/// Writing a separate prop per verb would have produced four nearly identical files that
/// drifted apart the first time one of them was tuned.
/// <para>
/// It is a <see cref="StaticBody3D"/> carrying a <see cref="Combatant"/>, the same shape as
/// a shard node, so the player's existing attacks hit it with no special case anywhere in
/// combat.
/// </para>
/// </remarks>
public partial class FloorPylon : StaticBody3D
{
    private Combatant _self = null!;
    private VisualRoot _visual = null!;
    private NamePlate _plate = null!;
    private double _phase;
    private bool _spent;

    /// <summary>Health. Set by the floor before the node enters the tree.</summary>
    [Export] public int Hitpoints { get; set; } = 900;

    /// <summary>
    /// Whether breaking this one counts.
    /// </summary>
    /// <remarks>
    /// Only a Find floor has any that do not. Everywhere else every pylon is real, which is
    /// why this defaults to true rather than being set on every placement.
    /// </remarks>
    [Export] public bool Real { get; set; } = true;

    /// <summary>
    /// Whether this one joins the chorus the decoys keep up.
    /// </summary>
    /// <remarks>
    /// This is the tell (FR-7.16). The original's version of this floor is one of seven
    /// identical stones against a clock, which with a party is a search and alone is a guess.
    /// Ours differs in something the player can see from across the room: the decoys all
    /// breathe together, and the real one does not. It is a perception test, not a lottery.
    /// </remarks>
    [Export] public bool Chants { get; set; }

    [Export] public string Label { get; set; } = "Seal";

    /// <summary>True once it has been broken.</summary>
    public bool Spent => _spent;

    /// <summary>Raised when the pylon is destroyed, whether or not it counted.</summary>
    [Signal] public delegate void ShatteredEventHandler(bool real);

    public override void _Ready()
    {
        AddToGroup("floor_pylons");

        CollisionLayer = Foundation.Layers.Enemy;
        CollisionMask = 0;

        _self = new Combatant { Name = "Combatant", IsEncounter = true };
        AddChild(_self);

        _self.Configure(
            new StatBlock
            {
                Level = 20,
                Family = MonsterFamily.Mystic,
                FlatMaxHp = Hitpoints * GameSession.Difficulty.EnemyHpMultiplier,
                FlatDefense = 30,
                FlatAttackPower = 0,
            },
            Label);

        _self.Health.Fill();
        _self.Died += OnDied;

        AddChild(new CollisionShape3D
        {
            Name = "Collision",
            Shape = new CylinderShape3D { Radius = 0.9f, Height = 2.6f },
            Position = new Vector3(0, 1.3f, 0),
        });

        _visual = new VisualRoot { Name = "VisualRoot", Position = new Vector3(0, 1.3f, 0) };
        AddChild(_visual);
        _visual.Apply("mesh_placeholder_monolith", Chants ? "#6a5fa8" : "#8f6ad2", 0.8);

        _plate = new NamePlate { Name = "NamePlate", Rank = NameRank.Elite, Offset = new Vector3(0, 3.0f, 0) };
        AddChild(_plate);
        _plate.SetText(Label);
    }

    /// <summary>
    /// The chorus. Decoys rise and fall in step; the real one stands still.
    /// </summary>
    /// <remarks>
    /// Driven off scene time rather than a per-node timer so that every decoy is at the same
    /// point in the cycle without any of them talking to each other. Two decoys drifting out
    /// of phase would read as two tells and ruin the floor.
    /// </remarks>
    public override void _Process(double delta)
    {
        if (_spent || !Chants) return;

        _phase = Time.GetTicksMsec() / 1000.0;

        var bob = 0.22f * Mathf.Sin((float)(_phase * 2.4));

        _visual.Position = new Vector3(0, 1.3f + bob, 0);
    }

    private void OnDied()
    {
        if (_spent) return;

        _spent = true;

        EmitSignal(SignalName.Shattered, Real);

        // Left in the world as rubble rather than vanishing: on a Find floor the broken
        // lookalikes are the record of what has already been ruled out.
        _visual.Position = new Vector3(0, 0.2f, 0);
        _visual.Scale = new Vector3(1f, 0.18f, 1f);
        _plate.Visible = false;
        SetProcess(false);

        CollisionLayer = 0;
    }
}
