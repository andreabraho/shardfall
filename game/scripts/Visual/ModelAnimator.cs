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
    private string _idleTwoHand = "";
    private string _run = "";

    /// <summary>
    /// Holding a great sword (REF-22): it stands, swings and casts two-handed.
    /// </summary>
    public bool TwoHanded { get; set; }
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
        _idleTwoHand = Pick(names, "2H_Melee_Idle");
        _run = Find(names, Move);

        if (_run.Length == 0) _run = _idle;

        // Looping is a property of the imported clip, and glTF does not carry one. Without
        // this every animation plays once and the character freezes on its last frame.
        foreach (var name in new[] { _idle, _idleTwoHand, _run })
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

    // Held while a telegraphed attack winds up. Only the humanoid packs carry one; a creature
    // without it rears back instead (VisualRoot).
    private static readonly string[] Charge =
        ["Spellcasting", "Spellcast_Long", "Idle_Attack", "2H_Melee_Idle", "Taunt", "Idle_Combat"];

    // A shot leaving the hand.
    private static readonly string[] Shot =
        ["Spellcast_Shoot", "1H_Ranged_Shoot", "Throw", "Weapon", "Punch"];

    private static readonly string[] Fall =
        ["Death_A", "Death", "Death_B", "Spider_Death", "Rat_Death", "Snake_Death", "Wasp_Death"];

    private bool _dead;

    /// <summary>Plays the model's death and holds its last frame. Seconds it lasts; 0 without one.</summary>
    public double Die()
    {
        if (_player is null) return 0;

        var clip = Find(_player.GetAnimationList(), Fall);

        if (clip.Length == 0) return 0;

        _dead = true;
        _player.GetAnimation(clip).LoopMode = Animation.LoopModeEnum.None;
        _player.Play(clip);
        return _player.GetAnimation(clip).Length;
    }

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
    /// The player's four blows (REF-22), one clip each and the fourth — the one that sweeps —
    /// the widest: a flat cut with a sword, a full turn with a great sword.
    /// </summary>
    private static readonly string[] OneHandChain =
    [
        "1H_Melee_Attack_Slice_Diagonal",
        "1H_Melee_Attack_Stab",
        "1H_Melee_Attack_Chop",
        "1H_Melee_Attack_Slice_Horizontal",
    ];

    private static readonly string[] TwoHandChain =
    [
        "2H_Melee_Attack_Slice",
        "2H_Melee_Attack_Stab",
        "2H_Melee_Attack_Chop",
        "2H_Melee_Attack_Spin",
    ];

    /// <summary>How far into a melee clip its blade meets the target, as a share of the clip.</summary>
    private const double ImpactAt = 0.4;

    /// <summary>
    /// The player's blow number <paramref name="blow"/> of a fight (REF-22), timed to the attack:
    /// the blade meets the target as the windup ends, and the clip is over before the next
    /// blow may start. Played at its own speed it did neither — slow blows landed their damage
    /// before the swing had come round, quick ones cut each other off.
    /// </summary>
    public double Blow(int blow, double windup, double interval)
    {
        if (_player is null) return 0;

        var chain = TwoHanded ? TwoHandChain : OneHandChain;
        var clip = Pick(_player.GetAnimationList(), chain[(((blow - 1) % chain.Length) + chain.Length) % chain.Length]);

        return clip.Length == 0 ? Attack() : PlayTimed(clip, windup, interval);
    }

    /// <summary>
    /// A skill's own move (REF-22): a heavy overhead for Heavy Strike, a flat cut for Cleave,
    /// the shield driven forward for Shield Bash, a two-handed smash for Ground Slam, a guard
    /// raised for Iron Skin, the blade lifted for the Blade Aura. A skill's damage lands as it
    /// is cast, so its move is played to land quickly and to be over by the time the Warrior
    /// may move again.
    /// </summary>
    public double SkillMove(string motion, double seconds)
    {
        if (_player is null) return 0;

        var wanted = motion switch
        {
            "chop" => TwoHanded ? "2H_Melee_Attack_Chop" : "1H_Melee_Attack_Chop",
            "slice" => TwoHanded ? "2H_Melee_Attack_Slice" : "1H_Melee_Attack_Slice_Horizontal",
            "bash" => "Block_Attack",
            "slam" => "2H_Melee_Attack_Chop",
            "brace" => "Block",
            "raise" => "Spellcast_Raise",
            _ => "",
        };

        var clip = wanted.Length == 0 ? "" : Pick(_player.GetAnimationList(), wanted);

        if (clip.Length == 0) return motion is "brace" or "raise" ? Flourish() : Attack();

        // A buff is raised, not struck: it has the whole hold to play out.
        var landBy = motion is "brace" or "raise" ? seconds * 0.6 : SkillImpact;

        return PlayTimed(clip, landBy, seconds);
    }

    /// <summary>Seconds into a skill's move its blow should appear to land.</summary>
    private const double SkillImpact = 0.22;

    /// <summary>
    /// Plays a one-shot clip fast enough that its impact comes by <paramref name="impactBy"/>
    /// and it ends within <paramref name="within"/>, and not slower than a little under its
    /// own pace. Restarted from its first frame even when it is the clip already playing.
    /// </summary>
    private double PlayTimed(string clip, double impactBy, double within)
    {
        var animation = _player!.GetAnimation(clip);
        var length = animation.Length;
        var speed = System.Math.Max(length * ImpactAt / System.Math.Max(0.05, impactBy), length / System.Math.Max(0.05, within));

        speed = System.Math.Clamp(speed, 0.8, 3.0);

        animation.LoopMode = Animation.LoopModeEnum.None;
        _player.Play(clip, customBlend: 0.08, customSpeed: (float)speed);
        _player.Seek(0, update: true);

        _attackFor = length / speed;
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
    /// <summary>
    /// Holds a charging pose for <paramref name="seconds"/>, looped. False when the model has
    /// none, so the caller can show the wind-up another way.
    /// </summary>
    public bool WindUp(double seconds)
    {
        if (_player is null) return false;

        var clip = Find(_player.GetAnimationList(), Charge);

        if (clip.Length == 0) return false;

        _player.GetAnimation(clip).LoopMode = Animation.LoopModeEnum.Linear;
        _player.Play(clip);
        _attackFor = seconds;
        return true;
    }

    /// <summary>A throw or a cast; the ordinary attack clip when the model has neither.</summary>
    public double Shoot()
    {
        if (_player is null) return 0;

        var clip = Find(_player.GetAnimationList(), Shot);

        if (clip.Length == 0) return Attack();

        _player.Play(clip);
        _attackFor = _player.GetAnimation(clip).Length;
        return _attackFor;
    }

    public void CancelAttack() => _attackFor = 0;

    /// <summary>
    /// The model's hit reaction, if it carries one — but never over its own attack: a creature
    /// mid-swing that flinched would look as if the hit had interrupted it, and it has not.
    /// </summary>
    public void Hit()
    {
        if (_player is null || _attackFor > 0 || _dead) return;

        var names = _player.GetAnimationList();
        var clip = Find(names, Hurt);

        if (clip.Length == 0) return;

        _player.Play(clip);
        _attackFor = System.Math.Min(_player.GetAnimation(clip).Length, 0.4);
    }

    public override void _Process(double delta)
    {
        if (_player is null || _dead) return;

        // An attack owns the body until it finishes. Cutting back to a run mid-swing is what
        // makes an attack read as a twitch rather than a blow.
        if (_attackFor > 0)
        {
            _attackFor -= delta;
            return;
        }

        var speed = _body is null ? 0f : (_body.Velocity with { Y = 0 }).Length();

        // With a great sword in hand, standing still is its guard, not an empty-handed rest.
        var idle = TwoHanded && _idleTwoHand.Length > 0 ? _idleTwoHand : _idle;

        Play(speed > MovingAbove ? _run : idle);
    }

    private void Play(string name)
    {
        if (name.Length == 0 || _player is null || _player.CurrentAnimation == name) return;

        _player.Play(name);
    }
}
