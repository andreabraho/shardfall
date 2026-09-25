using Godot;
using Kiln.Game.Settings;

namespace Kiln.Game.Audio;

/// <summary>
/// The sound of a place under the music (REF-20): wind, birds, a cave's drip, heard all over
/// the map and never seeming to start again.
/// </summary>
/// <remarks>
/// A recording played on a loop gives itself away where its end meets its start — an MP3
/// most of all, which pads both ends with a sliver of silence. So there are two players, and
/// a few seconds before one reaches its end the other starts the same recording from the top
/// and the two cross over. Changing map crosses over to the new place's recording the same way.
/// <para>
/// The crossing keeps the power constant — one rises along a sine as the other falls along a
/// cosine — so it does not dip in the middle, which two fades in decibels did.
/// </para>
/// </remarks>
public partial class AmbienceBed : Node
{
    /// <summary>How long the end of one pass and the start of the next play together.</summary>
    private const double Overlap = 4.0;

    /// <summary>How long a change of place takes to cross over.</summary>
    private const double ChangeSeconds = 2.5;

    private const float Silent = -60f;

    private readonly AudioStreamPlayer[] _players = new AudioStreamPlayer[2];
    private int _current;
    private float _volume = Silent;

    /// <summary>The crossing under way: how far through it, and how long it takes. Done at 1.</summary>
    private double _crossing = 1;
    private double _crossingSeconds = 1;

    /// <summary>The level the outgoing player had when the crossing began.</summary>
    private float _outgoingFrom = Silent;

    /// <summary>The sound id playing, or empty for none.</summary>
    public string Id { get; private set; } = "";

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;

        for (var i = 0; i < _players.Length; i++)
        {
            _players[i] = new AudioStreamPlayer { Name = $"Bed{i}", Bus = GameSettings.EffectsBus, VolumeDb = Silent };
            AddChild(_players[i]);
        }
    }

    /// <summary>Crosses over to <paramref name="stream"/>, or to silence when it is null.</summary>
    public void Change(string id, AudioStream? stream, float volumeDb)
    {
        if (id == Id) return;

        Id = id;
        _volume = volumeDb;

        BeginCrossing(stream, ChangeSeconds);
    }

    public override void _Process(double delta)
    {
        var incoming = _players[_current];
        var outgoing = _players[1 - _current];

        if (_crossing < 1)
        {
            _crossing = System.Math.Min(1, _crossing + (delta / _crossingSeconds));

            var angle = _crossing * Mathf.Pi / 2;

            if (incoming.Playing) incoming.VolumeDb = Level(_volume, System.Math.Sin(angle));
            outgoing.VolumeDb = Level(_outgoingFrom, System.Math.Cos(angle));

            if (_crossing >= 1) outgoing.Stop();

            return;
        }

        if (!incoming.Playing || incoming.Stream is null) return;

        var length = incoming.Stream.GetLength();

        // The next pass begins under the end of this one.
        if (length > Overlap * 2 && incoming.GetPlaybackPosition() >= length - Overlap) BeginCrossing(incoming.Stream, Overlap);
    }

    private void BeginCrossing(AudioStream? stream, double seconds)
    {
        var outgoing = _players[_current];

        // A crossing cut short by another: the quieter of the two is dropped now.
        _players[1 - _current].Stop();

        _current = 1 - _current;

        var incoming = _players[_current];

        _outgoingFrom = outgoing.Playing ? outgoing.VolumeDb : Silent;
        _crossing = 0;
        _crossingSeconds = seconds;

        if (stream is null)
        {
            incoming.Stop();
            return;
        }

        incoming.Stream = stream;
        incoming.VolumeDb = Silent;
        incoming.Play();
    }

    /// <summary><paramref name="full"/> decibels scaled by an amplitude between 0 and 1.</summary>
    private static float Level(float full, double amplitude) =>
        amplitude <= 0.001 ? Silent : Mathf.Max(Silent, full + Mathf.LinearToDb((float)amplitude));
}
