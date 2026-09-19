using Godot;
using Kiln.Game.Input;
using Kiln.Game.Items;

namespace Kiln.Game.Debug;

/// <summary>
/// Hands the player the yang and materials a system needs to be exercised (F8).
/// </summary>
/// <remarks>
/// The upgrade ladder cannot be judged on a starting purse: reaching the first fallible rung
/// takes a few thousand yang, and seeing the pity counter do its job takes several attempts
/// at a rung that fails more often than not. Without this, testing the anvil means an hour of
/// killing wolves first, which is exactly the grind the design is trying to avoid — and it
/// would be testing the drop rate rather than the anvil.
/// <para>
/// Debug builds only. It frees itself in a release build rather than sitting there as an
/// unbound key waiting to be found.
/// </para>
/// </remarks>
public partial class DebugGrants : Node
{
    [Export] public int Yang { get; set; } = 250_000;

    private static readonly (string Id, int Count)[] Kit =
    [
        ("mat_iron_scrap", 60),
        ("mat_tempering_oil", 20),
        ("mat_steel_core", 20),
        ("mat_shard_essence", 10),
        ("mat_radiant_core", 3),
        ("mat_boring_stone", 6),
        ("mat_mutation_ink", 12),
        ("stn_ember", 3),
        ("stn_granite", 3),
        ("stn_falcon", 2),
        ("stn_bane_undead", 2),
    ];

    public override void _Ready()
    {
        if (!OS.IsDebugBuild())
        {
            QueueFree();
            return;
        }

        GD.Print("[debug] F8 grants test yang and crafting materials");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed(GameActions.DebugGrantResources)) return;

        GetViewport().SetInputAsHandled();

        if (GetTree().GetFirstNodeInGroup("player") is not Node player) return;

        var bag = player.GetNodeOrNull<PlayerInventory>("PlayerInventory");

        if (bag is null || !GameItems.IsLoaded) return;

        bag.Bag.AddYang(Yang);

        var refused = 0;

        foreach (var (id, count) in Kit)
        {
            // TryGrant reports a full bag rather than dropping the stack, so a cramped bag
            // shows up as a number here instead of as materials that silently never arrived.
            if (!bag.Bag.TryGrant(id, count)) refused++;
        }

        GD.Print($"[debug] granted {Yang:N0} yang and {Kit.Length - refused}/{Kit.Length} material stacks"
            + (refused > 0 ? " — bag is full" : ""));
    }
}
