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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Expressions;

using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;

/// <summary>
/// Builder for creating expression visitor chains.
/// </summary>
internal class ExpressionVisitorChainBuilder(CypherQueryContext context)
{
    private CypherQueryContext _context = context;
    private readonly List<Func<ICypherExpressionVisitor?, ICypherExpressionVisitor>> _visitorFactories = [];

    public ExpressionVisitorChainBuilder AddBase()
    {
        _visitorFactories.Add(next => new BaseExpressionVisitor(_context, next));
        return this;
    }

    public ExpressionVisitorChainBuilder AddBinary()
    {
        _visitorFactories.Add(next => new BinaryExpressionVisitor(_context, next!));
        return this;
    }

    public ExpressionVisitorChainBuilder AddStringMethods()
    {
        _visitorFactories.Add(next => new StringMethodVisitor(_context, next!));
        return this;
    }

    public ExpressionVisitorChainBuilder AddCollectionMethods()
    {
        _visitorFactories.Add(next => new CollectionMethodVisitor(_context, next!));
        return this;
    }

    public ExpressionVisitorChainBuilder AddDateTimeMethods()
    {
        _visitorFactories.Add(next => new DateTimeMethodVisitor(_context, next!));
        return this;
    }

    public ExpressionVisitorChainBuilder AddConversions()
    {
        _visitorFactories.Add(next => new ConversionVisitor(_context, next!));
        return this;
    }

    public ExpressionVisitorChainBuilder AddAggregations()
    {
        _visitorFactories.Add(next => new AggregationMethodVisitor(_context, next));
        return this;
    }

    public ExpressionVisitorChainBuilder AddCustom(Func<ICypherExpressionVisitor?, ICypherExpressionVisitor> factory)
    {
        _visitorFactories.Add(factory);
        return this;
    }

    public ICypherExpressionVisitor Build()
    {
        if (_context.Scope == null || _context.Builder == null)
        {
            throw new InvalidOperationException("Scope and Builder must be set before building the chain");
        }

        // Build the chain in reverse order (last added will be first in chain)
        ICypherExpressionVisitor? current = null;
        for (int i = _visitorFactories.Count - 1; i >= 0; i--)
        {
            current = _visitorFactories[i](current);
        }

        return current ?? throw new InvalidOperationException("No visitors were added to the chain");
    }
}