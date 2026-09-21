using System.Linq;
using Godot;
using Kiln.Core.Foundation;

namespace Kiln.Game.Settings;

/// <summary>
/// The player's settings (UIX-02, FR-12.2 "settings"): kept in <c>user://settings.cfg</c>,
/// applied at boot and again whenever one changes.
/// </summary>
/// <remarks>
/// Separate from the save on purpose. Settings belong to the machine and the person at it,
/// not to a character: loading an old save must not also put the window back the way it was
/// the day that save was written.
/// <para>
/// <c>KILN_SETTINGS</c> points it elsewhere, for the same reason <c>KILN_SAVE_DIR</c> exists:
/// an automated run that toggles fullscreen must not leave the player's own game windowed.
/// </para>
/// </remarks>
public static class GameSettings
{
    public const string MusicBus = "Music";
    public const string EffectsBus = "Effects";

    private const string Section = "settings";

    public static bool Fullscreen { get; set; } = true;
    public static bool VSync { get; set; } = true;

    /// <summary>Frame cap; 0 for none.</summary>
    public static int MaxFps { get; set; }

    /// <summary>3D resolution as a fraction of the window's, 0.5–1. The UI is always sharp.</summary>
    public static float RenderScale { get; set; } = 1f;

    /// <summary>Volumes, 0–1.</summary>
    public static float MasterVolume { get; set; } = 0.8f;
    public static float MusicVolume { get; set; } = 0.7f;
    public static float EffectsVolume { get; set; } = 0.8f;

    /// <summary>Language code; a table in data/strings must exist for it.</summary>
    public static string Language { get; set; } = Kiln.Core.Foundation.L10n.English;

    public static readonly int[] FpsCaps = [0, 30, 60, 120, 144, 240];

    private static string Path =>
        OS.GetEnvironment("KILN_SETTINGS") is { Length: > 0 } path ? path : "user://settings.cfg";

    public static void Load()
    {
        var file = new ConfigFile();

        if (file.Load(Path) == Error.Ok)
        {
            Fullscreen = (bool)file.GetValue(Section, "fullscreen", Fullscreen);
            VSync = (bool)file.GetValue(Section, "vsync", VSync);
            MaxFps = (int)file.GetValue(Section, "max_fps", MaxFps);
            RenderScale = Mathf.Clamp((float)file.GetValue(Section, "render_scale", RenderScale), 0.5f, 1f);
            MasterVolume = Mathf.Clamp((float)file.GetValue(Section, "master_volume", MasterVolume), 0f, 1f);
            MusicVolume = Mathf.Clamp((float)file.GetValue(Section, "music_volume", MusicVolume), 0f, 1f);
            EffectsVolume = Mathf.Clamp((float)file.GetValue(Section, "effects_volume", EffectsVolume), 0f, 1f);
            Language = (string)file.GetValue(Section, "language", Language);
        }

        EnsureBuses();
        Apply();
    }

    public static void Save()
    {
        var file = new ConfigFile();

        file.SetValue(Section, "fullscreen", Fullscreen);
        file.SetValue(Section, "vsync", VSync);
        file.SetValue(Section, "max_fps", MaxFps);
        file.SetValue(Section, "render_scale", RenderScale);
        file.SetValue(Section, "master_volume", MasterVolume);
        file.SetValue(Section, "music_volume", MusicVolume);
        file.SetValue(Section, "effects_volume", EffectsVolume);
        file.SetValue(Section, "language", Language);

        if (file.Save(Path) != Error.Ok) GD.PushWarning($"[settings] could not write {Path}");
    }

    /// <summary>Makes the engine match the settings.</summary>
    public static void Apply()
    {
        var mode = DisplayServer.WindowGetMode();
        var isFull = mode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;

        if (isFull != Fullscreen) DisplaySettings.SetFullscreen(Fullscreen);

        DisplayServer.WindowSetVsyncMode(VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
        Engine.MaxFps = MaxFps;

        if (Engine.GetMainLoop() is SceneTree tree) tree.Root.Scaling3DScale = RenderScale;

        Volume("Master", MasterVolume);
        Volume(MusicBus, MusicVolume);
        Volume(EffectsBus, EffectsVolume);
    }

    /// <summary>
    /// Puts the chosen language's tables in place (UIX-06). Needs content loaded, so it runs
    /// after the rest of the settings; a language with no table falls back to English.
    /// </summary>
    public static void ApplyLanguage()
    {
        if (!GameContent.IsLoaded) return;

        var db = GameContent.Database;

        if (!db.Strings.ContainsKey(Language)) Language = Kiln.Core.Foundation.L10n.English;

        Kiln.Core.Foundation.L10n.Use(Language, db.StringsFor(Language), db.StringsFor(Kiln.Core.Foundation.L10n.English));
    }

    /// <summary>The languages there are tables for, English first.</summary>
    public static System.Collections.Generic.List<string> Languages() =>
        !GameContent.IsLoaded
            ? [Kiln.Core.Foundation.L10n.English]
            : [.. GameContent.Database.Strings.Keys.OrderBy(k => k == Kiln.Core.Foundation.L10n.English ? "" : k)];

    /// <summary>
    /// Adds the music and effects buses under the master one.
    /// </summary>
    /// <remarks>
    /// Created in code rather than a bus layout file so that the sliders work before any sound
    /// exists (AUD-01 wires the players to them). Idempotent.
    /// </remarks>
    private static void EnsureBuses()
    {
        foreach (var name in new[] { MusicBus, EffectsBus })
        {
            if (AudioServer.GetBusIndex(name) >= 0) continue;

            AudioServer.AddBus();

            var index = AudioServer.BusCount - 1;

            AudioServer.SetBusName(index, name);
            AudioServer.SetBusSend(index, "Master");
        }
    }

    private static void Volume(string bus, float fraction)
    {
        var index = AudioServer.GetBusIndex(bus);

        if (index < 0) return;

        AudioServer.SetBusMute(index, fraction <= 0.001f);
        AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(Mathf.Max(fraction, 0.001f)));
    }
}
