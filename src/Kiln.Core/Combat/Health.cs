namespace Kiln.Core.Combat;

/// <summary>
/// A depletable pool (health, mana, stamina). Engine-free so death, regeneration and
/// resource costs are testable without booting Godot.
/// </summary>
public sealed class Pool(double max, double current)
{
    private double _max = Math.Max(1, max);

    public Pool(double max) : this(max, max) { }

    public double Max
    {
        get => _max;
        set
        {
            var previous = _max;
            _max = Math.Max(1, value);

            // Scale current with max so a +HP buff expiring cannot instantly kill,
            // and gaining max HP does not leave the bar looking emptier than before.
            if (previous > 0 && Current > 0)
            {
                Current = Current / previous * _max;
            }

            Current = Math.Min(Current, _max);
        }
    }

    public double Current { get; private set; } = Math.Clamp(current, 0, Math.Max(1, max));

    public double Fraction => Max <= 0 ? 0 : Current / Max;

    public bool IsEmpty => Current <= 0;

    public bool IsFull => Current >= Max;

    /// <summary>Removes up to <paramref name="amount"/>; returns how much was actually removed.</summary>
    public double Remove(double amount)
    {
        if (amount <= 0) return 0;

        var removed = Math.Min(amount, Current);
        Current -= removed;
        return removed;
    }

    /// <summary>Adds up to <paramref name="amount"/>; returns how much was actually added.</summary>
    public double Add(double amount)
    {
        if (amount <= 0) return 0;

        var added = Math.Min(amount, Max - Current);
        Current += added;
        return added;
    }

    /// <summary>True when there is enough to pay a cost. Does not spend it.</summary>
    public bool CanAfford(double cost) => Current >= cost;

    /// <summary>Spends the full cost, or nothing at all. Partial spends are always a bug.</summary>
    public bool TrySpend(double cost)
    {
        if (cost < 0 || !CanAfford(cost)) return false;

        Current -= cost;
        return true;
    }

    public void Fill() => Current = Max;

    public void Empty() => Current = 0;

    public void SetCurrent(double value) => Current = Math.Clamp(value, 0, Max);

    public override string ToString() => $"{Current:F0}/{Max:F0}";
}
