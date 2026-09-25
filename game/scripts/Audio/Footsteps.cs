using Godot;
using Kiln.Game.World;

namespace Kiln.Game.Audio;

/// <summary>
/// The player's footsteps (REF-20): one sound a stride while walking, of the map's ground.
/// </summary>
/// <remarks>
/// Counted by distance rather than by the animation, so a slowed player steps less often
/// and a shoved one does not step at all while in the air. Which sound it is belongs to the
/// map (<c>footsteps</c> in the zone data): grass, dirt, or the tower's stone.
/// </remarks>
public partial class Footsteps : Node
{
    /// <summary>Metres between two steps.</summary>
    private const float Stride = 1.7f;

    /// <summary>Slower than this is shuffling, not walking.</summary>
    private const float MinSpeed = 1.0f;

    private CharacterBody3D? _body;
    private float _walked;

    public override void _Ready() => _body = GetParent() as CharacterBody3D;

    public override void _PhysicsProcess(double delta)
    {
        if (_body is null || !_body.IsOnFloor()) return;

        var speed = (_body.Velocity with { Y = 0 }).Length();

        if (speed < MinSpeed)
        {
            // The first step of the next walk comes soon after starting, not a stride later.
            _walked = Stride * 0.6f;
            return;
        }

        _walked += speed * (float)delta;

        if (_walked < Stride) return;

        _walked -= Stride;

        if (GameContent.IsLoaded
            && GameContent.Database.Zones.TryGetValue(GameWorld.CurrentZoneId, out var zone)
            && zone.Footsteps.Length > 0)
        {
            AudioDirector.Play(zone.Footsteps);
        }
    }
}
