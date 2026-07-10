// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.
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

namespace Cvoya.Graph.Neo4j.Querying.Linq.Queryables;

/// <summary>
/// A non-generic single-hop segment used to compose a <see cref="GraphPath"/>. Unlike
/// <see cref="IGraphPathSegment{TSource, TRel, TTarget}"/>, intermediate hops in a variable-length
/// path are not statically typed per-hop (a path may traverse through heterogeneous node/
/// relationship types when labels overlap), so <see cref="IGraphPath.Segments"/> is expressed in
/// terms of the non-generic <see cref="IGraphPathSegment"/> base.
/// </summary>
internal sealed record GraphPathHopSegment(INode StartNode, IRelationship Relationship, INode EndNode) : IGraphPathSegment;

/// <summary>
/// Concrete <see cref="IGraphPath"/> implementation materialized from a <c>TraversePaths</c> query.
/// </summary>
internal sealed record GraphPath(INode Start, INode End, IReadOnlyList<IGraphPathSegment> Segments) : IGraphPath;
