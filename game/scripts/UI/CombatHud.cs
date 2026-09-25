using Godot;
using Kiln.Core.Foundation;
using Kiln.Game.Combat;
using Kiln.Game.Player;

namespace Kiln.Game.UI;

/// <summary>
/// The target frame, top centre: the name and health of what the player is fighting.
/// </summary>
/// <remarks>
/// The player's own health, mana, level, experience, flask and statuses moved to the
/// <see cref="TaskBar"/> along the bottom (REF-19), where the original keeps them.
/// </remarks>
public partial class CombatHud : CanvasLayer
{
    private PanelContainer _targetPanel = null!;
    private ProgressBar _targetBar = null!;
    private Label _targetLabel = null!;

    private PlayerCombat? _combat;

    public override void _Ready()
    {
        Layer = 10;
        BuildTargetFrame();
        CallDeferred(nameof(Bind));
    }

    private void Bind()
    {
        _combat = GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<PlayerCombat>("PlayerCombat");
    }

    private void BuildTargetFrame()
    {
        _targetPanel = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            OffsetLeft = -160,
            OffsetRight = 160,
            OffsetTop = 24,
            Visible = false,
        };

        var box = new VBoxContainer();
        _targetLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _targetBar = new ProgressBar { MinValue = 0, MaxValue = 1, Value = 1, ShowPercentage = false, CustomMinimumSize = new Vector2(300, 18) };

        box.AddChild(_targetLabel);
        box.AddChild(_targetBar);
        _targetPanel.AddChild(box);
        AddChild(_targetPanel);
    }

    public override void _Process(double _)
    {
        if (_combat is null) return;

        var target = _combat.Target;

        if (target is null || !IsInstanceValid(target) || !target.IsAlive)
        {
            _targetPanel.Visible = false;
            return;
        }

        _targetPanel.Visible = true;
        _targetBar.Value = target.Health.Fraction;

        _targetLabel.Text = $"{Pretty(target.DisplayName)}   {target.Health}";
    }

    private static string Pretty(string key) =>
        string.IsNullOrEmpty(key) ? L10n.T("Target") : Items.GameItems.Localise(key);
}
