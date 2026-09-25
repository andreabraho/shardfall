using Godot;
using Kiln.Core.Foundation;
using Kiln.Game.Input;
using Kiln.Game.World;

namespace Kiln.Game.UI;

/// <summary>
/// The zone map, on M (WLD-08).
/// </summary>
/// <remarks>
/// Two jobs, and the second is the one that makes it worth having. It answers "where am I and
/// which way out" — but it also remembers where you have been, which is what turns a greybox
/// field into somewhere with a near side and a far side. A map that showed the whole zone the
/// moment you arrived would hand over every camp and every shrine for free, and the finding
/// is most of what a field map has to offer.
/// <para>
/// The remembering runs whether the map is open or not: you explore with it shut, and a
/// panel that only recorded what happened while it was being looked at would reward keeping
/// it open. It is the walking that reveals ground, not the reading.
/// </para>
/// </remarks>
public partial class MapPanel : CanvasLayer
{
    /// <summary>How often the character's surroundings are written down, in seconds.</summary>
    /// <remarks>
    /// Four times a second rather than every frame. At a walk that is under a metre of
    /// travel between samples, far finer than the six-metre cells being filled in, so the
    /// trail is continuous and the work is a twentieth of what it would otherwise be.
    /// </remarks>
    private const double SampleInterval = 0.25;

    private MapView _view = null!;
    private Control _root = null!;
    private Label _title = null!;
    private Node3D? _player;
    private double _since;
    private bool _counted;

    public override void _Ready()
    {
        Layer = 24;
        Build();
        Visible = false;
        CallDeferred(nameof(Bind));
    }

