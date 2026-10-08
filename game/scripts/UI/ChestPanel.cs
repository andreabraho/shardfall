using System.Collections.Generic;
using System.Linq;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.Items;
using Kiln.Game.Input;
using Kiln.Game.Items;
using Kiln.Game.World;

namespace Kiln.Game.UI;

/// <summary>
/// The Abyss chest opened (2026-10-08, at your call): every piece of the set laid out, the player
/// picks one, and for a piece born with a stat — earring, bracelet, ring — which stat it has.
/// </summary>
/// <remarks>
/// The pieces are the player's class's own (a sword named is the Mental Warrior's great sword, an
/// armour named is the Archer's), so what is shown is what can be worn. The lines and sockets are
/// rolled as any drop's are; only the piece and its born stat are chosen.
/// </remarks>
public partial class ChestPanel : CanvasLayer
{
    private const float Cell = 56f;

    private static readonly Color Gold = new(0.96f, 0.86f, 0.58f);
    private static readonly Color Soft = new(0.72f, 0.70f, 0.64f);
    private static readonly Color Dim = new(0.55f, 0.53f, 0.48f);
    private static readonly Color Edge = new(0.30f, 0.25f, 0.18f);

    private PanelContainer _panel = null!;
    private GridContainer _pieces = null!;
    private Control _statBlock = null!;
    private VBoxContainer _stats = null!;
    private Label _choice = null!;
    private Button _take = null!;
    private ItemTip _tip = null!;

    private ChestNode? _chest;
    private string? _picked;
    private int? _stat;
    private bool _counted;

    public bool IsOpen => _panel.Visible;

    public override void _Ready()
    {
        Layer = 22;
        Build();
        _panel.Visible = false;
    }

    public void Open(ChestNode chest)
    {
        if (IsOpen && _chest == chest) return;

        _chest = chest;
        _picked = null;
        _stat = null;
        _panel.Visible = true;
        UiState.SetOpen(ref _counted, true);
        Refresh();
    }

    private void Close()
    {
        _panel.Visible = false;
        _tip.Clear();
        UiState.SetOpen(ref _counted, false);
    }

    public override void _ExitTree() => UiState.SetOpen(ref _counted, false);

    public override void _Process(double delta)
    {
        // The chest gone with its floor, or the player gone from the tower: nothing left to choose from.
        if (IsOpen && (_chest is null || !IsInstanceValid(_chest) || _chest.Spent)) Close();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsOpen || !@event.IsActionPressed(GameActions.Cancel)) return;

