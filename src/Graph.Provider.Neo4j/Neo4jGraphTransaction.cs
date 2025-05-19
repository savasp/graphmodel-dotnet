// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Cvoya.Graph.Provider.Model;
using Neo4j.Driver;

namespace Cvoya.Graph.Provider.Neo4j;

internal class Neo4jGraphTransaction : IGraphTransaction
{
    private readonly IAsyncSession _session;
    private IAsyncTransaction? _transaction;
    private bool _committed;
    private bool _rolledBack;

    public Neo4jGraphTransaction(IAsyncSession session, IAsyncTransaction transaction)
    {
        _session = session;
        _transaction = transaction;
    }

    public bool IsActive => _transaction != null && !_committed && !_rolledBack;

    internal IAsyncSession Session => _session;

    public async Task CommitAsync()
    {
        if (_transaction == null || _committed || _rolledBack)
            throw new InvalidOperationException("Transaction is not active.");
        await _transaction.CommitAsync();
        _committed = true;
        await _session.CloseAsync();
        _transaction = null;
    }

    public async Task RollbackAsync()
    {
        if (_transaction == null || _committed || _rolledBack)
            throw new InvalidOperationException("Transaction is not active.");
        await _transaction.RollbackAsync();
        _rolledBack = true;
        await _session.CloseAsync();
        _transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction != null && !_committed && !_rolledBack)
        {
            await _transaction.RollbackAsync();
        }
        await _session.CloseAsync();
        _transaction = null;
    }

    public IAsyncTransaction? GetTransaction() => _transaction;
}
