using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Kiln.Data.Definitions;
using Kiln.Game.Settings;

namespace Kiln.Game.Audio;

/// <summary>
/// Every sound and every piece of music goes through here (AUD-01).
/// </summary>
/// <remarks>
/// Gameplay says what happened — <c>Sfx.Play(Ids.Sounds.SndHit, where)</c> — and
/// <c>data/tables/sounds.json</c> says what that sounds like. The split is the same one visuals
/// use: when the sounds arrive (AUD-02) they are a data edit, and a sound with no files yet is
/// silence, not an error. The F3 overlay shows what would have played, so the hooks can be
/// checked before there is anything to hear.
/// <para>
/// Lives on the autoload so music carries across a border and crossfades instead of cutting.
/// Positional sounds are placed in the current scene, because that is where the listener is.
/// </para>
/// </remarks>
public partial class AudioDirector : Node
{
    private const double CrossfadeSeconds = 1.5;
    private const float Silent = -60f;

    private static AudioDirector? _instance;

    private readonly Dictionary<string, AudioStream?[]> _streams = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Node>> _voices = new(StringComparer.Ordinal);
    private readonly Random _random = new();

    private AudioStreamPlayer _musicA = null!;
    private AudioStreamPlayer _musicB = null!;
    private string _musicId = "";
    private string _lastSound = "—";
    private Tween? _fade;

    public override void _Ready()
    {
        _instance = this;
        ProcessMode = ProcessModeEnum.Always;

        _musicA = MusicPlayer("MusicA");
        _musicB = MusicPlayer("MusicB");

        // Every button in the game clicks, without every panel having to remember to.
        GetTree().NodeAdded += OnNodeAdded;

        Debug.DebugOverlay.Register("audio", this, () =>
            $"{_lastSound} · music {(_musicId.Length == 0 ? "—" : _musicId)}{(HasFiles(_musicId) ? "" : " (no file)")}");
    }

    public override void _ExitTree() => GetTree().NodeAdded -= OnNodeAdded;

    private AudioStreamPlayer MusicPlayer(string name)
    {
        var player = new AudioStreamPlayer { Name = name, Bus = GameSettings.MusicBus, VolumeDb = Silent };

        // Loops by replaying. The loop flag lives on the imported stream and differs between
        // formats; this works for all of them.
        player.Finished += () =>
        {
            if (player.VolumeDb > Silent + 1) player.Play();
        };

        AddChild(player);

        return player;
    }

    private void OnNodeAdded(Node node)
    {
        if (node is BaseButton button) button.Pressed += () => Play(Kiln.Data.Ids.Sounds.SndUiClick);
    }

    // ------------------------------------------------------------------ effects

    /// <summary>Plays a sound, flat or — when the sound is positional and a place is given — from there.</summary>
    public static void Play(string id, Vector3? at = null) => _instance?.PlayInternal(id, at);

    private void PlayInternal(string id, Vector3? at)
    {
        if (!GameContent.IsLoaded || !GameContent.Database.Sounds.TryGetValue(id, out var def)) return;

        _lastSound = id;

        var stream = Pick(def);

        if (stream is null) return;

        var voices = _voices.TryGetValue(id, out var list) ? list : _voices[id] = [];

        voices.RemoveAll(v => !IsInstanceValid(v));

        // Over the cap: the oldest copy gives way, so a burst of hits stays a burst and not a wall.
        while (voices.Count >= Math.Max(1, def.MaxVoices))
        {
            // Stopped now, not at the end of the frame: a freed-later voice is still a voice.
            if (voices[0] is AudioStreamPlayer flat) flat.Stop();
            if (voices[0] is AudioStreamPlayer3D placed) placed.Stop();

            voices[0].QueueFree();
            voices.RemoveAt(0);
        }

        var pitch = 1f + (float)((_random.NextDouble() * 2 - 1) * def.PitchJitter);
        var bus = def.Bus == "music" ? GameSettings.MusicBus : GameSettings.EffectsBus;
        Node voice;

        if (def.Positional && at is { } where && GetTree().CurrentScene is Node3D scene)
        {
            var player = new AudioStreamPlayer3D
            {
                Stream = stream,
                Bus = bus,
                VolumeDb = (float)def.VolumeDb,
                PitchScale = pitch,
                MaxDistance = (float)def.MaxDistance,
                UnitSize = 6f,
            };

            scene.AddChild(player);
            player.GlobalPosition = where;
            player.Finished += player.QueueFree;
            player.Play();
            voice = player;
        }
        else
        {
            var player = new AudioStreamPlayer { Stream = stream, Bus = bus, VolumeDb = (float)def.VolumeDb, PitchScale = pitch };

            AddChild(player);
            player.Finished += player.QueueFree;
            player.Play();
            voice = player;
        }

        voices.Add(voice);
    }

    /// <summary>One of the sound's files at random, or null when it has none that load.</summary>
    private AudioStream? Pick(SoundDef def)
    {
        if (!_streams.TryGetValue(def.Id, out var loaded))
        {
            loaded = def.Files.Select(Load).ToArray();
            _streams[def.Id] = loaded;
        }

        var usable = loaded.Where(s => s is not null).ToArray();

        return usable.Length == 0 ? null : usable[_random.Next(usable.Length)];
    }

    private static AudioStream? Load(string path)
    {
        if (!ResourceLoader.Exists(path))
        {
            GD.PushWarning($"[audio] {path} is listed but not there; that variant stays silent.");
            return null;
        }

        return ResourceLoader.Load<AudioStream>(path);
    }

    private static bool HasFiles(string id) =>
        id.Length > 0 && GameContent.IsLoaded && GameContent.Database.Sounds.TryGetValue(id, out var def) && def.Files.Length > 0;

    // ------------------------------------------------------------------ music

    /// <summary>
    /// Changes the music, crossfading from whatever is playing. The same track again is left
    /// alone, so walking between two maps that share one does not restart it.
    /// </summary>
    public static void Music(string id) => _instance?.MusicInternal(id ?? "");

    private void MusicInternal(string id)
    {
        if (id == _musicId) return;

        GD.Print($"[audio] music {(_musicId.Length == 0 ? "—" : _musicId)} → {(id.Length == 0 ? "—" : id)}");
        _musicId = id;

        // The louder of the two is the one to fade out. Not "the one playing": a short loop can be
        // between its end and its replay at exactly this moment, and then the wrong one fades.
        var outgoing = _musicA.VolumeDb >= _musicB.VolumeDb ? _musicA : _musicB;
        var incoming = outgoing == _musicA ? _musicB : _musicA;
        var def = GameContent.IsLoaded && GameContent.Database.Sounds.TryGetValue(id, out var found) ? found : null;
        var stream = def is null ? null : Pick(def);

        _fade?.Kill();
        _fade = CreateTween().SetParallel();
        _fade.TweenProperty(outgoing, "volume_db", Silent, CrossfadeSeconds);

        if (stream is not null)
        {
            incoming.Stream = stream;
            incoming.VolumeDb = Silent;
            incoming.Play();
            _fade.TweenProperty(incoming, "volume_db", (float)def!.VolumeDb, CrossfadeSeconds);
        }

        _fade.Chain().TweenCallback(Callable.From(() =>
        {
            if (outgoing.VolumeDb <= Silent + 1) outgoing.Stop();
        }));
    }
}