        Close();
        GetViewport().SetInputAsHandled();
    }

    // ------------------------------------------------------------------ build

    private void Build()
    {
        var root = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
        };

        PanelMover.Attach(root, "chest");
        root.AddThemeStyleboxOverride("panel", ShrinePanel.Box(new Color(0.06f, 0.055f, 0.05f, 0.96f), 2, 0));
        AddChild(root);
        _panel = root;

        var outer = new VBoxContainer();
        outer.AddThemeConstantOverride("separation", 0);
        root.AddChild(outer);

        var bar = new PanelContainer();
        bar.AddThemeStyleboxOverride("panel", ShrinePanel.Box(new Color(0.20f, 0.14f, 0.07f), 0, 8, bottom: 1));
        outer.AddChild(bar);

        var barRow = new HBoxContainer();
        bar.AddChild(barRow);

        var title = new Label { Text = L10n.T("Abyss chest"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 15);
        title.AddThemeColorOverride("font_color", Gold);
        barRow.AddChild(title);

        var cross = new Button { Text = "✕", Flat = true, FocusMode = Control.FocusModeEnum.None };
        cross.AddThemeFontSizeOverride("font_size", 13);
        cross.Pressed += Close;
        barRow.AddChild(cross);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        outer.AddChild(margin);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(460, 0) };
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);

        var notice = new Label
        {
            Text = L10n.T("Choose one piece of the set to take. The chest is empty once you have."),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };

        notice.AddThemeFontSizeOverride("font_size", 12);
        notice.AddThemeColorOverride("font_color", Soft);
        column.AddChild(notice);

        column.AddChild(ShrinePanel.Heading(L10n.T("Piece")));

        _pieces = new GridContainer { Columns = 5 };
        _pieces.AddThemeConstantOverride("h_separation", 8);
        _pieces.AddThemeConstantOverride("v_separation", 8);
        column.AddChild(_pieces);

        var statBlock = new VBoxContainer();
        statBlock.AddThemeConstantOverride("separation", 6);
        statBlock.AddChild(ShrinePanel.Heading(L10n.T("Stat")));

        _stats = new VBoxContainer();
        _stats.AddThemeConstantOverride("separation", 3);
        statBlock.AddChild(_stats);
        column.AddChild(statBlock);
        _statBlock = statBlock;

        _choice = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _choice.AddThemeFontSizeOverride("font_size", 12);
        _choice.AddThemeColorOverride("font_color", Gold);
        column.AddChild(_choice);

        _take = new Button { Text = L10n.T("Take"), CustomMinimumSize = new Vector2(0, 32), FocusMode = Control.FocusModeEnum.None };
        ShrinePanel.Plate(_take);
        _take.Pressed += AskTake;
        column.AddChild(_take);

        var foot = new PanelContainer();
        foot.AddThemeStyleboxOverride("panel", ShrinePanel.Box(new Color(0.10f, 0.08f, 0.06f), 0, 8, top: 1));
        outer.AddChild(foot);

        var footRow = new HBoxContainer();
        foot.AddChild(footRow);

        var hint = new Label { Text = L10n.T("Esc to close — the chest stays until you choose"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", Dim);
        footRow.AddChild(hint);

        var close = Menus.MenuStyle.Small(L10n.T("Close"), Close);
        close.FocusMode = Control.FocusModeEnum.None;
        footRow.AddChild(close);

        // Last, so it is drawn over the window it describes.
        _tip = new ItemTip();
        AddChild(_tip);
    }

    // ------------------------------------------------------------------ refresh

    /// <summary>The pieces this player can wear, one each: the class's own of every piece named.</summary>
    private List<string> Offer() => _chest is null
        ? []
        : _chest.Offer.Select(GameItems.ForClass).Distinct().Where(id => GameItems.Spec(id) is not null).ToList();

    private void Refresh()
    {
        Callable.From(() => PanelMover.Settle(_panel, "chest")).CallDeferred();

        UiNodes.Clear(_pieces);

        foreach (var id in Offer()) _pieces.AddChild(Piece(id));

        RefreshStats();
    }

    /// <summary>One piece: its icon on a plate, its name under it, lit gold when chosen.</summary>
    private Control Piece(string id)
    {
        var spec = GameItems.Spec(id)!;
        var chosen = _picked == id;

        var plate = new PanelContainer { CustomMinimumSize = new Vector2(84, 0), MouseFilter = Control.MouseFilterEnum.Stop };
        plate.AddThemeStyleboxOverride("panel", ShrinePanel.Box(chosen ? new Color(0.22f, 0.16f, 0.08f) : new Color(0.10f, 0.09f, 0.08f), chosen ? 2 : 1, 4, edge: chosen ? Gold : Edge));

        var cell = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        cell.AddThemeConstantOverride("separation", 2);
        plate.AddChild(cell);

        var view = new ItemView { CustomMinimumSize = new Vector2(Cell, Cell), MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        cell.AddChild(view);

        var shown = Preview(id, chosen ? _stat : null);
        view.Display(shown);

        var name = new Label
        {
            Text = GameItems.NameOf(shown),
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(76, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        name.AddThemeFontSizeOverride("font_size", 11);
        name.AddThemeColorOverride("font_color", chosen ? Gold : Soft);
        cell.AddChild(name);

        plate.MouseEntered += () => _tip.Say(ItemText.RichTooltip(Preview(id, _picked == id ? _stat : null), Worn(spec), PlayerProfile.Progression.Level));
        plate.MouseExited += () => _tip.Clear();
        plate.GuiInput += @event =>
        {
            if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;

            plate.AcceptEvent();

            if (_picked != id)
            {
                _picked = id;
                _stat = null;
            }

            Refresh();
        };

        return plate;
    }

    /// <summary>The stats the chosen piece may be born with, one plate each, when there is a choice.</summary>
    private void RefreshStats()
    {
        UiNodes.Clear(_stats);

        var innate = _picked is null ? null : GameItems.Spec(_picked)?.Innate;
        var choose = innate is { Stats.Count: > 1 };

        _statBlock.Visible = choose;

        if (choose)
        {
            var worth = innate!.Ladder.Count > 0 ? innate.Ladder[0] : innate.Magnitude;

            for (var i = 0; i < innate.Stats.Count; i++)
            {
                var index = i;
                var button = new Button { Text = innate.Stats[i].Describe(worth), CustomMinimumSize = new Vector2(0, 28), FocusMode = Control.FocusModeEnum.None };

                ShrinePanel.Plate(button);

                if (_stat == i)
                {
                    button.AddThemeStyleboxOverride("normal", ShrinePanel.Box(new Color(0.22f, 0.16f, 0.08f), 2, 6, edge: Gold));
                    button.AddThemeColorOverride("font_color", Gold);
                }

                button.Pressed += () =>
                {
                    _stat = index;
                    Refresh();
                };

                _stats.AddChild(button);
            }
        }

        var ready = _picked is not null && (!choose || _stat is not null);

        _take.Disabled = !ready;
        _choice.Text = _picked is null
            ? L10n.T("Click a piece to choose it.")
            : !ready
                ? L10n.T("Now choose the stat it is born with. It grows as the piece is upgraded.")
                : L10n.F("You will take: {0}", Describe());
    }

    private string Describe()
    {
        if (_picked is null) return "";

        var item = Preview(_picked, _stat);
        var spec = GameItems.Spec(_picked);
        var name = GameItems.NameOf(item);

        return spec is not null && item.InnateFor(spec) is { } innate && spec.Innate is { Stats.Count: > 1 }
            ? $"{name} ({innate.Describe()})"
            : name;
    }

    /// <summary>A copy to show, not to keep: the born stat chosen, if one is.</summary>
    private static ItemInstance Preview(string id, int? stat)
    {
        var item = new ItemInstance(0, id);

        if (stat is { } chosen) item.ChooseInnate(chosen);

        return item;
    }

    private ItemInstance? Worn(ItemSpec spec) =>
        spec.Slot is { } slot && GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<PlayerInventory>("PlayerInventory") is { } bag ? bag.Gear.In(slot) : null;

    /// <summary>Chooses a piece, and its stat (-1 for none), as the clicks do: for scripted checks.</summary>
    public void Choose(string id, int stat = -1)
    {
        _picked = id;
        _stat = stat >= 0 ? stat : null;
        Refresh();
    }

    // ------------------------------------------------------------------ take

    private void AskTake()
    {
        if (_picked is null) return;

        var dialog = new ConfirmationDialog
        {
            Title = L10n.T("Abyss chest"),
            DialogText = L10n.F("Take {0}? The other pieces stay in the chest for your next run.", Describe()),
            OkButtonText = L10n.T("Take"),
            CancelButtonText = L10n.T("Cancel"),
            Theme = Menus.MenuStyle.Theme(),
        };

        dialog.Confirmed += () =>
        {
            Take();
            dialog.QueueFree();
        };

        dialog.Canceled += dialog.QueueFree;

        AddChild(dialog);
        dialog.PopupCentered();
    }

    /// <summary>Takes the chosen piece: what the dialog's Take does, callable by itself for scripted checks.</summary>
    public void Take()
    {
        if (_picked is null || _chest is null || !IsInstanceValid(_chest) || _chest.Spent) return;

        var item = GameItems.Factory.Create(_picked, GameItems.LootRng);

        if (_stat is { } stat) item.ChooseInnate(stat);

        var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
        var bag = player?.GetNodeOrNull<PlayerInventory>("PlayerInventory");

        // A full bag puts it on the ground at the player's feet rather than losing it.
        if (bag is null || !bag.TryPickUp(item))
        {
            LootDrop.Place(_chest.GetParent(), item, player?.GlobalPosition ?? _chest.GlobalPosition);
        }

        Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndRareDrop, _chest.GlobalPosition);
        GD.Print($"[tower] took {item.DefId} from the abyss chest" + (_stat is { } s ? $", stat {s}" : ""));

        _chest.Empty();
        Close();
    }
}
