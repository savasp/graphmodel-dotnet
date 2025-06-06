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
/// Interface for building and executing graph pattern matching queries
/// </summary>
/// <typeparam name="T">The type of the starting entity</typeparam>
public interface IGraphPattern<T> where T : class, IEntity, new()
{
    /// <summary>
    /// Adds a node pattern to match
    /// </summary>
    /// <typeparam name="TNode">The type of node to match</typeparam>
    /// <param name="alias">The alias for this node in the pattern</param>
    /// <returns>A pattern builder for the specified node type</returns>
    INodePattern<TNode> Node<TNode>(string alias) where TNode : class, INode, new();

    /// <summary>
    /// Adds a relationship pattern to match
    /// </summary>
    /// <typeparam name="TRel">The type of relationship to match</typeparam>
    /// <param name="alias">The alias for this relationship in the pattern</param>
    /// <returns>A pattern builder for the specified relationship type</returns>
    IRelationshipPattern<TRel> Relationship<TRel>(string alias) where TRel : class, IRelationship, new();

    /// <summary>
    /// Adds a path pattern to match
    /// </summary>
    /// <param name="alias">The alias for this path in the pattern</param>
    /// <returns>A pattern builder for the path</returns>
    IPathPattern Path(string alias);

    /// <summary>
    /// Adds a conditional pattern that must be satisfied
    /// </summary>
    /// <param name="condition">The condition expression</param>
    /// <returns>The pattern with the added condition</returns>
    IGraphPattern<T> Where(Expression<Func<IPatternContext, bool>> condition);

    /// <summary>
    /// Adds an optional pattern that may or may not match
    /// </summary>
    /// <param name="pattern">The optional pattern</param>
    /// <returns>The pattern with the added optional clause</returns>
    IGraphPattern<T> Optional(Action<IGraphPattern<T>> pattern);

    /// <summary>
    /// Unions this pattern with another pattern
    /// </summary>
    /// <param name="other">The other pattern to union with</param>
    /// <returns>A pattern representing the union</returns>
    IGraphPattern<T> Union(IGraphPattern<T> other);

    /// <summary>
    /// Creates a subquery within this pattern
    /// </summary>
    /// <param name="subquery">The subquery pattern</param>
    /// <returns>The pattern with the added subquery</returns>
    IGraphPattern<T> Subquery(Action<IGraphPattern<T>> subquery);

    /// <summary>
    /// Executes the pattern and returns matching results
    /// </summary>
    /// <typeparam name="TResult">The type of result to project</typeparam>
    /// <param name="selector">The projection expression</param>
    /// <returns>A queryable for the pattern results</returns>
    IGraphQueryable<TResult> Select<TResult>(Expression<Func<IPatternContext, TResult>> selector) where TResult : class;

    /// <summary>
    /// Executes the pattern and returns all matches
    /// </summary>
    /// <returns>A queryable for the pattern matches</returns>
    IGraphQueryable<IPatternMatch> Matches();

    /// <summary>
    /// Limits the number of pattern matches returned
    /// </summary>
    /// <param name="count">The maximum number of matches</param>
    /// <returns>The pattern with the limit applied</returns>
    IGraphPattern<T> Limit(int count);

    /// <summary>
    /// Skips a number of pattern matches
    /// </summary>
    /// <param name="count">The number of matches to skip</param>
    /// <returns>The pattern with the skip applied</returns>
    IGraphPattern<T> Skip(int count);

    /// <summary>
    /// Orders pattern matches by a specified expression
    /// </summary>
    /// <typeparam name="TKey">The type of the ordering key</typeparam>
    /// <param name="keySelector">The key selection expression</param>
    /// <returns>The pattern with ordering applied</returns>
    IGraphPattern<T> OrderBy<TKey>(Expression<Func<IPatternContext, TKey>> keySelector);

    /// <summary>
    /// Orders pattern matches by a specified expression in descending order
    /// </summary>
    /// <typeparam name="TKey">The type of the ordering key</typeparam>
    /// <param name="keySelector">The key selection expression</param>
    /// <returns>The pattern with descending ordering applied</returns>
    IGraphPattern<T> OrderByDescending<TKey>(Expression<Func<IPatternContext, TKey>> keySelector);
}

