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
/// Base class for graph nodes that provides a default implementation of the INode interface.
/// This serves as a foundation for creating domain-specific node entities.
/// </summary>
/// <remarks>
/// Use this class as a base class for your domain models to get automatic ID generation
/// and basic node functionality.
/// </remarks>
public abstract record Node : INode
{
    /// <summary>
    /// Gets or sets the unique identifier of this node.
    /// Automatically initialized with a new GUID string when a node is created.
    /// </summary>
    /// <remarks>
    /// The default format used is the "N" format (32 digits without hyphens).
    /// </remarks>
    public string Id { get; private set; } = Guid.NewGuid().ToString("N");
}
