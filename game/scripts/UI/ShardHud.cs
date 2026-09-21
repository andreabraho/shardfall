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
public partial class ShardHud : CanvasLayer
{
    private PanelContainer _root = null!;
    private Label _title = null!;
    private ProgressBar _health = null!;
    private Label _phase = null!;
    private ProgressBar _cast = null!;
    private Label _castLabel = null!;

    private ShardNode? _shard;

    public override void _Ready()
    {
        Layer = 11;
        Build();
        _root.Visible = false;
    }

    private void Build()
    {
        _root = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            OffsetLeft = -260,
            OffsetRight = 260,
            OffsetTop = 92,
        };

        AddChild(_root);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        _root.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 3);
        margin.AddChild(box);

        _title = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _title.AddThemeFontSizeOverride("font_size", 17);
        box.AddChild(_title);

        _health = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = 1,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(500, 20),
        };

        Tint(_health, new Color("a05ad0"));
        box.AddChild(_health);

        _phase = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _phase.AddThemeFontSizeOverride("font_size", 12);
        _phase.AddThemeColorOverride("font_color", new Color(0.72f, 0.76f, 0.82f));
        box.AddChild(_phase);

        _castLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _castLabel.AddThemeFontSizeOverride("font_size", 13);
        _castLabel.AddThemeColorOverride("font_color", new Color("ffb347"));
        box.AddChild(_castLabel);

        _cast = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = 0,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(500, 10),
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

        var modifier = _shard.Modifier == ShardModifier.None ? "" : $"  ·  {Words.Of(_shard.Modifier)}";

        _title.Text = $"{Items.GameItems.Localise("$shard." + _shard.ShardId + ".name")}{modifier}";
        _health.Value = _shard.Core.Health.Fraction;

        _phase.Text = _shard.Phase switch
        {
            ShardPhase.One => L10n.T("Phase 1  ·  walk out of the pulse"),
            ShardPhase.Two => L10n.T("Phase 2  ·  two rings now"),
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
