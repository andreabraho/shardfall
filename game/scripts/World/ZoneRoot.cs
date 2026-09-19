using Godot;

namespace Kiln.Game.World;

/// <summary>
/// Sits on a zone scene's root and says which zone this is (WLD-01).
/// </summary>
/// <remarks>
/// The link between a scene and its content is one exported string, deliberately. Everything
/// else about a zone — its band, its exits, its shrines, what its camps are made of — is data
/// the validator can check; the scene only supplies the coordinates data cannot hold.
/// </remarks>
public partial class ZoneRoot : Node3D
{
    [Export] public string ZoneId { get; set; } = "";

    public override void _Ready()
    {
        if (!GameWorld.IsLoaded)
        {
            GD.PushError("[world] a zone scene loaded before GameWorld.Load() ran.");
            return;
        }

        GameWorld.EnterZone(ZoneId);

        Debug.DebugOverlay.Register("zone", () =>
        {
            var zone = GameWorld.CurrentZone;

            return zone is null
                ? "—"
                : $"{zone.Id} {zone.Band} · {GameWorld.LivingEnemies(GetTree())}/{GameWorld.PopulationCap} alive";
        });
    }
}
