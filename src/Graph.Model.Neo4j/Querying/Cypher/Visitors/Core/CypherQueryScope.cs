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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;

internal sealed class CypherQueryScope(Type rootType)
{
    private readonly Dictionary<Type, string> _typeAliases = [];
    private readonly Dictionary<string, Type> _aliasTypes = [];
    private readonly Stack<string> _aliasStack = new();
    private int _aliasCounter = 0;

    /// <summary>
    /// Gets the root type of the query context.
    /// </summary>
    public Type RootType { get; } = rootType;

    /// <summary>
    /// Gets or sets the current alias being used in the query context.
    /// This changes as we traverse through different parts of the expression tree.
    /// </summary>
    public string? CurrentAlias { get; set; }

    /// <summary>
    /// Gets or sets the current type being queried.
    /// This is used to determine the type of the current context in the query.
    /// </summary>
    public Type? CurrentType { get; set; }

    /// <summary>
    /// Gets or sets whether the current context is a path segment.
    /// This is used to determine if we are currently processing a path segment in the query.
    /// </summary>
    public bool IsPathSegmentContext { get; set; }

    /// <summary>
    /// Gets or sets whether the path segment patterns have been finalized.
    /// This is used to ensure that path segment patterns are only finalized once.
    /// </summary>
    public bool PathSegmentPatternsFinalized { get; set; }

    /// <summary>
    /// Gets the minimum traversal depth for path patterns.
    /// </summary>
    public int? TraversalMinDepth { get; private set; }

    /// <summary>
    /// Gets the maximum traversal depth for path patterns.
    /// </summary>
    public int? TraversalMaxDepth { get; private set; }

    /// <summary>
    /// Gets information about the current traversal operation.
    /// </summary>
    public TraversalInfo? TraversalInfo { get; private set; }

    /// <summary>
    /// Gets the GROUP BY expression for use in g.Key references.
    /// </summary>
    public string? GroupByExpression { get; private set; }

    /// <summary>
    /// Sets the traversal depth constraints for path patterns.
    /// </summary>
    public void SetTraversalDepth(int minDepth, int maxDepth)
    {
        TraversalMinDepth = minDepth;
        TraversalMaxDepth = maxDepth;
    }

    /// <summary>
    /// Clears the traversal depth constraints.
    /// </summary>
    public void ClearTraversalDepth()
    {
        TraversalMinDepth = null;
        TraversalMaxDepth = null;
    }

    /// <summary>
    /// Pushes a new alias onto the stack and sets it as current.
    /// </summary>
    public void PushAlias(string alias)
    {
        _aliasStack.Push(CurrentAlias ?? string.Empty);
        CurrentAlias = alias;
    }

    /// <summary>
    /// Pops the previous alias from the stack and restores it.
    /// </summary>
    public void PopAlias()
    {
        if (_aliasStack.Count > 0)
        {
            CurrentAlias = _aliasStack.Pop();
            if (string.IsNullOrEmpty(CurrentAlias))
            {
                CurrentAlias = null;
            }
        }
    }

    public string GetOrCreateAlias(Type type, string? preferredAlias = null)
    {
        if (_typeAliases.TryGetValue(type, out var alias))
        {
            if (alias == preferredAlias)
            {
                return string.Concat(alias);
            }
        }

        alias = preferredAlias ?? GenerateAlias(type);

        // Ensure uniqueness
        while (_aliasTypes.ContainsKey(alias))
        {
            alias = $"{alias}_{++_aliasCounter}";
        }

        _typeAliases[type] = alias;
        _aliasTypes[alias] = type;

        return alias;
    }

    public Type? GetTypeForAlias(string alias)
    {
        return _aliasTypes.GetValueOrDefault(alias);
    }

    public string? GetAliasForType(Type type)
    {
        return _typeAliases.GetValueOrDefault(type);
    }

    /// <summary>
    /// Sets traversal information.
    /// </summary>
    public void SetTraversalInfo(Type sourceType, Type relationshipType, Type targetNodeType)
    {
        TraversalInfo = new TraversalInfo(sourceType, relationshipType, targetNodeType);
    }

    /// <summary>
    /// Sets the GROUP BY expression for later use in g.Key references.
    /// </summary>
    public void SetGroupByExpression(string expression)
    {
        GroupByExpression = expression;
    }

    private string GenerateAlias(Type type)
    {
        var name = type.Name;

        // Remove interface prefix
        if (name.StartsWith("I") && name.Length > 1 && char.IsUpper(name[1]))
        {
            name = name[1..];
        }

        // Take first letter and make it lowercase
        return char.ToLower(name[0]).ToString();
    }
}
