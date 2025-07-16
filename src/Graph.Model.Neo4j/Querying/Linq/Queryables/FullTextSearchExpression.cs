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
using Cvoya.Graph.Model;

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
    public override Type Type
    {
        get
        {
            // Return the appropriate graph queryable type based on the entity type
            if (typeof(INode).IsAssignableFrom(EntityType))
            {
                return typeof(IGraphNodeQueryable<>).MakeGenericType(EntityType);
            }
            else if (typeof(IRelationship).IsAssignableFrom(EntityType))
            {
                return typeof(IGraphRelationshipQueryable<>).MakeGenericType(EntityType);
            }
            else
            {
                // For general entities, return the base graph queryable type
                return typeof(IGraphQueryable<>).MakeGenericType(EntityType);
            }
        }
    }
}

/// <summary>
/// Represents a full text search operation on a LINQ queryable
/// </summary>
internal class SearchExpression : Expression
{
    public string SearchQuery { get; }
    public Expression Source { get; }

    public SearchExpression(Expression source, string searchQuery)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        SearchQuery = searchQuery ?? throw new ArgumentNullException(nameof(searchQuery));
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => Source.Type;
}