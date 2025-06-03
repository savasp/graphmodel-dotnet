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
/// Simple implementation of graph path cost for single-hop traversals
/// </summary>
public record SimpleGraphPathCost(double? Weight, int Hops = 1) : IGraphPathCost
{
    /// <summary>
    /// Gets the total cost of the path
    /// </summary>
    public double TotalCost => Weight ?? 0.0;

    /// <summary>
    /// Gets the distance of the path (same as total cost for simple paths)
    /// </summary>
    public double Distance => TotalCost;

    /// <summary>
    /// Gets the number of hops in the path
    /// </summary>
    public int Hops { get; } = Hops;

    /// <summary>
    /// Gets the computational cost of finding this path (minimal for simple paths)
    /// </summary>
    public double ComputationCost => 1.0;

    /// <summary>
    /// Gets the unit of measurement for the cost
    /// </summary>
    public string Unit => "weight";

    /// <summary>
    /// Gets additional cost-related properties
    /// </summary>
    public IReadOnlyDictionary<string, object> Properties { get; } = new Dictionary<string, object>
    {
        ["weight"] = Weight ?? 0.0,
        ["distance"] = Weight ?? 0.0,
        ["hops"] = Hops,
        ["computationCost"] = 1.0,
        ["unit"] = "weight"
    }.AsReadOnly();
}