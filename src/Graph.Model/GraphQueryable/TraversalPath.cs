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

namespace Cvoya.Graph.Model;

/// <summary>
/// Represents a complete traversal path from source to target through a relationship
/// </summary>
/// <typeparam name="TSource">The type of the source node, which must implement <see cref="INode"/>.</typeparam>
/// <typeparam name="TRelationship">The type of the relationship, which must implement <see cref="IRelationship"/>.</typeparam>
/// <typeparam name="TTarget">The type of the target node, which must implement <see cref="INode"/>.</typeparam>
public record TraversalPath<TSource, TRelationship, TTarget>(
    TSource Source,
    TRelationship Relationship,
    TTarget Target
) where TSource : class, INode
  where TRelationship : class, IRelationship
  where TTarget : class, INode;