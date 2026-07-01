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

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Execution;

using Cvoya.Graph.Model;

/// <summary>
/// Concrete implementation of <see cref="IGraphPathSegment{TSource, TRel, TTarget}"/> for the AGE provider.
/// Used by the shared materializer when reconstructing path segments
/// from projected Cypher columns (e.g., <c>Select(ps => new {{ Path = ps }})</c>).
/// </summary>
/// <typeparam name="TSource">The type of the source node.</typeparam>
/// <typeparam name="TRel">The type of the relationship.</typeparam>
/// <typeparam name="TTarget">The type of the target node.</typeparam>
internal sealed record GraphPathSegment<TSource, TRel, TTarget>(
    TSource StartNode,
    TRel Relationship,
    TTarget EndNode) : IGraphPathSegment<TSource, TRel, TTarget>
    where TSource : INode
    where TRel : IRelationship
    where TTarget : INode
{
    INode IGraphPathSegment.StartNode => StartNode;
    INode IGraphPathSegment.EndNode => EndNode;
    IRelationship IGraphPathSegment.Relationship => Relationship;
}
