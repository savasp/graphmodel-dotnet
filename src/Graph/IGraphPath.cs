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

namespace Cvoya.Graph;

/// <summary>
/// Represents a variable-length path through the graph: an ordered sequence of one or more
/// single-hop <see cref="IGraphPathSegment"/> instances connecting <see cref="Start"/> to
/// <see cref="End"/>.
/// </summary>
/// <remarks>
/// Use <see cref="IGraphQueryable{T}"/> traversal operators that return
/// <c>IGraphQueryable&lt;IGraphPath&gt;</c> (e.g. <c>TraversePaths</c>) when the number of hops
/// is variable (min/max depth greater than a single hop). For a single, statically-typed hop,
/// use <see cref="IGraphPathSegment{TSource, TRel, TTarget}"/> directly instead.
/// </remarks>
public interface IGraphPath
{
    /// <summary>
    /// Gets the first node in the path.
    /// </summary>
    INode Start { get; }

    /// <summary>
    /// Gets the last node in the path.
    /// </summary>
    INode End { get; }

    /// <summary>
    /// Gets the ordered sequence of single-hop segments that make up this path, from
    /// <see cref="Start"/> to <see cref="End"/>. Always contains at least one segment.
    /// </summary>
    IReadOnlyList<IGraphPathSegment> Segments { get; }
}
