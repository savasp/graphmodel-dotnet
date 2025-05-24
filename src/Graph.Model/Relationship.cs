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
/// Base class for graph relationships that provides default implementation for IRelationship
/// </summary>
public abstract class Relationship : IRelationship
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Relationship"/> class with default values.
    /// </summary>
    /// <param name="isBidirectional">Indicates whether the relationship is bidirectional.</param>
    public Relationship(bool isBidirectional)
    {
        this.IsBidirectional = isBidirectional;
    }

    /// <inheritdoc/>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <inheritdoc/>
    public string SourceId { get; set; } = default!;

    /// <inheritdoc/>
    public string TargetId { get; set; } = default!;

    /// <inheritdoc/>
    public bool IsBidirectional { get; set; }
}