/// <summary>
/// Interface for building node patterns
/// </summary>
/// <typeparam name="TNode">The type of node being matched</typeparam>
public interface INodePattern<TNode> where TNode : class, INode, new()
{
    /// <summary>
    /// Adds a property constraint to this node pattern
    /// </summary>
    /// <param name="predicate">The property constraint</param>
    /// <returns>The node pattern with the constraint added</returns>
    INodePattern<TNode> Where(Expression<Func<TNode, bool>> predicate);

    /// <summary>
    /// Adds a label constraint to this node pattern
    /// </summary>
    /// <param name="labels">The labels that the node must have</param>
    /// <returns>The node pattern with the label constraint added</returns>
    INodePattern<TNode> HasLabels(params string[] labels);

    /// <summary>
    /// Connects this node to another node via a relationship
    /// </summary>
    /// <typeparam name="TRel">The type of relationship</typeparam>
    /// <typeparam name="TTarget">The type of target node</typeparam>
    /// <param name="relationshipAlias">The alias for the relationship</param>
    /// <param name="targetAlias">The alias for the target node</param>
    /// <returns>A relationship pattern builder</returns>
    IRelationshipPattern<TRel> ConnectedTo<TRel, TTarget>(string relationshipAlias, string targetAlias)
        where TRel : class, IRelationship, new()
        where TTarget : class, INode, new();

    /// <summary>
    /// Specifies that this node is connected by incoming relationships
    /// </summary>
    /// <typeparam name="TRel">The type of relationship</typeparam>
    /// <param name="relationshipAlias">The alias for the relationship</param>
    /// <returns>A relationship pattern builder</returns>
    IRelationshipPattern<TRel> IncomingRelationship<TRel>(string relationshipAlias)
        where TRel : class, IRelationship, new();

    /// <summary>
    /// Specifies that this node is connected by outgoing relationships
    /// </summary>
    /// <typeparam name="TRel">The type of relationship</typeparam>
    /// <param name="relationshipAlias">The alias for the relationship</param>
    /// <returns>A relationship pattern builder</returns>
    IRelationshipPattern<TRel> OutgoingRelationship<TRel>(string relationshipAlias)
        where TRel : class, IRelationship, new();
}

/// <summary>
/// Interface for building relationship patterns
/// </summary>
/// <typeparam name="TRel">The type of relationship being matched</typeparam>
public interface IRelationshipPattern<TRel> where TRel : class, IRelationship, new()
{
    /// <summary>
    /// Adds a property constraint to this relationship pattern
    /// </summary>
    /// <param name="predicate">The property constraint</param>
    /// <returns>The relationship pattern with the constraint added</returns>
    IRelationshipPattern<TRel> Where(Expression<Func<TRel, bool>> predicate);

    /// <summary>
    /// Specifies the direction of this relationship
    /// </summary>
    /// <param name="direction">The relationship direction</param>
    /// <returns>The relationship pattern with the direction constraint</returns>
    IRelationshipPattern<TRel> InDirection(PatternRelationshipDirection direction);

    /// <summary>
    /// Specifies a length constraint for variable-length paths
    /// </summary>
    /// <param name="minLength">The minimum path length</param>
    /// <param name="maxLength">The maximum path length</param>
    /// <returns>The relationship pattern with the length constraint</returns>
    IRelationshipPattern<TRel> WithLength(int minLength, int maxLength);

    /// <summary>
    /// Connects this relationship to a target node
    /// </summary>
    /// <typeparam name="TTarget">The type of target node</typeparam>
    /// <param name="targetAlias">The alias for the target node</param>
    /// <returns>A node pattern builder for the target</returns>
    INodePattern<TTarget> To<TTarget>(string targetAlias) where TTarget : class, INode, new();

    /// <summary>
    /// Connects this relationship from a source node
    /// </summary>
    /// <typeparam name="TSource">The type of source node</typeparam>
    /// <param name="sourceAlias">The alias for the source node</param>
    /// <returns>A node pattern builder for the source</returns>
    INodePattern<TSource> From<TSource>(string sourceAlias) where TSource : class, INode, new();
}

/// <summary>
/// Interface for building path patterns
/// </summary>
public interface IPathPattern
{
    /// <summary>
    /// Specifies the minimum length for this path
    /// </summary>
    /// <param name="length">The minimum length</param>
    /// <returns>The path pattern with the minimum length constraint</returns>
    IPathPattern MinLength(int length);

