using System.Linq;
using Godot;

namespace Kiln.Game.Visual;

/// <summary>
/// Drives an imported character's animations from what its body is already doing.
/// </summary>
/// <remarks>
/// Nothing calls this to say "now walk". It reads the velocity of the body it hangs under,
/// which is the same body the animation is supposed to be describing — so a creature that
/// moves is animated as moving whether it was told to move by a behaviour tree, a click, a
/// key or a shove, and no new state can be forgotten at a call site. The one thing it is
/// told about is the attack, because a swing is an event rather than a state.
/// <para>
/// Falls silent rather than failing when a model has no animations. The kit's props go
/// through the same loader as its characters.
/// </para>
/// </remarks>
public partial class ModelAnimator : Node
{
    /// <summary>Metres per second above which the character is animated as running.</summary>
    private const float MovingAbove = 0.35f;

    /// <summary>
    /// The one-handed melee swings, cycled in order.
    /// </summary>
    /// <remarks>
    /// Four of them, and the player's basic attack is a four-swing chain (CBT-17) — so
    /// cycling here lines the animation up with the chain for free, without the chain having
    /// to reach through the combat code to say which swing it is on. For a creature with one
    /// attack it simply means the same blow does not play identically twice in a row.
    /// </remarks>
    private static readonly string[] Swings =
    [
        "1H_Melee_Attack_Chop",
        "1H_Melee_Attack_Slice_Horizontal",
        "1H_Melee_Attack_Slice_Diagonal",
        "1H_Melee_Attack_Stab",
    ];

    private AnimationPlayer? _player;
    private CharacterBody3D? _body;
    private string _idle = "";
    private string _run = "";
    private int _swing;
    private double _attackFor;

    /// <summary>True when this model had animations to drive at all.</summary>
    public bool Ready => _player is not null;

    public void Adopt(Node3D model, Node3D owner)
    {
        _player = model.FindChildren("*", "AnimationPlayer", true, false)
            .OfType<AnimationPlayer>().FirstOrDefault();

        if (_player is null) return;

        _body = owner.GetParentOrNull<CharacterBody3D>();

        var names = _player.GetAnimationList();

        _idle = Pick(names, "Idle");
        _run = Pick(names, "Running_A", "Running_B", "Walking_A", "Idle");

        // Looping is a property of the imported clip, and glTF does not carry one. Without
        // this every animation plays once and the character freezes on its last frame.
        foreach (var name in new[] { _idle, _run })
        {
            if (name.Length > 0) _player.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
        }

        Play(_idle);
    }

    private static string Pick(string[] names, params string[] wanted)
    {
        foreach (var want in wanted)
        {
            if (names.Contains(want)) return want;
        }

        return "";
    }

    /// <summary>Plays the next swing. Returns how long it will hold the body for.</summary>
    public double Attack()
    {
        if (_player is null) return 0;

        var names = _player.GetAnimationList();
        var clip = "";

        // Round-robin from where we left off, skipping any this model happens not to carry.
        for (var i = 0; i < Swings.Length && clip.Length == 0; i++)
        {
            var candidate = Swings[(_swing + i) % Swings.Length];

            if (names.Contains(candidate)) clip = candidate;
        }

        _swing = (_swing + 1) % Swings.Length;

        if (clip.Length == 0) return 0;

        _player.Play(clip);
        _attackFor = _player.GetAnimation(clip).Length;

        return _attackFor;
    }

    public override void _Process(double delta)
    {
        if (_player is null) return;

        // An attack owns the body until it finishes. Cutting back to a run mid-swing is what
        // makes an attack read as a twitch rather than a blow.
        if (_attackFor > 0)
        {
            _attackFor -= delta;
            return;
        }

        var speed = _body is null ? 0f : (_body.Velocity with { Y = 0 }).Length();

        Play(speed > MovingAbove ? _run : _idle);
    }

    private void Play(string name)
    {
        if (name.Length == 0 || _player is null || _player.CurrentAnimation == name) return;

        _player.Play(name);
    }
}
