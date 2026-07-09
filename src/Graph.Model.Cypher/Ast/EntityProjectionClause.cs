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

using Cvoya.Graph.Model.Cypher.Internal;

namespace Cvoya.Graph.Model.Cypher.Ast;

/// <summary>
/// Represents the provider-neutral wire projection used to materialize a node or path segment.
/// </summary>
public sealed record EntityProjectionClause : ICypherClause
{
    /// <summary>Initializes an entity wire projection.</summary>
    public EntityProjectionClause(
        EntityProjectionShape shape,
        string sourceAlias,
        string? relationshipAlias,
        string? targetAlias,
        bool loadSourceProperties,
        bool loadTargetProperties)
        : this(
            shape,
            sourceAlias,
            relationshipAlias,
            targetAlias,
            loadSourceProperties,
            loadTargetProperties,
            includePathCoordinates: false)
    {
    }

    /// <summary>Initializes an entity wire projection.</summary>
    public EntityProjectionClause(
        EntityProjectionShape shape,
        string sourceAlias,
        string? relationshipAlias,
        string? targetAlias,
        bool loadSourceProperties,
        bool loadTargetProperties,
        bool includePathCoordinates)
    {
        Shape = ArgumentValidation.DefinedEnum(shape, nameof(shape));
        SourceAlias = ArgumentValidation.RequiredName(sourceAlias, nameof(sourceAlias));
        RelationshipAlias = ArgumentValidation.OptionalName(relationshipAlias, nameof(relationshipAlias));
        TargetAlias = ArgumentValidation.OptionalName(targetAlias, nameof(targetAlias));
        LoadSourceProperties = loadSourceProperties;
        LoadTargetProperties = loadTargetProperties;
        IncludePathCoordinates = includePathCoordinates;

        if (shape == EntityProjectionShape.PathSegment &&
            (RelationshipAlias is null || TargetAlias is null))
        {
            throw new ArgumentException("Path-segment projections require relationship and target aliases.", nameof(shape));
        }
    }

    /// <summary>Gets the projection shape.</summary>
    public EntityProjectionShape Shape { get; }

    /// <summary>Gets the source node alias.</summary>
    public string SourceAlias { get; }

    /// <summary>Gets the relationship alias for a path segment.</summary>
    public string? RelationshipAlias { get; }

    /// <summary>Gets the target node alias for a path segment.</summary>
    public string? TargetAlias { get; }

    /// <summary>Gets whether declared complex properties are loaded from the source node.</summary>
    public bool LoadSourceProperties { get; }

    /// <summary>Gets whether declared complex properties are loaded from the target node.</summary>
    public bool LoadTargetProperties { get; }

    /// <summary>Gets whether the projection includes graph-path and hop indexes.</summary>
    public bool IncludePathCoordinates { get; }
}

/// <summary>Defines supported entity wire-projection shapes.</summary>
public enum EntityProjectionShape
{
    /// <summary>A single node projection.</summary>
    Node,

    /// <summary>A start-node, relationship, and end-node path segment.</summary>
    PathSegment,
}
