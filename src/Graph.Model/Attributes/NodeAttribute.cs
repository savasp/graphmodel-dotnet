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

using System;
using System.Linq;


/// <summary>
/// Attribute to specify custom labels for graph nodes.
/// </summary>
/// <remarks>
/// Use this attribute on classes implementing INode to define how the node
/// should be labeled in the graph storage system.
/// </remarks>
/// <example>
/// <code>
/// [Node("Person")]
/// public class Person : INode
/// {
///     public string Id { get; set; } = Guid.NewGuid().ToString();
///     public string Name { get; set; } = string.Empty;
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
public class NodeAttribute() : Attribute
{
    /// <summary>
    /// Initializes a new instance of the NodeAttribute class with the specified label.
    /// </summary>
    /// <param name="label">The label to apply to the node.</param>
    public NodeAttribute(string label) : this()
    {
        Label = label;
    }

    /// <summary>
    /// Initializes a new instance of the NodeAttribute class with multiple labels.
    /// </summary>
    /// <param name="labels">The labels to apply to the node.</param>
    public NodeAttribute(params string[] labels) : this()
    {
        if (labels.Length > 0)
        {
            Label = labels[0]; // Primary label
            AdditionalLabels = labels.Skip(1).ToArray();
        }
    }

    /// <summary>
    /// Gets or sets the label to apply to the node. If null, the name of the class is used as the label.
    /// </summary>
    /// <value>The node label used for graph storage.</value>
    public string Label { get; set; } = null!;

    /// <summary>
    /// Gets additional labels for the node.
    /// </summary>
    /// <value>Additional labels used for graph storage.</value>
    public string[] AdditionalLabels { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// Gets all labels (primary + additional) for the node.
    /// </summary>
    /// <returns>All labels for this node.</returns>
    public IEnumerable<string> GetAllLabels()
    {
        if (!string.IsNullOrEmpty(Label))
            yield return Label;

        foreach (var additionalLabel in AdditionalLabels)
        {
            if (!string.IsNullOrEmpty(additionalLabel))
                yield return additionalLabel;
        }
    }
}
