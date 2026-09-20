using System.Linq;
using System.Collections.Generic;
using Godot;

namespace Kiln.Game.Debug;

/// <summary>
/// F3 overlay: FPS, frame time, entity counts and whatever systems register.
/// <para>
/// Built in Phase 0 on purpose. Every later phase — combat timing, AI states, shard phases,
/// drop rolls — needs somewhere to print live state, and retrofitting that once combat is
/// half-built is painful. Registered values cost nothing when the overlay is hidden.
/// </para>
/// </summary>
public partial class DebugOverlay : CanvasLayer
{
    private static readonly Dictionary<string, System.Func<string>> Providers = new();

    private RichTextLabel? _label;
    private double _accum;
    private double _worstFrameMs;

    /// <summary>
    /// Registers a line in the overlay. Call from any system's _Ready:
    /// <code>DebugOverlay.Register("combat", () => $"targets={_targets.Count}");</code>
    /// </summary>
    public static void Register(string key, System.Func<string> provider) => Providers[key] = provider;

    /// <summary>
    /// Registers a line owned by a scene node, dropped when that node goes.
    /// </summary>
    /// <remarks>
    /// The overlay is an autoload and outlives every scene. A line registered by a node in a
    /// zone scene keeps being asked for its value after a gate replaces that scene, and the
    /// closure reaches through a freed node — which is an error per line, per frame, forever.
    /// Passing the owner is what lets the overlay let go.
    /// </remarks>
    public static void Register(string key, Node owner, System.Func<string> provider)
    {
        Owners[key] = owner;
        Providers[key] = provider;
    }

    private static readonly Dictionary<string, Node> Owners = [];

    /// <summary>Forgets lines whose owning node has been freed or taken out of the tree.</summary>
    private static void DropOrphans()
    {
        foreach (var key in Owners.Keys.ToList())
        {
            if (GodotObject.IsInstanceValid(Owners[key]) && Owners[key].IsInsideTree()) continue;

            Owners.Remove(key);
            Providers.Remove(key);
        }
    }

    public static void Unregister(string key) => Providers.Remove(key);

    public override void _Ready()
    {
        Layer = 128;

        var panel = new PanelContainer
        {
            OffsetLeft = 8,
            OffsetTop = 8,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        // Rich text rather than a plain label: colouring the values is the whole point, and a
        // plain Label can only be tinted as a block.
        _label = new RichTextLabel
        {
            BbcodeEnabled = true,
            Text = "…",
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.Off,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(320, 0),
        };

        panel.AddChild(_label);
        AddChild(panel);

        // Visible by default in debug builds; never present in a release build.
        Visible = OS.IsDebugBuild();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("toggle_debug_overlay"))
        {
            Visible = !Visible;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        var frameMs = delta * 1000.0;
        if (frameMs > _worstFrameMs) _worstFrameMs = frameMs;

        if (!Visible) return;

        _accum += delta;
        if (_accum < 0.25) return; // 4 Hz is plenty and keeps the overlay itself cheap
        _accum = 0;

        var fps = Engine.GetFramesPerSecond();
        var sb = new System.Text.StringBuilder();

        // One line for the budget, one for the scene. Three separate lines of counters pushed
        // everything a system actually registered further down the screen, which is the part
        // worth reading — the frame rate is the part you glance at.
        sb.Append(Rate(fps)).Append(Dim(" fps  ")).Append(Frame(frameMs))
            .Append(Dim($"  peak {_worstFrameMs:F0}")).Append('\n');

        sb.Append(Dim("nodes ")).Append(GetTree().GetNodeCount())
            .Append(Dim("  obj ")).Append($"{Performance.GetMonitor(Performance.Monitor.ObjectCount):F0}")
            .Append(Dim("  draws ")).Append($"{Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):F0}")
            .Append('\n');

        DropOrphans();

        foreach (var (key, provider) in Providers)
        {
            string value;
            try
            {
                value = provider();
            }
            catch (System.Exception ex)
            {
                // A broken debug provider must never take down the game.
                value = $"[color=#e06c6c]<{ex.GetType().Name}>[/color]";
            }

            sb.Append(Dim(key + " ")).Append(value).Append('\n');
        }

        _label!.Text = sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Labels are dimmed so the numbers carry the line.
    /// </summary>
    /// <remarks>
    /// The overlay is read at a glance while doing something else. Every word at the same
    /// weight means every glance costs a read; dimming the words that never change turns it
    /// into a row of values with captions attached.
    /// </remarks>
    private static string Dim(string text) => $"[color=#7b8794]{text}[/color]";

    /// <summary>Frame rate, coloured against the sixty the game is built for.</summary>
    private static string Rate(double fps) => fps switch
    {
        >= 55 => $"[color=#7fc98a]{fps:F0}[/color]",
        >= 30 => $"[color=#e0b356]{fps:F0}[/color]",
        _ => $"[color=#e06c6c]{fps:F0}[/color]",
    };

    /// <summary>Frame time against the 16.7 ms budget, which is the same story told usefully.</summary>
    private static string Frame(double ms) => ms switch
    {
        <= 18 => $"[color=#7fc98a]{ms:F1} ms[/color]",
        <= 33 => $"[color=#e0b356]{ms:F1} ms[/color]",
        _ => $"[color=#e06c6c]{ms:F1} ms[/color]",
    };

    /// <summary>Resets the worst-frame tracker — call when starting a perf measurement.</summary>
    public void ResetWorstFrame() => _worstFrameMs = 0;
}
