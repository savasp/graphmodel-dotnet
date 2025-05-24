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
/// Options for controlling graph operations behavior
/// </summary>
public struct GraphOperationOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphOperationOptions"/> struct with default values.
    /// </summary>
    public GraphOperationOptions()
    {
    }

    /// <summary>
    /// Controls how deep to traverse relationships during operations.
    /// 0 = node only (default), 1 = immediate relationships, -1 = full graph
    /// </summary>
    public int TraversalDepth { get; set; } = 0;

    /// <summary>
    /// Whether to create nodes that don't exist when updating relationships
    /// </summary>
    public bool CreateMissingNodes { get; set; } = false;

    /// <summary>
    /// Whether to update existing related nodes
    /// </summary>
    public bool UpdateExistingNodes { get; set; } = false;

    /// <summary>
    /// Specific relationship types to follow (null = all)
    /// </summary>
    public HashSet<string>? RelationshipTypes { get; set; }
}