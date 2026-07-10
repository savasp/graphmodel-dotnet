// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory;

/// <summary>
/// A buffered-write transaction over an <see cref="InMemoryStore"/>: mutations apply eagerly to
/// a private view (so the transaction reads its own writes and validation errors surface at the
/// call site) and are replayed atomically against the latest committed state on commit. Rollback
/// and disposal discard the buffer; uncommitted work never reaches the store.
/// </summary>
internal sealed class InMemoryTransaction : IGraphTransaction
{
    private readonly InMemoryStore _store;
    private readonly List<Func<StoreState, StoreState>> _mutations = [];
    private StoreState _view;
    private bool _committed;
    private bool _rolledBack;
    private bool _disposed;

    public InMemoryTransaction(InMemoryStore store)
    {
        _store = store;
        _view = store.CurrentState;
    }

    /// <summary>Gets whether the transaction can still accept work.</summary>
    public bool IsActive => !_committed && !_rolledBack && !_disposed;

    /// <summary>Gets whether this transaction belongs to the given store.</summary>
    public bool BelongsTo(InMemoryStore store) => ReferenceEquals(_store, store);

    /// <summary>
    /// Gets the snapshot this transaction reads from: the base snapshot taken at begin, plus the
    /// transaction's own buffered writes.
    /// </summary>
    public StoreState View
    {
        get
        {
            ThrowIfNotActive();
            return _view;
        }
    }

    /// <summary>
    /// Applies a mutation to the private view (surfacing validation failures immediately) and
    /// buffers it for replay at commit.
    /// </summary>
    public void Apply(Func<StoreState, StoreState> mutation)
    {
        ThrowIfNotActive();
        _view = mutation(_view);
        _mutations.Add(mutation);
    }

    /// <inheritdoc />
    public Task CommitAsync()
    {
        ThrowIfNotActive();
        _store.Commit(_mutations);
        _committed = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RollbackAsync()
    {
        ThrowIfNotActive();
        _mutations.Clear();
        _rolledBack = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (IsActive)
            {
                // Dispose without commit rolls back, mirroring the public transaction contract.
                _mutations.Clear();
                _rolledBack = true;
            }

            _disposed = true;
        }

        return ValueTask.CompletedTask;
    }

    private void ThrowIfNotActive()
    {
        if (!IsActive)
        {
            throw new GraphException("Transaction is not active.");
        }
    }
}
