using Godot;
using Kiln.Game.Input;

namespace Kiln.Game.World;

/// <summary>
/// An upgrade bench standing in the world (FR-7.14).
/// </summary>
/// <remarks>
/// The bench panel already opens from anywhere on a key, and that stays true — this does not
/// gate it. What this adds is the *place*: the requirement is that a bench stands immediately
/// after every boss floor, because the reward for a boss is a decision, and a decision means
/// most at the moment the player has just found out which piece of their kit is holding them
/// back. A keybind cannot be somewhere. This can.
/// </remarks>
public partial class BenchNode : Area3D
{
    private Combat.NamePlate? _plate;
    private bool _playerInside;

    [Export] public float Radius { get; set; } = 3.0f;

    public override void _Ready()
    {
        AddToGroup("benches");

        CollisionLayer = 0;
        CollisionMask = Foundation.Layers.Player;
        Monitoring = true;

        AddChild(new CollisionShape3D
        {
            Name = "Range",
            Shape = new SphereShape3D { Radius = Radius },
            Position = new Vector3(0, 1.0f, 0),
        });

        _plate = new Combat.NamePlate
        {
            Name = "NamePlate",
            Offset = new Vector3(0, 2.2f, 0),
            Rank = Combat.NameRank.Elite,
            Tint = new Color("c9b08a"),
        };

        AddChild(_plate);
        _plate.SetText("Workbench");

        BodyEntered += body => { if (body.IsInGroup("player")) _playerInside = true; };
        BodyExited += body => { if (body.IsInGroup("player")) _playerInside = false; };
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_playerInside || !@event.IsActionPressed(GameActions.Interact)) return;

        if (GetTree().CurrentScene?.GetNodeOrNull<UI.WorkbenchPanel>("Session/WorkbenchPanel") is not { } panel)
        {
            return;
        }

        panel.Open();
        GetViewport().SetInputAsHandled();
    }
}
