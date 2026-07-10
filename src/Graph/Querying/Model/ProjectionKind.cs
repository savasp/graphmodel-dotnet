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

namespace Cvoya.Graph.Querying;

/// <summary>
/// Classifies the shape of a query projection.
/// </summary>
public enum ProjectionKind
{
    /// <summary>
    /// The projection returns the current element unchanged.
    /// </summary>
    Identity,

    /// <summary>
    /// The projection returns a scalar expression.
    /// </summary>
    Scalar,

    /// <summary>
    /// The projection returns an anonymous object shape.
    /// </summary>
    Anonymous,

    /// <summary>
    /// The projection returns a path segment or path-segment component.
    /// </summary>
    PathSegment,
}
