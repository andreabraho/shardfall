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

    private Label? _label;
    private double _accum;
    private double _worstFrameMs;

    /// <summary>
    /// Registers a line in the overlay. Call from any system's _Ready:
    /// <code>DebugOverlay.Register("combat", () => $"targets={_targets.Count}");</code>
    /// </summary>
    public static void Register(string key, System.Func<string> provider) => Providers[key] = provider;

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

        _label = new Label
        {
            Text = "…",
            MouseFilter = Control.MouseFilterEnum.Ignore,
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

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"FPS {Engine.GetFramesPerSecond():F0}   frame {frameMs:F1} ms   worst {_worstFrameMs:F1} ms");
        sb.AppendLine($"nodes {GetTree().GetNodeCount()}   objects {Performance.GetMonitor(Performance.Monitor.ObjectCount):F0}");
        sb.AppendLine($"draw calls {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):F0}");

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
                value = $"<error: {ex.GetType().Name}>";
            }

            sb.AppendLine($"{key}: {value}");
        }

        _label!.Text = sb.ToString().TrimEnd();
    }

    /// <summary>Resets the worst-frame tracker — call when starting a perf measurement.</summary>
    public void ResetWorstFrame() => _worstFrameMs = 0;
}
