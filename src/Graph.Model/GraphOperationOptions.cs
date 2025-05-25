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
/// Options for controlling graph operations behavior.
/// </summary>
/// <remarks>
/// This struct provides configuration options for graph traversal depth, node creation behavior,
/// and relationship type filtering. Use the extension methods in <see cref="GraphOperationExtensions"/>
/// to create and configure instances with a fluent API.
/// </remarks>
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
    /// </summary>
    /// <remarks>
    /// <para>0 = node only (default) - No relationships will be loaded.</para>
    /// <para>1 = immediate relationships - Load direct relationships only.</para>
    /// <para>-1 = full graph - Load the complete accessible graph from this node.</para>
    /// <para>Other positive values represent the maximum traversal depth.</para>
    /// </remarks>
    public int TraversalDepth { get; set; } = 0;

    /// <summary>
    /// Gets or sets whether to create nodes that don't exist when creating or updating relationships.
    /// </summary>
    /// <remarks>
    /// When true, if a relationship references nodes that don't exist, those nodes will be created automatically.
    /// When false, an exception will be thrown if referenced nodes don't exist.
    /// </remarks>
    public bool CreateMissingNodes { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to update existing related nodes when processing relationships.
    /// </summary>
    /// <remarks>
    /// When true, related nodes will be updated based on the provided data.
    /// When false, existing nodes will be left unchanged.
    /// </remarks>
    public bool UpdateExistingNodes { get; set; } = false;

    /// <summary>
    /// Gets or sets specific relationship types to follow during traversal.
    /// </summary>
    /// <remarks>
    /// When null, all relationship types will be considered.
    /// When set, only relationships of the specified types will be traversed.
    /// </remarks>
    public HashSet<string>? RelationshipTypes { get; set; }
}