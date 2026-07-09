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
/// Represents a relationship pattern in a Cypher path.
/// </summary>
public sealed record RelationshipPattern : PatternElement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RelationshipPattern"/> class.
    /// </summary>
    /// <param name="alias">The optional relationship alias.</param>
    /// <param name="type">The optional relationship type.</param>
    /// <param name="direction">The relationship direction.</param>
    /// <param name="depth">The optional variable-length depth range.</param>
    public RelationshipPattern(string? alias, string? type, CypherDirection direction, DepthRange? depth)
    {
        Alias = ArgumentValidation.OptionalName(alias, nameof(alias));
        Type = ArgumentValidation.OptionalName(type, nameof(type));
        Direction = direction;
        Depth = depth;
    }

    /// <summary>
    /// Gets the optional relationship alias.
    /// </summary>
    public string? Alias { get; }

    /// <summary>
    /// Gets the optional relationship type.
    /// </summary>
    public string? Type { get; }

    /// <summary>
    /// Gets the relationship direction.
    /// </summary>
    public CypherDirection Direction { get; }

    /// <summary>
    /// Gets the optional variable-length depth range.
    /// </summary>
    public DepthRange? Depth { get; }
}
