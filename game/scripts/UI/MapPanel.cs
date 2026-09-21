using Godot;
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
            UiState.SetOpen(ref _counted, Visible);

            if (Visible) Refresh();

            GetViewport().SetInputAsHandled();
            return;
        }

        if (Visible && @event.IsActionPressed(GameActions.Cancel))
        {
            Visible = false;
            UiState.SetOpen(ref _counted, false);
            GetViewport().SetInputAsHandled();
        }
    }

    // A panel freed while open would leave the modal count raised and the player unable to move.
    public override void _ExitTree() => UiState.SetOpen(ref _counted, false);

    public override void _Process(double delta)
    {
        // Every frame while open: the quest marker pulses, and a quarter-second redraw both
        // stuttered it and left a finished quest's markers up for a beat after it changed.
        if (Visible) _view.QueueRedraw();

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
            : $"{Items.GameItems.Localise(zone.Name)}   ·   level {zone.Band.Min}–{zone.Band.Max}";

        _view.QueueRedraw();
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

        root.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.11f, 0.13f, 0.96f),
            BorderColor = new Color(0.3f, 0.33f, 0.38f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 12,
            ContentMarginBottom = 12,
        });

        AddChild(root);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        root.AddChild(column);

        _title = new Label { Text = "—" };
        _title.AddThemeColorOverride("font_color", new Color(0.88f, 0.9f, 0.93f));
        column.AddChild(_title);

        _view = new MapView
        {
            CustomMinimumSize = new Vector2(760, 560),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        column.AddChild(_view);
        column.AddChild(Legend());
    }

    private static Control Legend()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 18);

        row.AddChild(Key("you", new Color(0.98f, 0.86f, 0.42f)));
        row.AddChild(Key("border", new Color(0.62f, 0.82f, 1f)));
        row.AddChild(Key("shrine", new Color(0.45f, 0.82f, 0.86f)));
        row.AddChild(Key("shard", new Color(0.82f, 0.52f, 0.95f)));
        row.AddChild(Key("camp", new Color(0.85f, 0.38f, 0.34f)));
        row.AddChild(Key("safe", new Color(0.36f, 0.72f, 0.42f)));

        var hint = new Label { Text = "M or Esc to close" };
        hint.AddThemeColorOverride("font_color", new Color(0.48f, 0.53f, 0.58f));
        row.AddChild(hint);

        return row;
    }

    private static Control Key(string text, Color tint)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", tint);

        return label;
    }
}
