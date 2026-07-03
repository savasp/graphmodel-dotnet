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

namespace Cvoya.Graph.Model.Neo4j.Translation.Tests.Harness;

using System.Linq.Expressions;

/// <summary>
/// Entry points for building root <see cref="IGraphQueryable{T}"/> instances backed by
/// <see cref="FakeGraphQueryProvider"/>. This mirrors how the real
/// <c>GraphNodeQueryable&lt;TNode&gt;</c>/<c>GraphRelationshipQueryable&lt;TRel&gt;</c> build a
/// root expression: a constant carrying a placeholder queryable whose only purpose is to give
/// <c>CypherQueryVisitor.VisitConstant</c> the element type and node-vs-relationship kind.
/// </summary>
internal static class Root
{
    public static IGraphNodeQueryable<T> Nodes<T>() where T : INode
    {
        var provider = new FakeGraphQueryProvider();
        var placeholder = new FakeGraphNodeQueryable<T>(provider, Expression.Constant(null, typeof(Expression)));
        var expression = Expression.Constant(placeholder, typeof(IGraphNodeQueryable<T>));
        return new FakeGraphNodeQueryable<T>(provider, expression);
    }

    public static IGraphRelationshipQueryable<T> Relationships<T>() where T : IRelationship
    {
        var provider = new FakeGraphQueryProvider();
        var placeholder = new FakeGraphRelationshipQueryable<T>(provider, Expression.Constant(null, typeof(Expression)));
        var expression = Expression.Constant(placeholder, typeof(IGraphRelationshipQueryable<T>));
        return new FakeGraphRelationshipQueryable<T>(provider, expression);
    }
}
