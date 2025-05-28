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

using System.Linq.Expressions;

namespace Cvoya.Graph.Model;

/// <summary>
/// Represents a graph traversal operation.
/// </summary>
public interface IGraphTraversal<TNode, TRelationship>
    where TNode : class, INode, new()
    where TRelationship : class, IRelationship, new()
{
    /// <summary>
    /// Filters relationships during traversal.
    /// </summary>
    IGraphTraversal<TNode, TRelationship> Where(Expression<Func<TRelationship, bool>> predicate);

    /// <summary>
    /// Specifies the target node type to reach.
    /// </summary>
    IQueryable<TTargetNode> To<TTargetNode>() where TTargetNode : class, INode, new();

    /// <summary>
    /// Specifies the target nodes with a filter.
    /// </summary>
    IQueryable<TTargetNode> To<TTargetNode>(Expression<Func<TTargetNode, bool>> targetFilter)
        where TTargetNode : class, INode, new();

    /// <summary>
    /// Returns the relationships found during traversal.
    /// </summary>
    IQueryable<TRelationship> Relationships();

    /// <summary>
    /// Returns paths found during traversal.
    /// </summary>
    IQueryable<GraphPath<TNode>> Paths();

    /// <summary>
    /// Limits the traversal depth.
    /// </summary>
    IGraphTraversal<TNode, TRelationship> WithDepth(int minDepth, int maxDepth);
}

/// <summary>
/// Represents a graph expansion operation.
/// </summary>
public interface IGraphExpansion<TNode> where TNode : class, INode, new()
{
    /// <summary>
    /// Includes related nodes via a specific relationship type.
    /// </summary>
    IGraphExpansion<TNode> Include<TRelationship, TTargetNode>()
        where TRelationship : class, IRelationship, new()
        where TTargetNode : class, INode, new();

    /// <summary>
    /// Includes related nodes with a custom pattern.
    /// </summary>
    IGraphExpansion<TNode> Include(string pattern);

    /// <summary>
    /// Executes the expansion and returns the results.
    /// </summary>
    IQueryable<GraphResult<TNode>> Execute();
}

/// <summary>
/// Represents a graph pattern matching operation.
/// </summary>
public interface IGraphPattern<TNode> where TNode : class, INode, new()
{
    /// <summary>
    /// Binds a variable in the pattern to a type.
    /// </summary>
    IGraphPattern<TNode> Bind<T>(string variable) where T : class, IEntity, new();

    /// <summary>
    /// Adds a WHERE clause to the pattern.
    /// </summary>
    IGraphPattern<TNode> Where(string condition);

    /// <summary>
    /// Executes the pattern and returns results.
    /// </summary>
    IQueryable<T> Return<T>() where T : class, new();
}

/// <summary>
/// Represents a path in the graph.
/// </summary>
public class GraphPath<TNode> where TNode : class, INode, new()
{
    public required IReadOnlyList<TNode> Nodes { get; init; }
    public required IReadOnlyList<IRelationship> Relationships { get; init; }
    public int Length => Relationships.Count;
}

/// <summary>
/// Represents an expanded graph result.
/// </summary>
public class GraphResult<TNode> where TNode : class, INode, new()
{
    public required TNode Root { get; init; }
    public required IReadOnlyDictionary<string, IEnumerable<IEntity>> RelatedEntities { get; init; }
}