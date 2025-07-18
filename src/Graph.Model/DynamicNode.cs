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
/// Concrete implementation of a dynamic node.
/// </summary>
public sealed record DynamicNode : Node
{
    /// <summary>
    /// Gets the labels for this node.
    /// </summary>
    public IReadOnlyList<string> Labels { get; init; } = new List<string>();

    /// <summary>
    /// Gets the properties of this node.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicNode"/> class.
    /// </summary>
    public DynamicNode() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicNode"/> class with specified labels and properties.
    /// </summary>
    /// <param name="labels">The labels for the node.</param>
    /// <param name="properties">The properties of the node.</param>
    public DynamicNode(IEnumerable<string> labels, IDictionary<string, object?> properties)
    {
        Labels = labels.ToList().AsReadOnly();
        Properties = new Dictionary<string, object?>(properties).AsReadOnly();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicNode"/> class with specified ID, labels and properties.
    /// </summary>
    /// <param name="id">The unique identifier of the node.</param>
    /// <param name="labels">The labels for the node.</param>
    /// <param name="properties">The properties of the node.</param>
    public DynamicNode(string id, IEnumerable<string> labels, IDictionary<string, object?> properties)
    {
        Id = id;
        Labels = labels.ToList().AsReadOnly();
        Properties = new Dictionary<string, object?>(properties).AsReadOnly();
    }
}