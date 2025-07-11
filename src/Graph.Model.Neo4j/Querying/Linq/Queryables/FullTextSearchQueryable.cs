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

namespace Cvoya.Graph.Model.Neo4j.Querying.Linq.Queryables;

using System.Linq.Expressions;
using Cvoya.Graph.Model.Neo4j.Core;
using Cvoya.Graph.Model.Neo4j.Querying.Linq.Providers;

/// <summary>
/// Represents a full text search query expression
/// </summary>
internal class FullTextSearchExpression : Expression
{
    public string SearchQuery { get; }
    public Type EntityType { get; }

    public FullTextSearchExpression(string searchQuery, Type entityType)
    {
        SearchQuery = searchQuery;
        EntityType = entityType;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => typeof(IQueryable<>).MakeGenericType(EntityType);
}

/// <summary>
/// Static factory methods for creating full text search queryables
/// </summary>
internal static class FullTextSearchQueryableFactory
{
    public static GraphQueryable<T> CreateSearchQueryable<T>(
        GraphQueryProvider provider,
        GraphTransaction transaction,
        GraphContext context,
        string searchQuery)
    {
        var searchExpression = new FullTextSearchExpression(searchQuery, typeof(T));
        return new GraphQueryable<T>(provider, context, transaction, searchExpression);
    }

    public static GraphNodeQueryable<T> CreateNodeSearchQueryable<T>(
        GraphQueryProvider provider,
        GraphTransaction transaction,
        GraphContext context,
        string searchQuery)
        where T : INode
    {
        var searchExpression = new FullTextSearchExpression(searchQuery, typeof(T));
        return new GraphNodeQueryable<T>(provider, context, transaction, searchExpression);
    }

    public static GraphRelationshipQueryable<T> CreateRelationshipSearchQueryable<T>(
        GraphQueryProvider provider,
        GraphTransaction transaction,
        GraphContext context,
        string searchQuery)
        where T : IRelationship
    {
        var searchExpression = new FullTextSearchExpression(searchQuery, typeof(T));
        return new GraphRelationshipQueryable<T>(provider, context, transaction, searchExpression);
    }
}