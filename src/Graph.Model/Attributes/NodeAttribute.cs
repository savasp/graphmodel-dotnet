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
/// Attribute to specify custom labels for graph nodes.
/// </summary>
/// <param name="label">The label to apply to the node. Cannot be null.</param>
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
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public class NodeAttribute(string label) : Attribute
{
    /// <summary>
    /// Gets the label to apply to the node.
    /// </summary>
    /// <value>The node label used for graph storage.</value>
    public string Label { get; } = label ?? throw new ArgumentNullException(nameof(label));
}
