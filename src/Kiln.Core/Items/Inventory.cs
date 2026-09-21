namespace Kiln.Core.Items;

/// <summary>An item and where it sits in the grid. Top-left cell, width and height from its spec.</summary>
public sealed record PlacedItem(ItemInstance Item, int X, int Y, int Width, int Height)
{
    public bool Covers(int x, int y) => x >= X && x < X + Width && y >= Y && y < Y + Height;
}

/// <summary>
/// The grid inventory (ITM-04, FR-5.7): multi-cell items, stacking materials, auto-sort, and
/// the payment surface every crafting bench talks to.
/// </summary>
/// <remarks>
/// The grid is kept from the original because it is a real decision space — a two-by-three
/// breastplate costing six cells is a meaningful thing to weigh against three stacks of
/// materials — and because "tidy the bag" is a small satisfying loop in its own right. What is
/// not kept is the original's habit of silently dropping what does not fit.
/// </remarks>
public sealed class Inventory : IResourceStore
{
    private readonly IItemSpecs _specs;
    private readonly List<PlacedItem> _placed = [];
    private readonly bool[,] _occupied;

    public Inventory(IItemSpecs specs, int width = 10, int height = 8, long yang = 0)
    {
        if (width < 1 || height < 1) throw new ArgumentOutOfRangeException(nameof(width));

        _specs = specs;
        Width = width;
        Height = height;
        Yang = yang;
        _occupied = new bool[width, height];
    }

    public int Width { get; }

    public int Height { get; }

    public long Yang { get; private set; }

    public IReadOnlyList<PlacedItem> Items => _placed;

    public int UsedCells => _placed.Sum(p => p.Width * p.Height);

    public int FreeCells => (Width * Height) - UsedCells;

    public event Action? Changed;

    // ---------------------------------------------------------------- yang

    public void AddYang(long amount)
    {
        if (amount <= 0) return;

        Yang += amount;
        Changed?.Invoke();
    }

    public bool TrySpendYang(long amount)
    {
        if (amount < 0 || Yang < amount) return false;

        Yang -= amount;
        Changed?.Invoke();

        return true;
    }

    // ---------------------------------------------------------------- placement

    public PlacedItem? Find(ItemInstance item) => _placed.FirstOrDefault(p => ReferenceEquals(p.Item, item));

    public PlacedItem? At(int x, int y) => _placed.FirstOrDefault(p => p.Covers(x, y));

    public bool Fits(int x, int y, int w, int h, ItemInstance? ignoring = null)
    {
        if (x < 0 || y < 0 || x + w > Width || y + h > Height) return false;

        for (var dx = 0; dx < w; dx++)
        {
            for (var dy = 0; dy < h; dy++)
            {
                if (!_occupied[x + dx, y + dy]) continue;

                var blocker = At(x + dx, y + dy);

                if (blocker is null || !ReferenceEquals(blocker.Item, ignoring)) return false;
            }
        }

        return true;
    }

    /// <summary>Places at an exact spot. Used by drag and drop.</summary>
    public bool TryPlace(ItemInstance item, int x, int y)
    {
        if (Find(item) is not null) return false;

        var spec = Spec(item);

        if (!Fits(x, y, spec.Width, spec.Height)) return false;

        Occupy(new PlacedItem(item, x, y, spec.Width, spec.Height), true);
        Changed?.Invoke();

        return true;
    }

    /// <summary>
    /// Adds an item wherever it fits, stacking into existing stacks first. Returns false only
    /// when the bag genuinely has no room — the caller then leaves the loot on the ground.
    /// </summary>
    public bool TryAdd(ItemInstance item)
    {
        var spec = Spec(item);

        if (spec.IsStackable && StackInto(item, spec)) return true;

        for (var y = 0; y <= Height - spec.Height; y++)
        {
            for (var x = 0; x <= Width - spec.Width; x++)
            {
                if (!Fits(x, y, spec.Width, spec.Height)) continue;

                Occupy(new PlacedItem(item, x, y, spec.Width, spec.Height), true);
                Changed?.Invoke();

                return true;
            }
        }

        return false;
    }

    private bool StackInto(ItemInstance item, ItemSpec spec)
    {
        var remaining = item.Count;

        foreach (var placed in _placed.Where(p => p.Item.DefId == item.DefId && p.Item.Count < spec.MaxStack))
        {
            var room = spec.MaxStack - placed.Item.Count;
            var moved = Math.Min(room, remaining);

            placed.Item.Count += moved;
            remaining -= moved;

            if (remaining == 0) break;
        }

        item.Count = remaining;

        if (remaining > 0) return false;

        Changed?.Invoke();

        return true;
    }

    public bool Remove(ItemInstance item)
    {
        if (Find(item) is not { } placed) return false;

        Occupy(placed, false);
        Changed?.Invoke();

        return true;
    }

    /// <summary>Moves an item within the grid, leaving it where it was if the target is blocked.</summary>
    public bool TryMove(ItemInstance item, int x, int y)
    {
        if (Find(item) is not { } placed) return false;
        if (!Fits(x, y, placed.Width, placed.Height, ignoring: item)) return false;

        Occupy(placed, false);
        Occupy(placed with { X = x, Y = y }, true);
        Changed?.Invoke();

        return true;
    }

