namespace Kiln.Core.Ai;

public enum BtStatus
{
    /// <summary>Finished this tick.</summary>
    Success,

    /// <summary>Could not run, or failed. The parent moves on.</summary>
    Failure,

    /// <summary>Still working; tick it again next frame.</summary>
    Running,
}

/// <summary>
/// A behaviour tree node, generic over the context so the framework stays engine-free and
/// unit-testable while the engine supplies whatever the leaves actually need.
/// </summary>
/// <remarks>
/// A tree rather than a state machine because five enemy roles share most of their
/// behaviour — chase, leash, attack when in range — and differ in a few branches. With
/// ~55 enemies at v1.0, composing shared subtrees is what keeps that from becoming
/// fifty-five copies of the same code.
/// </remarks>
public abstract class BtNode<T>
{
    public abstract BtStatus Tick(T context, double delta);

    /// <summary>Clears any in-progress state. Called when a branch is abandoned.</summary>
    public virtual void Reset() { }
}

/// <summary>Runs children in order until one succeeds or is still running. Logical OR.</summary>
public sealed class BtSelector<T>(params BtNode<T>[] children) : BtNode<T>
{
    private readonly BtNode<T>[] _children = children;
    private int _running = -1;

    public override BtStatus Tick(T context, double delta)
    {
        for (var i = 0; i < _children.Length; i++)
        {
            // A node that was running gets priority, but higher-priority siblings are
            // still evaluated first — that is what lets an urgent branch interrupt.
            var status = _children[i].Tick(context, delta);

            if (status == BtStatus.Failure) continue;

            if (_running >= 0 && _running != i)
            {
                _children[_running].Reset();
            }

            _running = status == BtStatus.Running ? i : -1;
            return status;
        }

        ResetRunning();
        return BtStatus.Failure;
    }

    public override void Reset()
    {
        ResetRunning();

        foreach (var child in _children)
        {
            child.Reset();
        }
    }

    private void ResetRunning()
    {
        if (_running >= 0)
        {
            _children[_running].Reset();
            _running = -1;
        }
    }
}

/// <summary>Runs children in order until one fails. Logical AND.</summary>
public sealed class BtSequence<T>(params BtNode<T>[] children) : BtNode<T>
{
    private readonly BtNode<T>[] _children = children;
    private int _index;

    public override BtStatus Tick(T context, double delta)
    {
        while (_index < _children.Length)
        {
            var status = _children[_index].Tick(context, delta);

            if (status == BtStatus.Running) return BtStatus.Running;

            if (status == BtStatus.Failure)
            {
                Reset();
                return BtStatus.Failure;
            }

            _index++;
        }

        Reset();
        return BtStatus.Success;
    }

    public override void Reset()
    {
        _index = 0;

        foreach (var child in _children)
        {
            child.Reset();
        }
    }
}

/// <summary>Succeeds when the predicate holds. Leaves are the only place logic lives.</summary>
public sealed class BtCondition<T>(Func<T, bool> predicate) : BtNode<T>
{
    public override BtStatus Tick(T context, double delta) =>
        predicate(context) ? BtStatus.Success : BtStatus.Failure;
}

/// <summary>Does something. May report Running across several ticks.</summary>
public sealed class BtAction<T>(Func<T, double, BtStatus> action, Action<T>? onReset = null) : BtNode<T>
{
    private T? _last;

    public override BtStatus Tick(T context, double delta)
    {
        _last = context;
        return action(context, delta);
    }

    public override void Reset()
    {
        if (onReset is not null && _last is not null) onReset(_last);
        _last = default;
    }
}

/// <summary>Blocks its child for a period after it succeeds.</summary>
public sealed class BtCooldown<T>(double seconds, BtNode<T> child) : BtNode<T>
{
    private double _remaining;

    public double Remaining => Math.Max(0, _remaining);

    public override BtStatus Tick(T context, double delta)
    {
        if (_remaining > 0)
        {
            _remaining -= delta;
            return BtStatus.Failure;
        }

        var status = child.Tick(context, delta);

        // Only a completed action starts the cooldown; a branch that failed or is still
        // running must stay available.
        if (status == BtStatus.Success) _remaining = seconds;

        return status;
    }

    public override void Reset()
    {
        child.Reset();
    }

    /// <summary>Makes the child available immediately.</summary>
    public void Clear() => _remaining = 0;
}

/// <summary>Turns Success into Failure and back. Running passes through.</summary>
public sealed class BtInverter<T>(BtNode<T> child) : BtNode<T>
{
    public override BtStatus Tick(T context, double delta) => child.Tick(context, delta) switch
    {
        BtStatus.Success => BtStatus.Failure,
        BtStatus.Failure => BtStatus.Success,
        _ => BtStatus.Running,
    };

    public override void Reset() => child.Reset();
}

/// <summary>Swallows failure, so a sequence can continue past an optional step.</summary>
public sealed class BtOptional<T>(BtNode<T> child) : BtNode<T>
{
    public override BtStatus Tick(T context, double delta) => child.Tick(context, delta) switch
    {
        BtStatus.Running => BtStatus.Running,
        _ => BtStatus.Success,
    };

    public override void Reset() => child.Reset();
}
