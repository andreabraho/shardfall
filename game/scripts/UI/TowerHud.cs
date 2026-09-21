using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.World;
using Kiln.Game.World;

namespace Kiln.Game.UI;

/// <summary>
/// What this floor is asking for, and how far along it is (FR-7.15).
/// </summary>
/// <remarks>
/// The arrival notice says the task once and fades; this is the copy that stays. A player who
/// looked away, or came back after dying, must be able to find out what the floor wants
/// without walking into something to remind them.
/// <para>
/// It shows nothing at all outside a tower, so it can live in the shared session scene
/// alongside every other panel rather than being a thing the dungeon remembers to carry.
/// </para>
/// </remarks>
public partial class TowerHud : CanvasLayer
{
    private static readonly Color Calm = new(0.86f, 0.82f, 0.72f);
    private static readonly Color Urgent = new(0.92f, 0.55f, 0.42f);

    private PanelContainer _panel = null!;
    private Label _floor = null!;
    private Label _task = null!;
    private Label _state = null!;
    private TowerNode? _tower;

    public override void _Ready()
    {
        Layer = 18;
        Build();

        _panel.Visible = false;
        CallDeferred(nameof(Bind));
    }

    private void Build()
    {
        _panel = new PanelContainer { Name = "Panel" };
        _panel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _panel.OffsetTop = 14;
        _panel.OffsetLeft = -180;
        _panel.OffsetRight = 180;

        var box = new VBoxContainer { Name = "Box" };
        box.AddThemeConstantOverride("separation", 2);

        _floor = Line(20, new Color(0.96f, 0.90f, 0.76f));
        _task = Line(14, Calm);
        _state = Line(16, Calm);

        box.AddChild(_floor);
        box.AddChild(_task);
        box.AddChild(_state);

        _panel.AddChild(box);
        AddChild(_panel);
    }

    private static Label Line(int size, Color colour)
    {
        var label = new Label { HorizontalAlignment = HorizontalAlignment.Center };

        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", colour);

        return label;
    }

    /// <summary>
    /// Finds the tower, if this scene is one.
    /// </summary>
    /// <remarks>
    /// Deferred: the session scene and the zone are siblings, and on the frame this is ready
    /// the tower may not be.
    /// </remarks>
    private void Bind()
    {
        _tower = GetTree().GetFirstNodeInGroup("tower") as TowerNode;

        if (_tower is null) return;

        _tower.FloorChanged += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        if (_tower?.Run is not { } run)
        {
            _panel.Visible = false;
            return;
        }

        _panel.Visible = true;

        _floor.Text = L10n.F("{0}   ·   {1} of {2}", Items.GameItems.Localise(run.Floor.Name), run.Depth, run.FloorCount);
        _task.Text = TowerNode.Brief(run.Floor);
        _state.Text = State(run);

        // The colour is the warning. A race clock under ten seconds is the one moment in the
        // tower where the number on the screen is more urgent than anything in the room.
        var pressed = run.Floor.Task == FloorTask.Race && run.ClockRunning && run.Remaining < 10;

        _state.AddThemeColorOverride("font_color", pressed ? Urgent : Calm);
    }

    private static string State(TowerRun run) => run.Phase switch
    {
        FloorPhase.Open => L10n.T("The stair is open."),
        _ => run.Floor.Task switch
        {
            FloorTask.Hold => $"{run.Remaining:F0}s",
            FloorTask.Race => run.ClockRunning ? L10n.F("{0:F0}s   ·   {1} of {2}", run.Remaining, run.Scored, run.Floor.Targets) : "—",
            FloorTask.Fight => "",
            FloorTask.Find => "",
            _ => L10n.F("{0} of {1}", run.Scored, run.Floor.Targets),
        },
    };
}
