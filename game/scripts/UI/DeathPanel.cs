using System;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Game.UI.Menus;

namespace Kiln.Game.UI;

/// <summary>
/// The screen a death leaves the player on (REF-14): where to get up again — at the last
/// shrine touched, or back in the village.
/// </summary>
/// <remarks>
/// The original's two buttons, "restart here" and "restart in town". The choices unlock after
/// a short wait, so a click meant for the fight that just ended cannot pick one by accident.
/// The world keeps running behind the veil; the player is dead, so nothing is chasing them.
/// </remarks>
public partial class DeathPanel : CanvasLayer
{
    private const string NodeName = "DeathPanel";

    private string _shrine = "";
    private string? _village;
    private double _wait;
    private bool _counted;
    private bool _chosen;

    private Label _countdown = null!;
    private Button _atShrine = null!;
    private Button? _atVillage;

    /// <summary>Raised once, with true when the player chose the village.</summary>
    public event Action<bool>? Chosen;

    /// <summary>
    /// Opens the screen. <paramref name="village"/> is null when the village is where the
    /// shrine choice already leads, so the screen does not offer the same place twice.
    /// </summary>
    public static DeathPanel Open(Node scene, string shrine, string? village, double wait)
    {
        scene.GetNodeOrNull<DeathPanel>(NodeName)?.QueueFree();

        var panel = new DeathPanel { Name = NodeName, _shrine = shrine, _village = village, _wait = wait };
        scene.AddChild(panel);

        return panel;
    }

    public override void _Ready()
    {
        Layer = 40;
        Build();
        UiState.SetOpen(ref _counted, true);
        Refresh();
    }

    public override void _ExitTree() => UiState.SetOpen(ref _counted, false);

    public override void _Process(double delta)
    {
        if (_wait <= 0) return;

        _wait -= delta;
        Refresh();
    }

    private void Refresh()
    {
        var locked = _wait > 0;

        _atShrine.Disabled = locked;

        if (_atVillage is not null) _atVillage.Disabled = locked;

        _countdown.Text = locked ? L10n.F("You can get up in {0:0.0} s", Math.Max(0, _wait)) : L10n.T("Where do you get up?");
    }

    private void Choose(bool village)
    {
        if (_chosen || _wait > 0) return;

        _chosen = true;
        UiState.SetOpen(ref _counted, false);
        Chosen?.Invoke(village);
        QueueFree();
    }

    private void Build()
    {
        // A dark red veil over the world, the panel in the middle of it.
        AddChild(new ColorRect
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            Color = new Color(0.18f, 0.02f, 0.02f, 0.55f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        });

        var root = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
        };

        root.AddThemeStyleboxOverride("panel", MenuStyle.Panel());
        AddChild(root);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(340, 0) };
        column.AddThemeConstantOverride("separation", 12);
        root.AddChild(column);

        var title = MenuStyle.Label(L10n.T("You have fallen"), 26, new Color(0.92f, 0.36f, 0.30f));
        title.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(title);

        _countdown = MenuStyle.Label("", 14, MenuStyle.Dim);
        _countdown.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_countdown);

        column.AddChild(new HSeparator());

        _atShrine = MenuStyle.Big(L10n.F("Get up at {0}", _shrine), () => Choose(false));
        column.AddChild(_atShrine);

        if (_village is not null)
        {
            _atVillage = MenuStyle.Big(L10n.F("Get up in the village ({0})", _village), () => Choose(true));
            column.AddChild(_atVillage);
        }
    }
}
