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

namespace Cvoya.Graph.Model.Querying;

/// <summary>
/// Represents a full-text search query root.
/// </summary>
public sealed record SearchRoot : QueryRoot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SearchRoot"/> record.
    /// </summary>
    /// <param name="query">The full-text search query.</param>
    /// <param name="target">The graph element family searched by the root.</param>
    /// <param name="elementType">An optional concrete element type used to narrow the search target.</param>
    public SearchRoot(string query, SearchRootTarget target, Type? elementType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        QueryModelGuard.RequireDefinedEnum(target, nameof(target));

        if (elementType is not null)
        {
            ValidateElementType(target, elementType);
        }

        Query = query;
        Target = target;
        ElementType = elementType;
    }

    /// <summary>
    /// Gets the full-text search query.
    /// </summary>
    public string Query { get; }

    /// <summary>
    /// Gets the graph element family searched by the root.
    /// </summary>
    public SearchRootTarget Target { get; }

    /// <summary>
    /// Gets the concrete element type used to narrow the search target, if one is known.
    /// </summary>
    public Type? ElementType { get; }

    private static void ValidateElementType(SearchRootTarget target, Type elementType)
    {
        switch (target)
        {
            case SearchRootTarget.Nodes:
                QueryModelGuard.RequireAssignableTo(elementType, typeof(INode), nameof(elementType));
                break;
            case SearchRootTarget.Relationships:
                QueryModelGuard.RequireAssignableTo(elementType, typeof(IRelationship), nameof(elementType));
                break;
            case SearchRootTarget.Entities:
                QueryModelGuard.RequireAssignableTo(elementType, typeof(IEntity), nameof(elementType));
                break;
        }
    }
}