    /// <summary>
    /// Specifies the maximum length for this path
    /// </summary>
    /// <param name="length">The maximum length</param>
    /// <returns>The path pattern with the maximum length constraint</returns>
    IPathPattern MaxLength(int length);

    /// <summary>
    /// Specifies an exact length for this path
    /// </summary>
    /// <param name="length">The exact length</param>
    /// <returns>The path pattern with the exact length constraint</returns>
    IPathPattern ExactLength(int length);

    /// <summary>
    /// Specifies that this should be the shortest path
    /// </summary>
    /// <returns>The path pattern configured for shortest path</returns>
    IPathPattern Shortest();

    /// <summary>
    /// Specifies that all paths should be found
    /// </summary>
    /// <returns>The path pattern configured to find all paths</returns>
    IPathPattern All();

    /// <summary>
    /// Connects this path between two nodes
    /// </summary>
    /// <typeparam name="TSource">The type of source node</typeparam>
    /// <typeparam name="TTarget">The type of target node</typeparam>
    /// <param name="sourceAlias">The alias for the source node</param>
    /// <param name="targetAlias">The alias for the target node</param>
    /// <returns>A path pattern builder</returns>
    IPathPattern Between<TSource, TTarget>(string sourceAlias, string targetAlias)
        where TSource : class, INode, new()
        where TTarget : class, INode, new();
}

/// <summary>
/// Context for pattern matching operations providing access to matched entities
/// </summary>
public interface IPatternContext
{
    /// <summary>
    /// Gets a matched node by its alias
    /// </summary>
    /// <typeparam name="TNode">The type of the node</typeparam>
    /// <param name="alias">The alias of the node in the pattern</param>
    /// <returns>The matched node</returns>
    TNode Node<TNode>(string alias) where TNode : class, INode, new();

    /// <summary>
    /// Gets a matched relationship by its alias
    /// </summary>
    /// <typeparam name="TRel">The type of the relationship</typeparam>
    /// <param name="alias">The alias of the relationship in the pattern</param>
    /// <returns>The matched relationship</returns>
    TRel Relationship<TRel>(string alias) where TRel : class, IRelationship, new();

    /// <summary>
    /// Gets a matched path by its alias
    /// </summary>
    /// <param name="alias">The alias of the path in the pattern</param>
    /// <returns>The matched path</returns>
    IGraphMultiPath Path(string alias);

    /// <summary>
    /// Gets all aliases defined in this pattern context
    /// </summary>
    IReadOnlyList<string> Aliases { get; }

    /// <summary>
    /// Checks if an alias exists in this pattern context
    /// </summary>
    /// <param name="alias">The alias to check</param>
    /// <returns>True if the alias exists, false otherwise</returns>
    bool HasAlias(string alias);
}

/// <summary>
/// Represents a complete pattern match result
/// </summary>
public interface IPatternMatch
{
    /// <summary>
    /// Gets the pattern context for this match
    /// </summary>
    IPatternContext Context { get; }

    /// <summary>
    /// Gets the score of this match (if scoring is enabled)
    /// </summary>
    double? Score { get; }

    /// <summary>
    /// Gets metadata about this pattern match
    /// </summary>
    IPatternMatchMetadata Metadata { get; }
}

/// <summary>
/// Metadata about a pattern match
/// </summary>
public interface IPatternMatchMetadata
{
    /// <summary>
    /// Gets the unique identifier for this match
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the execution time for finding this match
    /// </summary>
    TimeSpan ExecutionTime { get; }

    /// <summary>
    /// Gets the number of entities examined to find this match
    /// </summary>
    long EntitiesExamined { get; }

    /// <summary>
    /// Gets additional properties associated with this match
    /// </summary>
    IReadOnlyDictionary<string, object> Properties { get; }
}

/// <summary>
/// Direction for relationship patterns in graph queries
/// </summary>
public enum PatternRelationshipDirection
{
    /// <summary>Outgoing relationship</summary>
    Outgoing,

    /// <summary>Incoming relationship</summary>
    Incoming,

    /// <summary>Bidirectional relationship</summary>
    Both,

    /// <summary>Any direction</summary>
    Any
}