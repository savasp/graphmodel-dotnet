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
/// Represents a directional relationship.
/// </summary>
public interface IRelationship : IEntity
{
    /// <summary>
    /// The direction of the relationship.
    /// </summary>
    bool IsBidirectional { get; set; }

    /// <summary>
    /// The node Id of one of the source node.
    /// </summary>
    string SourceId { get; set; }

    /// <summary>
    /// The node Id of one of the target node.
    /// </summary>
    string TargetId { get; set; }
}
