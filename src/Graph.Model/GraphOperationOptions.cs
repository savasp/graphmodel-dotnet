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
/// and relationship type filtering./>
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
    /// Gets or sets whether to delete related nodes when deleting relationships.
    /// </summary>
    /// <remarks>
    /// When true, if a relationship is deleted, the related node will also be deleted if it has no other relationships.
    /// When false, the related node will remain in the graph even if it has no relationships.
    /// </remarks>
    public bool CascadeDelete { get; set; } = false;
}