using Godot;
using Kiln.Core.Encounters;
using Kiln.Core.Foundation;
using Kiln.Game.World;

namespace Kiln.Game.UI;

/// <summary>
/// The shard frame: health, phase, modifier, and the reclamation bar the player is racing.
/// </summary>
/// <remarks>
/// The reclamation cast is the fight's tension beat, and a deadline nobody can see is not a
/// deadline — it is damage that arrives for no reason. This exists so the player can tell
/// that killing the marked add is the thing to do, and how long they have to do it.
/// </remarks>
public partial class ShardHud : CanvasLayer, ITopBar
{
    public Control Panel => _root;

    private PanelContainer _root = null!;
    private Label _title = null!;
    private InfoBadge _info = null!;
    private ProgressBar _health = null!;
    private Label _phase = null!;
    private ProgressBar _cast = null!;
    private Label _castLabel = null!;

    private ShardNode? _shard;

    public override void _Ready()
    {
        Layer = 11;
        AddToGroup("top_bars");
        Build();
        _root.Visible = false;
    }

    private void Build()
    {
        _root = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            OffsetLeft = -170,
            OffsetRight = 170,
            OffsetTop = TopBars.Top,
            // Hovered, it says what helps against the stone (FoeAdvice), as the boss bar does.
            MouseFilter = Control.MouseFilterEnum.Stop,
        };

        AddChild(_root);

        var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        _root.AddChild(margin);

        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 1);
        margin.AddChild(box);

        _title = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _title.AddThemeFontSizeOverride("font_size", 13);
        box.AddChild(FoeAdvice.Titled(_title, out _info));

        _health = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = 1,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(320, 10),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        Tint(_health, new Color("a05ad0"));
        box.AddChild(_health);

        _phase = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _phase.AddThemeFontSizeOverride("font_size", 10);
        _phase.AddThemeColorOverride("font_color", new Color(0.72f, 0.76f, 0.82f));
        box.AddChild(_phase);

        _castLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _castLabel.AddThemeFontSizeOverride("font_size", 11);
        _castLabel.AddThemeColorOverride("font_color", new Color("ffb347"));
        box.AddChild(_castLabel);

        _cast = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = 0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(320, 6),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        Tint(_cast, new Color("d06a4f"));
        box.AddChild(_cast);
    }

    private static void Tint(ProgressBar bar, Color fill)
    {
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = fill });
        bar.AddThemeStyleboxOverride("background", new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.11f, 0.13f, 0.9f),
            BorderColor = new Color(0.30f, 0.33f, 0.38f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
        });
    }

    public override void _Process(double delta)
    {
        _shard = NearestEngaged();

        if (_shard is null)
        {
            _root.Visible = false;
            return;
        }

        _root.Visible = true;

        // Under the target's frame, which goes under a tower's floor panel.
        var top = TopBars.First(GetTree());

        if (!Mathf.IsEqualApprox(_root.OffsetTop, top))
        {
            _root.OffsetBottom += top - _root.OffsetTop;
            _root.OffsetTop = top;
        }

        var modifier = _shard.Modifier == ShardModifier.None ? "" : $"  ·  {Words.Of(_shard.Modifier)}";

        _title.Text = $"{Items.GameItems.Localise("$shard." + _shard.ShardId + ".name")}{modifier}";
        _health.Value = _shard.Core.Health.Fraction;

        var advice = FoeAdvice.For(_shard.Core, GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<Combat.Combatant>("Combatant"), _shard.DropTableId);

        if (_root.TooltipText != advice) _root.TooltipText = advice;
        _info.Advice = advice;

        _phase.Text = _shard.Phase switch
        {
            ShardPhase.One => L10n.T("Phase 1  ·  step into a gap in the pulse"),
            ShardPhase.Two => L10n.T("Phase 2  ·  narrower gaps now"),
            ShardPhase.Three => L10n.T("Phase 3  ·  kill the marked add"),
            _ => "",
        };

        var reclaiming = _shard.IsReclaiming;

        _cast.Visible = reclaiming;
        _castLabel.Visible = reclaiming;

        if (!reclaiming) return;

        _cast.Value = _shard.ReclamationProgress;
        _castLabel.Text = L10n.T("Reclaiming — kill the marked add");
    }

    /// <summary>The engaged shard, if any. Only one fight can be active at a time in practice.</summary>
    private ShardNode? NearestEngaged()
    {
        foreach (var node in GetTree().GetNodesInGroup("shards"))
        {
            if (node is ShardNode shard && shard.IsEngaged) return shard;
        }

        return null;
    }
}
