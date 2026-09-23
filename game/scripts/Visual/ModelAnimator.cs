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

        _idle = Find(names, Idle);
        _run = Find(names, Move);

        if (_run.Length == 0) _run = _idle;

        // Looping is a property of the imported clip, and glTF does not carry one. Without
        // this every animation plays once and the character freezes on its last frame.
        foreach (var name in new[] { _idle, _run })
        {
            if (name.Length > 0) _player.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
        }

        Play(_idle);
    }

    // What each role is called across the packs in use. KayKit names clips "Running_A";
    // Quaternius "Gallop", "Rat_Run" or "CharacterArmature|Run". The part after the last "|"
    // is compared, and the first list entry any clip matches wins, so the best-looking clip
    // for a role is listed first.
    private static readonly string[] Idle = ["Idle", "Rat_Idle", "Spider_Idle", "Snake_Idle", "Flying_Idle", "Idle_2", "Wasp_Flying"];

    private static readonly string[] Move =
        ["Running_A", "Running_B", "Run", "Gallop", "Rat_Run", "Fast_Flying", "Walking_A", "Walk",
         "Spider_Walk", "Snake_Walk", "Rat_Walk", "Wasp_Flying"];

    private static readonly string[] Strike =
        ["Attack", "Attack_Headbutt", "Attack_Kick", "Punch", "Bite_Front", "Headbutt",
         "Rat_Attack", "Spider_Attack", "Snake_Attack", "Wasp_Attack", "Weapon"];

    private static readonly string[] Hurt =
        ["Hit_A", "Hit_B", "HitReact", "HitRecieve", "Idle_HitReact_Left", "Idle_HitReact_Right"];

    /// <summary>The first clip playing <paramref name="role"/>, matched on the clip's own name.</summary>
    private static string Find(string[] names, string[] role)
    {
        foreach (var want in role)
        {
            foreach (var name in names)
            {
                var own = name[(name.LastIndexOf('|') + 1)..];

                if (string.Equals(own, want, System.StringComparison.OrdinalIgnoreCase)) return name;
            }
        }

        return "";
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

        // A creature from another pack has one attack under its own name.
        if (clip.Length == 0) clip = Find(names, Strike);

        if (clip.Length == 0) return 0;

        _player.Play(clip);
        _attackFor = _player.GetAnimation(clip).Length;

        return _attackFor;
    }

    /// <summary>
    /// The raised-weapon flourish a self-buff is cast with (REF-03). Falls back to a swing on
    /// a model that has no such clip, so the cast is never silent-looking.
    /// </summary>
    public double Flourish()
    {
        if (_player is null) return 0;

        var clip = Pick(_player.GetAnimationList(), "Cheer", "Spellcast_Raise", "Taunt", "Block");

        if (clip.Length == 0) return Attack();

        _player.Play(clip);
        _attackFor = System.Math.Min(_player.GetAnimation(clip).Length, 0.9);

        return _attackFor;
    }

    /// <summary>
    /// The two-handed spin, for Whirlwind (REF-03). Returns false on a model that has no such
    /// clip, so the caller can turn the body itself instead.
    /// </summary>
    public bool Spin(double seconds)
    {
        if (_player is null) return false;

        var clip = Pick(_player.GetAnimationList(), "2H_Melee_Attack_Spin", "2H_Melee_Attack_Spinning");

        if (clip.Length == 0) return false;

        // Stretched or squeezed to fill the gap between hits, so each hit is one whole turn
        // however slow or fast the skill has been tuned.
        var length = _player.GetAnimation(clip).Length;
        var speed = seconds > 0.01 ? (float)(length / seconds) : 1f;

        _player.Play(clip, customSpeed: speed);
        _attackFor = seconds;

        return true;
    }

    /// <summary>Stops a swing early so the body can run or idle again. The attack timer is not ours.</summary>
    public void CancelAttack() => _attackFor = 0;

    /// <summary>
    /// The model's hit reaction, if it carries one — but never over its own attack: a creature
    /// mid-swing that flinched would look as if the hit had interrupted it, and it has not.
    /// </summary>
    public void Hit()
    {
        if (_player is null || _attackFor > 0) return;

        var names = _player.GetAnimationList();
        var clip = Find(names, Hurt);

        if (clip.Length == 0) return;

        _player.Play(clip);
        _attackFor = System.Math.Min(_player.GetAnimation(clip).Length, 0.4);
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