    private void Bind() => _player = GetTree().GetFirstNodeInGroup("player") as Node3D;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(GameActions.ToggleMap))
        {
            Visible = !Visible;
            UiState.SetSide(ref _counted, _root, Visible);
            if (Visible) Refresh();

            GetViewport().SetInputAsHandled();
            return;
        }

        if (Visible && @event.IsActionPressed(GameActions.Cancel))
        {
            Visible = false;
            UiState.SetSide(ref _counted, _root, false);
            GetViewport().SetInputAsHandled();
        }
    }

    // A panel freed while open would leave the modal count raised and the player unable to move.
    public override void _ExitTree() => UiState.SetSide(ref _counted, _root, false);

    public override void _Process(double delta)
    {
        // Every frame while open: the quest marker pulses, and a quarter-second redraw both
        // stuttered it and left a finished quest's markers up for a beat after it changed.
        if (Visible)
        {
            _view.QueueRedraw();

            // Where the player stands, in metres, as the original's map writes it.
            if (_player is not null && GodotObject.IsInstanceValid(_player))
            {
                _where.Text = L10n.F("Your position: {0:0}, {1:0}", _player.GlobalPosition.X, _player.GlobalPosition.Z);
            }
        }

        _since += delta;

        if (_since < SampleInterval) return;

        _since = 0;

        Remember();
    }

    /// <summary>Writes down the ground around the character.</summary>
    private void Remember()
    {
        if (!GameWorld.IsLoaded || _player is null || !GodotObject.IsInstanceValid(_player)) return;

        var zone = GameWorld.CurrentZoneId;

        if (zone.Length == 0) return;

        if (!PlayerProfile.Explored.TryGetValue(zone, out var seen))
        {
            seen = [];
            PlayerProfile.Explored[zone] = seen;
        }

        var at = _player.GlobalPosition;
        var reach = Mathf.CeilToInt(MapView.RevealRadius / MapView.CellSize);
        var cx = Mathf.FloorToInt(at.X / MapView.CellSize);
        var cz = Mathf.FloorToInt(at.Z / MapView.CellSize);

        for (var dx = -reach; dx <= reach; dx++)
        {
            for (var dz = -reach; dz <= reach; dz++)
            {
                // A disc rather than the square it is cut from, measured to the cell's centre.
                // A square would reveal the corners of a region the character never saw, and
                // on a map made of straight walls that reads as a mistake.
                var centre = new Vector2((cx + dx + 0.5f) * MapView.CellSize,
                    (cz + dz + 0.5f) * MapView.CellSize);

                if (centre.DistanceTo(new Vector2(at.X, at.Z)) > MapView.RevealRadius) continue;

                seen.Add(MapView.Key(cx + dx, cz + dz));
            }
        }
    }

    private void Refresh()
    {
        var zone = GameWorld.CurrentZone;

        _title.Text = zone is null
            ? "—"
            : L10n.F("{0}   ·   level {1}–{2}", Items.GameItems.Localise(zone.Name), zone.Band.Min, zone.Band.Max);

        _view.QueueRedraw();
    }

    // ------------------------------------------------------------------ build

    private static readonly Color Gold = new(0.96f, 0.86f, 0.58f);
    private static readonly Color Frame = new(0.62f, 0.50f, 0.28f);

    private Label _where = null!;

    /// <summary>
    /// The window in the original's style (REF-19): a gold title bar with the map's name and
    /// its levels, the map in an inner frame, and the legend on a dark strip under it with
    /// where the player stands.
    /// </summary>
    private void Build()
    {
        // At the left edge and smaller than it was (REF-07): a map kept open while fighting
        // has to leave the middle of the screen to the fight.
        var root = _root = new PanelContainer
        {
            AnchorLeft = 0f,
            AnchorTop = 0.5f,
            AnchorRight = 0f,
            AnchorBottom = 0.5f,
            OffsetLeft = 16,
            GrowHorizontal = Control.GrowDirection.End,
            GrowVertical = Control.GrowDirection.Both,
        };

        // Moved by holding its border or title and dragging (REF-19).
        PanelMover.Attach(root, "map");

        root.AddThemeStyleboxOverride("panel", Box(new Color(0.06f, 0.055f, 0.05f, 0.96f), 2, 0));
        AddChild(root);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 0);
        root.AddChild(column);

        // Title bar.
        var bar = new PanelContainer();
        bar.AddThemeStyleboxOverride("panel", Box(new Color(0.20f, 0.14f, 0.07f), 0, 8, bottom: 1));
        column.AddChild(bar);

        var barRow = new HBoxContainer();
        bar.AddChild(barRow);

        _title = new Label { Text = "—", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _title.AddThemeFontSizeOverride("font_size", 15);
        _title.AddThemeColorOverride("font_color", Gold);
        barRow.AddChild(_title);

        var cross = new Button { Text = "✕", Flat = true, FocusMode = Control.FocusModeEnum.None };
        cross.AddThemeFontSizeOverride("font_size", 13);
        cross.Pressed += Close;
        barRow.AddChild(cross);

        // The map, in a frame of its own.
        var body = new MarginContainer();
        body.AddThemeConstantOverride("margin_left", 10);
        body.AddThemeConstantOverride("margin_right", 10);
        body.AddThemeConstantOverride("margin_top", 10);
        body.AddThemeConstantOverride("margin_bottom", 8);
        column.AddChild(body);

        var inset = new PanelContainer();
        var frame = Box(new Color(0.07f, 0.075f, 0.09f), 1, 0, edge: Frame);
        frame.ContentMarginLeft = frame.ContentMarginRight = frame.ContentMarginTop = frame.ContentMarginBottom = 1;
        inset.AddThemeStyleboxOverride("panel", frame);
        body.AddChild(inset);

        // Clipped to its frame: a caption near the edge is cut rather than drawn over the border.
        _view = new MapView
        {
            CustomMinimumSize = new Vector2(540, 400),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipContents = true,
        };

        inset.AddChild(_view);

        // The legend, on a dark strip.
        var foot = new PanelContainer();
        foot.AddThemeStyleboxOverride("panel", Box(new Color(0.10f, 0.08f, 0.06f), 0, 8, top: 1));
        column.AddChild(foot);

        var footColumn = new VBoxContainer();
        footColumn.AddThemeConstantOverride("separation", 2);
        foot.AddChild(footColumn);

        footColumn.AddChild(Legend());

        _where = new Label();
        _where.AddThemeFontSizeOverride("font_size", 11);
        _where.AddThemeColorOverride("font_color", new Color(0.62f, 0.58f, 0.50f));
        footColumn.AddChild(_where);
    }

    private void Close()
    {
        Visible = false;
        UiState.SetSide(ref _counted, _root, false);
    }

    private static Control Legend()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);

        row.AddChild(Key(L10n.T("you"), new Color(0.98f, 0.86f, 0.42f)));
        row.AddChild(Key(L10n.T("border"), new Color(0.62f, 0.82f, 1f)));
        row.AddChild(Key(L10n.T("shrine"), new Color(0.45f, 0.82f, 0.86f)));
        row.AddChild(Key(L10n.T("shard"), new Color(0.82f, 0.52f, 0.95f)));
        row.AddChild(Key(L10n.T("camp"), new Color(0.85f, 0.38f, 0.34f)));
        row.AddChild(Key(L10n.T("safe"), new Color(0.36f, 0.72f, 0.42f)));

        var hint = new Label { Text = L10n.T("M or Esc to close"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, HorizontalAlignment = HorizontalAlignment.Right };
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", new Color(0.55f, 0.52f, 0.46f));
        row.AddChild(hint);

        return row;
    }

    /// <summary>A coloured dot and its word, as the original's legend sets them.</summary>
    private static Control Key(string text, Color tint)
    {
        var item = new HBoxContainer();
        item.AddThemeConstantOverride("separation", 4);

        var dot = new Label { Text = "●", VerticalAlignment = VerticalAlignment.Center };
        dot.AddThemeFontSizeOverride("font_size", 11);
        dot.AddThemeColorOverride("font_color", tint);
        item.AddChild(dot);

        var label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", new Color(0.82f, 0.80f, 0.74f));
        item.AddChild(label);

        return item;
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
