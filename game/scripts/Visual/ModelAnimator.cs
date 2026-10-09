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
    /// Metres per second below which a creature is animated as walking rather than running:
    /// an animal roaming its ground at an amble (2026-09-30), which run at that pace looked
    /// like it was treading water. Only for enemies — the player moves at one speed.
    /// </summary>
    private const float WalkingBelow = 2.4f;

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
    private string _walk = "";

    /// <summary>
    /// Holding a great sword (REF-22): it stands, swings and casts two-handed.
    /// </summary>
    public bool TwoHanded { get; set; }

    /// <summary>True while a blade is held in each hand (the Ninja, CLS-03): the blows are the two-weapon ones.</summary>
    public bool DualWield { get; set; }

    /// <summary>
    /// How the body stands, runs and strikes (CLS-04, CLS-06): "bow" for the Archer — the bow low
    /// in the left hand, every shot a draw and a release — "caster" for the Dragonseer, empty for
    /// a blade.
    /// </summary>
    public string Stance
    {
        get => _stance;
        set
        {
            if (_stance == value) return;

            _stance = value;
            Restance();
        }
    }

    private string _stance = "";

    /// <summary>True while the Archer draws a bow back: the string goes to the drawing hand (BowString).</summary>
    public bool DrawingBow => _stance == "bow" && _player is { } player && player.CurrentAnimation.ToString().Contains("Ranged_Bow_Draw");
    private string _idleBow = "";

    /// <summary>A clip played as the one before it ends: the release after the draw.</summary>
    private (string Clip, float Speed, double Seconds)? _then;

    private void Restance()
    {
        if (_player is null) return;

        var names = _player.GetAnimationList();

        _idleBow = Pick(names, "Ranged_Bow_Idle");

        foreach (var name in new[] { _idleBow })
        {
            if (name.Length > 0) _player.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
        }
    }

    /// <summary>The gear whose blades are carried one way and struck with another (the Ninja's daggers).</summary>
    public GearRig? Gear { get; set; }

    /// <summary>How far the blades are turned to their carrying grip, 0 to 1.</summary>
    private float _carry = 1f;

    /// <summary>Seconds a blade takes to turn in the hand.</summary>
    private const float TurnTime = 0.12f;

    /// <summary>True while the clip holding the body is a flinch, not a blow: the blades stay as carried.</summary>
    private bool _flinch;
    private int _swing;
    private double _attackFor;

    /// <summary>True when this model had animations to drive at all.</summary>
    public bool Ready => _player is not null;

    public void Adopt(Node3D model, Node3D owner)
    {
        _player = model.FindChildren("*", "AnimationPlayer", true, false)
            .OfType<AnimationPlayer>().FirstOrDefault();
        _looping = "";

        if (_player is null) return;

        _body = owner.GetParentOrNull<CharacterBody3D>();

        var names = _player.GetAnimationList();

        _idle = Find(names, Idle);
        _idleTwoHand = Pick(names, "Melee_2H_Idle", "2H_Melee_Idle");
        _run = Find(names, Move);

        if (_run.Length == 0) _run = _idle;

        // A walk of its own where the model has one; otherwise it runs at every pace, as before.
        _walk = _body is Combat.EnemyBrain ? Find(names, Walk) : "";

        if (_walk.Length == 0) _walk = _run;

        // Looping is a property of the imported clip, and glTF does not carry one. Without
        // this every animation plays once and the character freezes on its last frame.
        foreach (var name in new[] { _idle, _idleTwoHand, _run, _walk })
        {
            if (name.Length > 0) _player.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
        }

        Restance();
        Play(_idle);
    }

    // What each role is called across the packs in use. KayKit names clips "Running_A";
    // Quaternius "Gallop", "Rat_Run" or "CharacterArmature|Run". The part after the last "|"
    // is compared, and the first list entry any clip matches wins, so the best-looking clip
    // for a role is listed first.
    private static readonly string[] Idle = ["Idle", "Idle_Loop", "Idle_A", "Rat_Idle", "Spider_Idle", "Snake_Idle", "Flying_Idle", "Idle_2", "Wasp_Flying", "Frog_Idle"];

    private static readonly string[] Move =
        ["Running_A", "Running_B", "Run", "Gallop", "Rat_Run", "Fast_Flying", "Walking_A", "Walk",
         "Spider_Walk", "Snake_Walk", "Rat_Walk", "Wasp_Flying", "Frog_Jump"];

    // A creature that flies has no walk: roaming, it drifts on its hover rather than beating
    // along at full tilt in slow motion (the ghosts, imps, golelings and tribal masks).
    private static readonly string[] Walk =
        ["Walk", "Walking_A", "Walking_B", "Rat_Walk", "Spider_Walk", "Snake_Walk", "Flying_Idle", "Frog_Jump"];

    private static readonly string[] Strike =
        ["Attack", "Attack_Headbutt", "Attack_Kick", "Punch", "Bite_Front", "Headbutt",
         "Rat_Attack", "Spider_Attack", "Snake_Attack", "Wasp_Attack", "Frog_Attack", "Weapon"];

    // Held while a telegraphed attack winds up. Only the humanoid packs carry one; a creature
    // without it rears back instead (VisualRoot).
    private static readonly string[] Charge =
        ["Spellcasting", "Spellcast_Long", "Idle_Attack", "2H_Melee_Idle", "Melee_2H_Idle", "Taunt", "Idle_Combat"];

    // A shot leaving the hand.
    private static readonly string[] Shot =
        ["Spellcast_Shoot", "1H_Ranged_Shoot", "Throw", "Weapon", "Punch"];

    private static readonly string[] Fall =
        ["Death_A", "Death", "Death_B", "Spider_Death", "Rat_Death", "Snake_Death", "Wasp_Death", "Frog_Death"];

    private bool _dead;

    /// <summary>Plays the model's death and holds its last frame. Seconds it lasts; 0 without one.</summary>
    public double Die()
    {
        if (_player is null) return 0;

        var clip = Find(_player.GetAnimationList(), Fall);

        if (clip.Length == 0) return 0;

        _dead = true;
        _player.GetAnimation(clip).LoopMode = Animation.LoopModeEnum.None;
        PlayClip(clip);
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

        PlayClip(clip);
        _attackFor = _player.GetAnimation(clip).Length;

        return _attackFor;
    }

    /// <summary>
    /// The player's four blows (REF-22), one clip each and the fourth — the one that sweeps —
    /// the widest: a flat cut with a sword, a full turn with a great sword.
    /// </summary>
    /// <remarks>
    /// Each blow under both the names KayKit has used: "Melee_1H_…" in the 2.0 animation
    /// files the Knight wears, "1H_Melee_…" in the 1.0 characters.
    /// </remarks>
    private static readonly string[][] OneHandChain =
    [
        ["Melee_1H_Attack_Slice_Diagonal", "1H_Melee_Attack_Slice_Diagonal"],
        ["Melee_1H_Attack_Stab", "1H_Melee_Attack_Stab"],
        ["Melee_1H_Attack_Chop", "1H_Melee_Attack_Chop"],
        ["Melee_1H_Attack_Slice_Horizontal", "1H_Melee_Attack_Slice_Horizontal"],
    ];

    // Two blades (CLS-03): a cut with both, a thrust, a chop, and a spin to close.
    private static readonly string[][] DualChain =
    [
        ["Melee_Dualwield_Attack_Slice", "Melee_1H_Attack_Slice_Diagonal"],
        ["Melee_Dualwield_Attack_Stab", "Melee_1H_Attack_Stab"],
        ["Melee_Dualwield_Attack_Chop", "Melee_1H_Attack_Chop"],
        ["Melee_2H_Attack_Spinning", "Melee_Dualwield_Attack_Slice", "Melee_1H_Attack_Slice_Horizontal"],
    ];

    private static readonly string[][] TwoHandChain =
    [
        ["Melee_2H_Attack_Slice", "2H_Melee_Attack_Slice"],
        ["Melee_2H_Attack_Stab", "2H_Melee_Attack_Stab"],
        ["Melee_2H_Attack_Chop", "2H_Melee_Attack_Chop"],
        ["Melee_2H_Attack_Spin", "2H_Melee_Attack_Spin"],
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

        // A shot is a draw through the windup and a release as it lands.
        if (_stance == "bow") return DrawAndLoose("Ranged_Bow_Draw", "Ranged_Bow_Release", windup, interval - windup);

        var chain = DualWield ? DualChain : TwoHanded ? TwoHandChain : OneHandChain;
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
    public double SkillMove(string motion, double seconds, double landBy = 0)
    {
        if (_player is null) return 0;

        // The bow (CLS-04): every shot is a draw and a release, the rain is loosed upward.
        if (_stance == "bow" && motion is "shoot" or "volley" or "shoot_up")
        {
            var draw = landBy > 0 ? landBy : motion == "volley" ? 0.07 : 0.16;

            return motion == "shoot_up"
                ? DrawAndLoose("Ranged_Bow_Draw_Up", "Ranged_Bow_Release_Up", draw, System.Math.Max(0.2, seconds - draw))
                : DrawAndLoose("Ranged_Bow_Draw", "Ranged_Bow_Release", draw, System.Math.Max(0.12, seconds - draw));
        }

        // With a blade in each hand (CLS-03) a blow is struck with both where there is a clip for it.
        string[] dual = !DualWield ? [] : motion switch
        {
            "chop" or "slam" or "leap" => ["Melee_Dualwield_Attack_Chop"],
            "slice" or "wave" => ["Melee_Dualwield_Attack_Slice"],
            "charge" => ["Melee_Dualwield_Attack_Stab"],
            _ => [],
        };

        string[] wanted = motion switch
        {
            // Magic from the hand (CLS-05, CLS-06): a bolt thrown, a seal swept forward, a claw called up;
            // a spell worked with both hands; a long one, for a heal or a rain.
            "cast" or "seals" or "breath" => ["Ranged_Magic_Shoot", "Throw"],
            // The Shadow Claw (CLS-05, at your call): a blow of the marked hand — the left, which the
            // shield arm's thrust drives forward; the pack's punch is of the sword hand.
            "claw" => ["Melee_Block_Attack", "Melee_Unarmed_Attack_Punch_A", "Ranged_Magic_Shoot"],
            "spell" => ["Ranged_Magic_Spellcasting", "Ranged_Magic_Raise", "Cheering"],
            // The Asura's own (CLS-05, at your call: not the Warrior's cheer and block): power drawn up
            // with both arms for the Arcane Mail, a long summoning for the Dread.
            "invoke" => ["Ranged_Magic_Raise", "Ranged_Magic_Spellcasting", "Cheering"],
            "dread" => ["Ranged_Magic_Summon", "Ranged_Magic_Spellcasting_Long", "Cheering"],
            "summon" => ["Ranged_Magic_Spellcasting_Long", "Ranged_Magic_Raise", "Cheering"],
            // A leap back (CLS-04): the Backstep.
            "backstep" => ["Dodge_Backward", "Jump_Full_Short"],
            "shoot" or "volley" or "shoot_up" => ["Throw"],
            // Something thrown from the hand: the Viper Fang.
            "throw" => ["Throw"],
            // A hop aside: the Shadow Step.
            "step" => ["Jump_Full_Short", "Jump_Start", "Running_B"],
            // A leap onto the target, the blades coming down with the landing: the Shadow Strike.
            "pounce" => ["Melee_1H_Attack_Jump_Chop", "Melee_Dualwield_Attack_Chop", "Melee_1H_Attack_Chop"],
            "chop" => TwoHanded ? ["Melee_2H_Attack_Chop", "2H_Melee_Attack_Chop"] : ["Melee_1H_Attack_Chop", "1H_Melee_Attack_Chop"],
            "slice" => TwoHanded ? ["Melee_2H_Attack_Slice", "2H_Melee_Attack_Slice"] : ["Melee_1H_Attack_Slice_Horizontal", "1H_Melee_Attack_Slice_Horizontal"],
            "bash" => ["Melee_Block_Attack", "Block_Attack"],
            // A leap and the blade brought down on the ground: the Heavy Strike (2026-10-02).
            "leap" => ["Melee_1H_Attack_Jump_Chop", "Melee_2H_Attack_Chop", "2H_Melee_Attack_Chop", "Melee_1H_Attack_Chop"],
            "slam" => ["Melee_1H_Attack_Jump_Chop", "Melee_2H_Attack_Chop", "2H_Melee_Attack_Chop"],
            "brace" => ["Melee_Block", "Block"],
            "raise" => _stance == "caster" ? ["Ranged_Magic_Raise", "Cheering"] : ["Cheering", "Cheer", "Spellcast_Raise"],
            // REF-21: the Blade Wave is a diagonal cut thrown forward, the Battle Frenzy a
            // shout with the arms up.
            "wave" => TwoHanded ? ["Melee_2H_Attack_Slice", "2H_Melee_Attack_Slice"] : ["Melee_1H_Attack_Slice_Diagonal", "1H_Melee_Attack_Slice_Diagonal"],
            "roar" => ["Cheering", "Cheer", "Spellcast_Raise"],
            // The Piercing Charge drives the point ahead.
            "charge" => TwoHanded ? ["Melee_2H_Attack_Stab", "2H_Melee_Attack_Stab"] : ["Melee_1H_Attack_Stab", "1H_Melee_Attack_Stab"],
            _ => [],
        };

        var clip = wanted.Length == 0 ? "" : Pick(_player.GetAnimationList(), [.. dual, .. wanted]);

        if (clip.Length == 0) return motion is "brace" or "raise" or "roar" or "spell" or "summon" or "invoke" or "dread" ? Flourish() : Attack();

        // A buff is raised, not struck: it has the whole hold to play out.
        // A slow skill says when it lands; any other lands quickly.
        var impact = motion is "brace" or "raise" or "roar" or "spell" or "summon" or "invoke" or "dread" ? seconds * 0.6 : landBy > 0 ? landBy : SkillImpact;

        return PlayTimed(clip, impact, seconds);
    }

    /// <summary>
    /// A shot (CLS-04): <paramref name="draw"/> played to be drawn in <paramref name="drawSeconds"/>,
    /// then <paramref name="release"/> over <paramref name="after"/>. The ordinary swing on a model
    /// without the clips.
    /// </summary>
    private double DrawAndLoose(string draw, string release, double drawSeconds, double after)
    {
        var names = _player!.GetAnimationList();

        if (!names.Contains(draw) || !names.Contains(release)) return Attack();

        var drawing = _player.GetAnimation(draw);
        var loosing = _player.GetAnimation(release);
        var speed = (float)System.Math.Clamp(drawing.Length / System.Math.Max(0.05, drawSeconds), 0.8, 8.0);

        _flinch = false;
        drawing.LoopMode = Animation.LoopModeEnum.None;
        loosing.LoopMode = Animation.LoopModeEnum.None;
        PlayClip(draw, customBlend: 0.05, customSpeed: speed);
        _player.Seek(0, update: true);

        _attackFor = drawing.Length / speed;

        // The release at its own pace, or quicker if the next shot is due before it would end.
        var hold = System.Math.Max(0.1, after);
        var loose = (float)System.Math.Clamp(loosing.Length / hold, 1.0, 4.0);

        _then = (release, loose, System.Math.Min(hold, loosing.Length / loose));

        return _attackFor + _then.Value.Seconds;
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

        _flinch = false;
        _then = null;
        animation.LoopMode = Animation.LoopModeEnum.None;
        PlayClip(clip, customBlend: 0.08, customSpeed: (float)speed);
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

        var clip = Pick(_player.GetAnimationList(), "Cheer", "Cheering", "Spellcast_Raise", "Taunt", "Block", "Melee_Block");

        if (clip.Length == 0) return Attack();

        PlayClip(clip);
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

        var clip = Pick(_player.GetAnimationList(), "Melee_2H_Attack_Spin", "Melee_2H_Attack_Spinning", "2H_Melee_Attack_Spin", "2H_Melee_Attack_Spinning");

        if (clip.Length == 0) return false;

        // Stretched or squeezed to fill the gap between hits, so each hit is one whole turn
        // however slow or fast the skill has been tuned.
        var length = _player.GetAnimation(clip).Length;
        var speed = seconds > 0.01 ? (float)(length / seconds) : 1f;

        PlayClip(clip, customSpeed: speed);
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

        // A caster gathers a spell between its hands (CLS-06, the Dragon's Breath); a blade is drawn back.
        var clip = _stance == "caster" ? Pick(_player.GetAnimationList(), "Ranged_Magic_Spellcasting_Long", "Ranged_Magic_Spellcasting") : "";

        if (clip.Length == 0) clip = Find(_player.GetAnimationList(), Charge);

        if (clip.Length == 0) return false;

        _player.GetAnimation(clip).LoopMode = Animation.LoopModeEnum.Linear;
        PlayClip(clip);
        _attackFor = seconds;
        return true;
    }

    /// <summary>A throw or a cast; the ordinary attack clip when the model has neither.</summary>
    public double Shoot()
    {
        if (_player is null) return 0;

        var clip = Find(_player.GetAnimationList(), Shot);

        if (clip.Length == 0) return Attack();

        PlayClip(clip);
        _attackFor = _player.GetAnimation(clip).Length;
        return _attackFor;
    }

    public void CancelAttack()
    {
        _attackFor = 0;
        _then = null;
    }

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

        PlayClip(clip);
        _attackFor = System.Math.Min(_player.GetAnimation(clip).Length, 0.4);
        _flinch = true;
    }

    public override void _Process(double delta)
    {
        if (_player is null || _dead) return;

        if (DualWield && Gear is not null)
        {
            _carry = Mathf.MoveToward(_carry, _attackFor > 0 && !_flinch ? 0f : 1f, (float)delta / TurnTime);
            Gear.Carry(_carry);
        }

        // An attack owns the body until it finishes. Cutting back to a run mid-swing is what
        // makes an attack read as a twitch rather than a blow.
        if (_attackFor > 0)
        {
            _attackFor -= delta;
            return;
        }

        // The release, once the draw is done.
        if (_then is { } next)
        {
            _then = null;
            PlayClip(next.Clip, customBlend: 0.03, customSpeed: next.Speed);
            _attackFor = next.Seconds;
            return;
        }

        _flinch = false;

        var speed = _body is null ? 0f : (_body.Velocity with { Y = 0 }).Length();

        // With a great sword in hand, standing still is its guard, not an empty-handed rest; with
        // two blades it is the same guard, the hands together in front.
        var idle = _stance == "bow" && _idleBow.Length > 0 ? _idleBow
            : (TwoHanded || DualWield) && _idleTwoHand.Length > 0 ? _idleTwoHand : _idle;
        // The Archer runs as everyone does (2026-10-09, at your call): KayKit's run holding the bow carried it out
        // to the side, sticking out from the body all the way.
        var run = _run;

        Play(speed <= MovingAbove ? idle : speed < WalkingBelow ? _walk : run);
    }

    /// <summary>The stand, walk or run loop last started by <see cref="Play"/>; empty once anything else plays.</summary>
    private string _looping = "";

    /// <remarks>
    /// Remembers which loop it started rather than asking the player which clip is current
    /// (2026-10-09): the answer comes back as a new engine string every time, and every creature
    /// asked every frame.
    /// </remarks>
    private void Play(string name)
    {
        if (name.Length == 0 || _player is null || (name == _looping && _player.IsPlaying())) return;

        _player.Play(name);
        _looping = name;
    }

    /// <summary>Any clip other than the loops: a blow, a flinch, a cast, a fall.</summary>
    private void PlayClip(string clip, double customBlend = -1, float customSpeed = 1f)
    {
        _looping = "";
        _player!.Play(clip, customBlend, customSpeed);
    }
}
