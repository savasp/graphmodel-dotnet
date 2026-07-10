// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory;

/// <summary>
/// The mutable cell holding the current committed <see cref="StoreState"/>. All commits are
/// serialized through one lock and applied by replaying the transaction's buffered mutations
/// against the latest committed state, so disjoint concurrent writes are preserved instead of
/// being overwritten by a snapshot state-swap. Reads take the current snapshot without locking
/// beyond the field read.
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
    /// re-validates its own invariants against that state, so duplicate ids, missing endpoints,
    /// and constraint conflicts surface through the mutation's <see cref="GraphException"/>.
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
