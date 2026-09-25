namespace Kiln.Core.Cosmetics;

/// <summary>
/// What the character has won to wear, and what it wears (REF-23): sword and armour skins, an
/// aura, a companion. Looks only — nothing here touches a number.
/// </summary>
/// <remarks>
/// Kept apart from the bag and the equipment on purpose. A skin is not an item: it cannot be
/// sold, upgraded, socketed or dropped, and keeping it out of the item code is what keeps all of
/// that from having to learn about it. One slot per kind; empty wears nothing.
/// </remarks>
public sealed class Wardrobe
{
    private readonly HashSet<string> _owned = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _worn = new(StringComparer.Ordinal);

    /// <summary>Raised whenever something is won, put on or taken off.</summary>
    public event Action? Changed;

    public IReadOnlyCollection<string> Owned => _owned;

    /// <summary>Kind → the id worn in it.</summary>
    public IReadOnlyDictionary<string, string> Worn => _worn;

    public bool Owns(string id) => _owned.Contains(id);

    /// <summary>The id worn for <paramref name="kind"/>, or null.</summary>
    public string? WornIn(string kind) => _worn.TryGetValue(kind, out var id) ? id : null;

    /// <summary>Adds a cosmetic to the collection. False when it was already there.</summary>
    public bool Unlock(string id)
    {
        if (string.IsNullOrEmpty(id) || !_owned.Add(id)) return false;

        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Wears <paramref name="id"/> in <paramref name="kind"/>, or takes it off with null. False
    /// for one not owned.
    /// </summary>
    public bool Wear(string kind, string? id)
    {
        if (id is null)
        {
            if (!_worn.Remove(kind)) return true;

            Changed?.Invoke();
            return true;
        }

        if (!_owned.Contains(id)) return false;
        if (_worn.TryGetValue(kind, out var current) && current == id) return true;

        _worn[kind] = id;
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Restores a saved wardrobe. Anything worn but not owned — a hand-edited save, a cosmetic
    /// since removed from the game — is left off rather than trusted.
    /// </summary>
    public void Load(IEnumerable<string> owned, IReadOnlyDictionary<string, string> worn, Func<string, bool> exists)
    {
        _owned.Clear();
        _worn.Clear();

        foreach (var id in owned.Where(exists)) _owned.Add(id);

        foreach (var (kind, id) in worn)
        {
            if (_owned.Contains(id)) _worn[kind] = id;
        }

        Changed?.Invoke();
    }
}
