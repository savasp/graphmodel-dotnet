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
/// Options for controlling graph traversal behavior
/// </summary>
public class TraversalOptions
{
    /// <summary>
    /// Gets or sets the maximum depth to traverse
    /// </summary>
    public int MaxDepth { get; set; } = 1;

    /// <summary>
    /// Gets or sets the minimum depth to traverse
    /// </summary>
    public int MinDepth { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether to include the starting nodes in results
    /// </summary>
    public bool IncludeStartNodes { get; set; } = false;

    /// <summary>
    /// Gets or sets the traversal direction
    /// </summary>
    public TraversalDirection Direction { get; set; } = TraversalDirection.Outgoing;

    /// <summary>
    /// Gets or sets whether to avoid cycles during traversal
    /// </summary>
    public bool AvoidCycles { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of results to return
    /// </summary>
    public int? MaxResults { get; set; }

    /// <summary>
    /// Gets or sets whether to compute path weights
    /// </summary>
    public bool ComputeWeights { get; set; } = false;

    /// <summary>
    /// Gets or sets the property name to use for edge weights
    /// </summary>
    public string? WeightProperty { get; set; }

    /// <summary>
    /// Gets or sets the traversal strategy
    /// </summary>
    public TraversalStrategy Strategy { get; set; } = TraversalStrategy.DepthFirst;

    /// <summary>
    /// Gets or sets additional traversal hints
    /// </summary>
    public List<string> Hints { get; set; } = new();
}

