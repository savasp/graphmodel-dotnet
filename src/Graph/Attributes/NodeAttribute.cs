// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph;

using System;


/// <summary>
/// Attribute to specify the label for a graph node.
/// </summary>
/// <remarks>
/// Use this attribute on classes implementing INode to define how the node is labeled in the graph storage
/// system. A node type maps to exactly one label. If the attribute is omitted (or its <see cref="Label"/> is
/// left unset), the class name is used. The label must be unique (case-insensitive) across every node type
/// loaded in the process; <see cref="SchemaRegistry"/> enforces this. The label is also the portability key
/// used to resolve a compatible local type when materializing provider-native graph rows.
/// </remarks>
/// <example>
/// <code>
/// [Node("Person")]
/// public class Person : Node
/// {
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
    /// Gets or sets the label to apply to the node. If null, the name of the class is used as the label.
    /// </summary>
    /// <value>The node label used for graph storage.</value>
    public string Label { get; set; } = null!;
}