    private void Occupy(PlacedItem placed, bool value)
    {
        for (var dx = 0; dx < placed.Width; dx++)
        {
            for (var dy = 0; dy < placed.Height; dy++)
            {
                _occupied[placed.X + dx, placed.Y + dy] = value;
            }
        }

        if (value) _placed.Add(placed);
        else _placed.RemoveAll(p => ReferenceEquals(p.Item, placed.Item));
    }

    /// <summary>
    /// Repacks the bag: equipment first by slot then by rarity, materials last, stacks merged.
    /// Ordering by kind rather than by value is the point — the player is looking for a
    /// category ("where are my stones"), not for the best item.
    /// </summary>
    public void AutoSort()
    {
        var items = _placed.Select(p => p.Item).ToList();

        foreach (var placed in _placed.ToList()) Occupy(placed, false);

        MergeStacks(items);

        var ordered = items
            .Where(i => i.Count > 0)
            .OrderBy(i => Spec(i).IsEquipment ? 0 : 1)
            .ThenBy(i => (int?)Spec(i).Slot ?? int.MaxValue)
            .ThenByDescending(i => (int)Spec(i).Rarity)
            .ThenByDescending(i => i.UpgradeLevel)
            .ThenBy(i => i.DefId, StringComparer.Ordinal)
            .ToList();

        foreach (var item in ordered) TryAdd(item);

        Changed?.Invoke();
    }

    private void MergeStacks(List<ItemInstance> items)
    {
        foreach (var group in items.GroupBy(i => i.DefId))
        {
            var spec = Spec(group.First());

            if (!spec.IsStackable) continue;

            var stacks = group.ToList();
            var total = stacks.Sum(s => s.Count);

            foreach (var stack in stacks)
            {
                var take = Math.Min(spec.MaxStack, total);
                stack.Count = take;
                total -= take;
            }
        }
    }

    // ---------------------------------------------------------------- IResourceStore

    public int CountOf(string itemId) => _placed.Where(p => p.Item.DefId == itemId).Sum(p => p.Item.Count);

    public bool CanAfford(long yang, IReadOnlyDictionary<string, int> materials)
    {
        if (Yang < yang) return false;

        return materials.All(m => CountOf(m.Key) >= m.Value);
    }

    public bool TrySpend(long yang, IReadOnlyDictionary<string, int> materials)
    {
        if (!CanAfford(yang, materials)) return false;

        Yang -= yang;

        foreach (var (id, count) in materials) Consume(id, count);

        Changed?.Invoke();

        return true;
    }

    private void Consume(string itemId, int count)
    {
        var remaining = count;

        foreach (var placed in _placed.Where(p => p.Item.DefId == itemId).ToList())
        {
            var taken = Math.Min(placed.Item.Count, remaining);
            placed.Item.Count -= taken;
            remaining -= taken;

            if (placed.Item.Count == 0) Occupy(placed, false);
            if (remaining == 0) return;
        }
    }

    /// <summary>
    /// Replaces the whole contents with a saved state (UIX-01).
    /// </summary>
    /// <remarks>
    /// Returns the items that could not be put back where they were. That should never happen
    /// with a save this build wrote, but an item that grew a size in a patch would otherwise
    /// vanish; the caller decides where it goes instead, and the player keeps it.
    /// </remarks>
    public List<ItemInstance> Restore(long yang, long nextGrantUid, IEnumerable<(ItemInstance Item, int X, int Y)> items)
    {
        _placed.Clear();
        Array.Clear(_occupied);

        Yang = Math.Max(0, yang);
        NextGrantUid = Math.Min(-1, nextGrantUid);

        var homeless = new List<ItemInstance>();

        foreach (var (item, x, y) in items)
        {
            var spec = Spec(item);

            if (Fits(x, y, spec.Width, spec.Height))
            {
                Occupy(new PlacedItem(item, x, y, spec.Width, spec.Height), true);
            }
            else
            {
                homeless.Add(item);
            }
        }

        foreach (var item in homeless.ToList())
        {
            if (TryAdd(item)) homeless.Remove(item);
        }

        Changed?.Invoke();

        return homeless;
    }

    public bool TryGrant(string itemId, int count)
    {
        var spec = _specs.TryGet(itemId, out var found) ? found : null;

        if (spec is null) return false;

        var item = new ItemInstance(NextGrantUid--, itemId, count);

        if (!spec.IsStackable) item.Count = 1;

        return TryAdd(item);
    }

    /// <summary>
    /// Uids for items the inventory mints itself (a stone handed back from a socket). Negative
    /// so they can never collide with the factory's, which counts up.
    /// </summary>
    public long NextGrantUid { get; private set; } = -1;

    private ItemSpec Spec(ItemInstance item) =>
        _specs.TryGet(item.DefId, out var spec) ? spec : new ItemSpec { Id = item.DefId };
}
