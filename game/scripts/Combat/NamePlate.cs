using Godot;

namespace Kiln.Game.Combat;

/// <summary>How loudly a nameplate announces what it is attached to.</summary>
public enum NameRank
{
    /// <summary>Ordinary trash. Present but quiet.</summary>
    Normal,

    /// <summary>Something the player is meant to single out — the anchor add, a role specialist.</summary>
    Elite,

    /// <summary>A shard or a boss. Read from anywhere in the arena.</summary>
    Boss,
}

/// <summary>
/// The name floating above a combatant.
/// </summary>
/// <remarks>
/// Necessary once waves are composed by role: "kill the mender first" is not a decision the
/// player can make about five identical capsules. The role marker already carries shape and
/// colour; the name carries which creature it actually is, which is what the drop tables and
/// the vs-family gear bonuses are keyed to.
/// <para>
/// Rank drives size rather than a separate widget, so a boss reads across the arena and a
/// wolf does not compete with it. Like the damage numbers, these scale with distance —
/// <c>FixedSize</c> would hold the same screen size at every zoom and fill the view with
/// names the moment the camera pulls back.
/// </para>
/// </remarks>
public partial class NamePlate : Node3D
{
    private Label3D _label = null!;
    private Label3D _prefix = null!;

    /// <summary>A short lead-in drawn before the name in its own colour — a creature's level.</summary>
    public string Prefix { get; private set; } = "";

    /// <summary>The prefix's colour: green, so the level reads apart from the name.</summary>
    public Color PrefixColour { get; set; } = new("6fe36f");

    [Export] public Vector3 Offset { get; set; } = new(0, 2.3f, 0);

    private NameRank _rank = NameRank.Normal;
    private Color? _tint;

    public string Text { get; private set; } = "";

    /// <summary>
    /// Settable at any time, before or after the node is in the tree. A shard rolls its
    /// modifier and an encounter promotes its anchor after construction, so a plate that only
    /// read these on ready would silently keep its defaults.
    /// </summary>
    public NameRank Rank
    {
        get => _rank;
        set
        {
            _rank = value;
            Restyle();
        }
    }

    /// <summary>Overrides the rank's default colour — used to match a role marker.</summary>
    public Color? Tint
    {
        get => _tint;
        set
        {
            _tint = value;
            Restyle();
        }
    }

    /// <summary>Beyond this the plate fades out, so a distant field is not a wall of text.</summary>
    [Export] public float FadeDistance { get; set; } = 34f;

    public override void _Ready()
    {
        _label = new Label3D
        {
            Text = Text,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            PixelSize = 0.0045f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };

        _prefix = new Label3D
        {
            Text = Prefix,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            PixelSize = 0.0045f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };

        AddChild(_label);
        AddChild(_prefix);
        Restyle();

        // Owns its own global transform so the parent's facing does not swing the name around
        // the body — the same reason the health bar takes the camera basis directly.
        TopLevel = true;
    }

    private static (int Size, Color Colour, int Outline) Style(NameRank rank) => rank switch
    {
        NameRank.Boss => (52, new Color("d9a7ff"), 14),
        NameRank.Elite => (36, new Color("ffd24f"), 11),
        _ => (30, new Color("cfd4da"), 9),
    };

    private void Restyle()
    {
        if (_label is null) return;

        var (size, colour, outline) = Style(_rank);

        _label.FontSize = size;
        _label.OutlineSize = outline;
        _label.Modulate = _tint ?? colour;

        _prefix.FontSize = size;
        _prefix.OutlineSize = outline;
        _prefix.Modulate = PrefixColour;

        // Every name draws over what is in front of it (2026-09-24): a name hidden behind a
        // body or a tree is a name the player cannot read, and that was what the plates did.
        _label.NoDepthTest = true;
        _prefix.NoDepthTest = true;
    }

    public void SetText(string text) => SetText(text, "");

    /// <summary>The name, and a lead-in drawn before it in <see cref="PrefixColour"/>.</summary>
    public void SetText(string text, string prefix)
    {
        Text = text;
        Prefix = prefix;

        if (_label is null) return;

        _label.Text = text;
        _prefix.Text = prefix;
    }

    /// <summary>
    /// Lays the prefix and the name side by side, centred together, along the camera's right.
    /// </summary>
    /// <remarks>
    /// Two labels because a Label3D has one colour. Both are billboards, so "side by side" has
    /// to be the camera's side, recomputed as the camera turns, or the pair would swing apart.
    /// </remarks>
    private void Arrange(Camera3D camera)
    {
        if (Prefix.Length == 0)
        {
            _prefix.Visible = false;
            _label.Position = Vector3.Zero;
            return;
        }

        var font = _label.Font ?? ThemeDB.FallbackFont;
        var name = font.GetStringSize(_label.Text, HorizontalAlignment.Left, -1, _label.FontSize).X * _label.PixelSize;
        var lead = font.GetStringSize(Prefix + " ", HorizontalAlignment.Left, -1, _prefix.FontSize).X * _prefix.PixelSize;
        var total = name + lead;
        var right = camera.GlobalBasis.X.Normalized();

        _prefix.Position = right * (-(total / 2) + (lead / 2));
        _label.Position = right * ((total / 2) - (name / 2));
    }

    public override void _Process(double delta)
    {
        var parent = GetParent<Node3D>();
        var camera = GetViewport().GetCamera3D();

        if (parent is null || camera is null) return;

        GlobalPosition = parent.GlobalPosition + Offset;
        Arrange(camera);

        // Trash fades with distance; a boss never does. Fading the thing the fight is about
        // would defeat the point of giving it a larger plate in the first place.
        if (Rank == NameRank.Boss) return;

        var distance = camera.GlobalPosition.DistanceTo(GlobalPosition);
        var alpha = Mathf.Clamp(1f - ((distance - (FadeDistance * 0.6f)) / (FadeDistance * 0.4f)), 0f, 1f);

        _label.Modulate = _label.Modulate with { A = alpha };
        _label.Visible = alpha > 0.02f;
        _prefix.Modulate = _prefix.Modulate with { A = alpha };
        _prefix.Visible = _label.Visible && Prefix.Length > 0;
    }
}
