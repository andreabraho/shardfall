using System.Linq;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Data.Definitions;
using Kiln.Game.Input;
using Kiln.Game.Items;

namespace Kiln.Game.UI;

/// <summary>
/// The wardrobe (REF-23): the looks won from bosses, and which of them are worn — a sword
/// skin, an armour skin, an aura, a companion. Beside the character window, in its style.
/// </summary>
/// <remarks>
/// Every look is listed, won or not: one not yet won is greyed, with the boss that gives it,
/// so the list is also the answer to "what is there to win". A click wears it, or takes off
/// the one worn; "None" takes off whatever is there.
/// </remarks>
public partial class WardrobePanel : CanvasLayer
{
    private static readonly Color Gold = new(0.96f, 0.86f, 0.58f);
    private static readonly Color Frame = new(0.62f, 0.50f, 0.28f);
    private static readonly Color Dim = new(0.55f, 0.53f, 0.48f);

    private VBoxContainer _sections = null!;
    private Label _count = null!;
    private bool _counted;
    private Control _root = null!;

    /// <summary>Where it opens: just right of the character window.</summary>
    public float Left { get; set; } = 24 + 330;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        Layer = 23;
        Build();
        Visible = false;
        PlayerProfile.Wardrobe.Changed += OnChanged;
    }

    public override void _ExitTree()
    {
        PlayerProfile.Wardrobe.Changed -= OnChanged;
        UiState.SetSide(ref _counted, _root, false);
    }

    private void OnChanged()
    {
        if (Visible) Refresh();
    }

    public void SetOpen(bool open)
    {
        Visible = open;

        // A side panel, as the character window it opens from: play goes on.
        UiState.SetSide(ref _counted, _root, open);

        if (open) Refresh();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || !@event.IsActionPressed(GameActions.Cancel)) return;

        SetOpen(false);
        GetViewport().SetInputAsHandled();
    }

    // ------------------------------------------------------------------ build

    private void Build()
    {
        var root = new PanelContainer
        {
            OffsetLeft = Left,
            OffsetTop = 210,
            GrowVertical = Control.GrowDirection.End,
        };

        // Moved by holding its border or title and dragging (REF-19).
        PanelMover.Attach(root, "wardrobe");
        _root = root;

        root.AddThemeStyleboxOverride("panel", Box(new Color(0.06f, 0.055f, 0.05f, 0.96f), 2, 0));
        AddChild(root);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 0);
        root.AddChild(column);

        var bar = new PanelContainer();
        bar.AddThemeStyleboxOverride("panel", Box(new Color(0.20f, 0.14f, 0.07f), 0, 8, bottom: 1));
        column.AddChild(bar);

        var barRow = new HBoxContainer();
        bar.AddChild(barRow);

        var title = new Label { Text = L10n.T("Wardrobe"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 15);
        title.AddThemeColorOverride("font_color", Gold);
        barRow.AddChild(title);

        var cross = new Button { Text = "✕", Flat = true, FocusMode = Control.FocusModeEnum.None };
        cross.AddThemeFontSizeOverride("font_size", 13);
        cross.Pressed += () => SetOpen(false);
        barRow.AddChild(cross);

        var body = new MarginContainer();
        body.AddThemeConstantOverride("margin_left", 14);
        body.AddThemeConstantOverride("margin_right", 14);
        body.AddThemeConstantOverride("margin_top", 10);
        body.AddThemeConstantOverride("margin_bottom", 10);
        column.AddChild(body);

        _sections = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        _sections.AddThemeConstantOverride("separation", 8);
        body.AddChild(_sections);

        var foot = new PanelContainer();
        foot.AddThemeStyleboxOverride("panel", Box(new Color(0.10f, 0.08f, 0.06f), 0, 8, top: 1));
        column.AddChild(foot);

        var footRow = new HBoxContainer();
        foot.AddChild(footRow);

        _count = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
        _count.AddThemeFontSizeOverride("font_size", 11);
        _count.AddThemeColorOverride("font_color", Dim);
        footRow.AddChild(_count);

        var close = Menus.MenuStyle.Small(L10n.T("Close"), () => SetOpen(false));
        close.FocusMode = Control.FocusModeEnum.None;
        footRow.AddChild(close);
    }

    // ------------------------------------------------------------------ refresh

    private void Refresh()
    {
        UiNodes.Clear(_sections);

        Section(Cosmetics.Sword, L10n.T("Sword — both blades"));
        Section(Cosmetics.Armour, L10n.T("Armour"));
        Section(Cosmetics.Aura, L10n.T("Aura"));
        Section(Cosmetics.Companion, L10n.T("Companion"));

        var total = GameContent.IsLoaded ? GameContent.Database.Cosmetics.Count : 0;

        _count.Text = L10n.F("{0} of {1} won — every boss has a look to give", PlayerProfile.Wardrobe.Owned.Count, total);

        Callable.From(() => { if (IsInstanceValid(_root)) _root.ResetSize(); }).CallDeferred();
    }

    private void Section(string kind, string heading)
    {
        _sections.AddChild(Heading(heading));

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 4);
        _sections.AddChild(grid);

        var worn = PlayerProfile.Wardrobe.WornIn(kind);

        grid.AddChild(Choice(L10n.T("None"), "", worn is null, owned: true, () => PlayerProfile.Wardrobe.Wear(kind, null)));

        foreach (var look in Cosmetics.OfKind(kind))
        {
            var owned = PlayerProfile.Wardrobe.Owns(look.Id);
            var id = look.Id;

            grid.AddChild(Choice(
                GameItems.Localise(look.Name),
                BossName(look),
                worn == look.Id,
                owned,
                () => PlayerProfile.Wardrobe.Wear(kind, worn == id ? null : id)));
        }
    }

    private static string BossName(CosmeticDef look) =>
        GameContent.Database.Enemies.TryGetValue(look.Boss, out var boss) ? GameItems.Localise(boss.Name) : look.Boss;

    /// <summary>One look as a plate: gold-edged when worn, greyed with its boss when not yet won.</summary>
    private static Button Choice(string name, string boss, bool worn, bool owned, System.Action pick)
    {
        var button = new Button
        {
            Text = name,
            CustomMinimumSize = new Vector2(176, 32),
            Disabled = !owned,
            FocusMode = Control.FocusModeEnum.None,
            ClipText = true,
            TooltipText = boss.Length == 0 ? "" : owned ? L10n.F("From {0}", boss) : L10n.F("Defeat {0} to win it", boss),
        };

        var edge = worn ? Gold : new Color(0.30f, 0.25f, 0.18f);
        var fill = worn ? new Color(0.22f, 0.16f, 0.08f) : new Color(0.10f, 0.09f, 0.08f);

        button.AddThemeStyleboxOverride("normal", Box(fill, worn ? 2 : 1, 6, edge: edge));
        button.AddThemeStyleboxOverride("hover", Box(new Color(0.17f, 0.13f, 0.08f), worn ? 2 : 1, 6, edge: worn ? Gold : Frame));
        button.AddThemeStyleboxOverride("pressed", Box(new Color(0.22f, 0.16f, 0.08f), 2, 6, edge: Gold));
        button.AddThemeStyleboxOverride("disabled", Box(new Color(0.07f, 0.065f, 0.06f), 1, 6, edge: new Color(0.20f, 0.18f, 0.15f)));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        button.AddThemeColorOverride("font_color", worn ? Gold : new Color(0.90f, 0.88f, 0.82f));
        button.AddThemeColorOverride("font_hover_color", Gold);
        button.AddThemeColorOverride("font_disabled_color", Dim);
        button.AddThemeFontSizeOverride("font_size", 13);
        button.Pressed += pick;

        return button;
    }

    private static Control Heading(string text)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);

        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color(0.85f, 0.72f, 0.45f));
        box.AddChild(label);

        box.AddChild(new ColorRect { Color = new Color(Frame, 0.6f), CustomMinimumSize = new Vector2(0, 1) });

        return box;
    }

    private static StyleBoxFlat Box(Color fill, int width, int margin, int bottom = -1, int top = -1, Color? edge = null) => new()
    {
        BgColor = fill,
        BorderColor = edge ?? Frame,
        BorderWidthTop = top >= 0 ? top : width,
        BorderWidthLeft = width,
        BorderWidthRight = width,
        BorderWidthBottom = bottom >= 0 ? bottom : width,
        ContentMarginLeft = margin + 4,
        ContentMarginRight = margin,
        ContentMarginTop = margin * 0.6f,
        ContentMarginBottom = margin * 0.6f,
    };
}
