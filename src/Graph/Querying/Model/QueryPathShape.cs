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

namespace Cvoya.Graph.Querying;

/// <summary>
/// Carries the CLR types required to materialize a decomposed graph path result.
/// </summary>
/// <param name="SourceType">The path source node type.</param>
/// <param name="RelationshipType">The path relationship type.</param>
/// <param name="TargetType">The path target node type.</param>
public sealed record QueryPathShape(Type SourceType, Type RelationshipType, Type TargetType);
