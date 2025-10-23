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

namespace Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;

using Cvoya.Graph.Model.Cypher.Querying.Cypher.Builders;

internal sealed class CypherQueryScope(Type rootType) : ICypherQueryScope
{
    private readonly Dictionary<Type, string> typeAliases = [];
    private readonly Dictionary<string, Type> aliasTypes = [];
    private readonly Stack<string> aliasStack = new();
    private int aliasCounter;

    public Type RootType { get; } = rootType;

    public string? CurrentAlias { get; set; }

    public Type? CurrentType { get; set; }

    public bool IsPathSegmentContext { get; set; }

    public bool PathSegmentPatternsFinalized { get; set; }

    public int? TraversalMinDepth { get; private set; }

    public int? TraversalMaxDepth { get; private set; }

    public TraversalInfo? TraversalInfo { get; private set; }

    public string? GroupByExpression { get; private set; }

    public void SetTraversalDepth(int minDepth, int maxDepth)
    {
        TraversalMinDepth = minDepth;
        TraversalMaxDepth = maxDepth;
    }

    public void ClearTraversalDepth()
    {
        TraversalMinDepth = null;
        TraversalMaxDepth = null;
    }

    public void PushAlias(string alias)
    {
        aliasStack.Push(CurrentAlias ?? string.Empty);
        CurrentAlias = alias;
    }

    public void PopAlias()
    {
        if (aliasStack.Count > 0)
        {
            CurrentAlias = aliasStack.Pop();
            if (string.IsNullOrEmpty(CurrentAlias))
            {
                CurrentAlias = null;
            }
        }
    }

    public string GetOrCreateAlias(Type type, string? preferredAlias = null)
    {
        if (typeAliases.TryGetValue(type, out var alias))
        {
            if (alias == preferredAlias)
            {
                return string.Concat(alias);
            }
        }

        alias = preferredAlias ?? GenerateAlias(type);

        while (aliasTypes.ContainsKey(alias))
        {
            alias = $"{alias}_{++aliasCounter}";
        }

        typeAliases[type] = alias;
        aliasTypes[alias] = type;

        return alias;
    }

    public Type? GetTypeForAlias(string alias)
        => aliasTypes.GetValueOrDefault(alias);

    public string? GetAliasForType(Type type)
        => typeAliases.GetValueOrDefault(type);

    public void SetTraversalInfo(Type sourceType, Type relationshipType, Type targetNodeType)
    {
        TraversalInfo = new TraversalInfo(sourceType, relationshipType, targetNodeType);
    }

    public void SetGroupByExpression(string expression)
    {
        GroupByExpression = expression;
    }

    public bool IsInPathSegmentContext()
        => IsPathSegmentContext;

    private static string GenerateAlias(Type type)
    {
        var name = type.Name;

        if (name.StartsWith("I", StringComparison.Ordinal) && name.Length > 1 && char.IsUpper(name[1]))
        {
            name = name[1..];
        }

        return char.ToLower(name[0]).ToString();
    }
}
