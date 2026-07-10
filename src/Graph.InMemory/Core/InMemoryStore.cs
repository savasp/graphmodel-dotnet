// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory;

/// <summary>
/// The mutable cell holding the current committed <see cref="StoreState"/>. All commits are
/// serialized through one lock and applied by replaying the transaction's buffered mutations
/// against the latest committed state, so concurrent transactions cannot lose each other's
/// writes. Reads take the current snapshot without locking beyond the field read.
/// </summary>
internal sealed class InMemoryStore
{
    private readonly Lock _gate = new();
    private StoreState _state = StoreState.Empty;

    /// <summary>Gets the current committed snapshot.</summary>
    public StoreState CurrentState
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>
    /// Applies the given mutations atomically against the latest committed state. Each mutation
    /// re-validates against that state, so a conflicting concurrent commit surfaces as the
    /// mutation's own <see cref="GraphException"/> rather than silently overwriting.
    /// </summary>
    public void Commit(IReadOnlyList<Func<StoreState, StoreState>> mutations)
    {
        lock (_gate)
        {
            var state = _state;
            foreach (var mutation in mutations)
            {
                state = mutation(state);
            }

            _state = state;
        }
    }

    /// <summary>Resets the store to empty.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _state = StoreState.Empty;
        }
    }
}
