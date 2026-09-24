using Godot;
using Kiln.Core.Foundation;
using Kiln.Game.Combat;

namespace Kiln.Game.UI;

/// <summary>
/// The boss bar (REF-05): name, level, phase and health across the top of the screen while a
/// boss is fighting the player.
/// </summary>
/// <remarks>
/// A boss's own plate is over its head, in the middle of the fight, and it is the one thing
/// the player cannot afford to be reading. The bar is where the eye rests between blows, and
/// its phase line says when the fight has turned.
/// </remarks>
public partial class BossHud : CanvasLayer, ITopBar
{
    public Control Panel => _root;

    private PanelContainer _root = null!;
    private Label _title = null!;
    private ProgressBar _health = null!;
    private Label _phase = null!;

    public override void _Ready()
    {
        Layer = 11;
        AddToGroup("top_bars");

        _root = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            OffsetLeft = -280,
            OffsetRight = 280,
            OffsetTop = TopBars.Top,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_root);

        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        _root.AddChild(margin);

        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 3);
        margin.AddChild(box);

        _title = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _title.AddThemeFontSizeOverride("font_size", 18);
        _title.AddThemeColorOverride("font_color", new Color("d9a7ff"));
        box.AddChild(_title);

        _health = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = 1,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(530, 18),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _health.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color("b3302a") });
        _health.AddThemeStyleboxOverride("background", new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.11f, 0.13f, 0.9f),
            BorderColor = new Color(0.30f, 0.33f, 0.38f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
        });
        box.AddChild(_health);

        _phase = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _phase.AddThemeFontSizeOverride("font_size", 12);
        _phase.AddThemeColorOverride("font_color", new Color(0.78f, 0.8f, 0.85f));
        box.AddChild(_phase);

        _root.Visible = false;
    }

    public override void _Process(double delta)
    {
        var boss = Engaged();

        if (boss is null)
        {
            _root.Visible = false;
            return;
        }

        _root.Visible = true;

        var shard = GetParent()?.GetNodeOrNull<ShardHud>("ShardHud");
        var top = TopBars.Below(shard?.Panel);

        if (!Mathf.IsEqualApprox(_root.OffsetTop, top))
        {
            _root.OffsetBottom += top - _root.OffsetTop;
            _root.OffsetTop = top;
        }

        var name = Items.GameItems.Localise(boss.Self.DisplayName);
        _title.Text = $"{L10n.F("Lv. {0}", boss.Self.Stats.Level)}  {name}";
        _health.Value = boss.Self.Health.Fraction;
        _phase.Text = L10n.F("Phase {0}", boss.BossPhase);
    }

    /// <summary>The nearest boss that is fighting the player, if any.</summary>
    private EnemyBrain? Engaged()
    {
        var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
        EnemyBrain? best = null;
        var bestDistance = float.MaxValue;

        foreach (var node in GetTree().GetNodesInGroup("bosses"))
        {
            if (node is not EnemyBrain boss || boss.IsDead || !boss.HasLivingTarget) continue;

            var distance = player is null ? 0 : player.GlobalPosition.DistanceTo(boss.GlobalPosition);

            if (distance >= bestDistance) continue;

            best = boss;
            bestDistance = distance;
        }

        return best;
    }
}
