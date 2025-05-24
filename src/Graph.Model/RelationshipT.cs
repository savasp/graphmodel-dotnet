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
/// Base class for strongly-typed graph relationships between specific node types.
/// Provides a default implementation of the IRelationship&lt;S, T&gt; interface.
/// </summary>
/// <typeparam name="S">The type of the source node in the relationship.</typeparam>
/// <typeparam name="T">The type of the target node in the relationship.</typeparam>
/// <remarks>
/// This class handles the synchronization between the node objects (Source/Target) and 
/// their respective IDs (SourceId/TargetId), ensuring consistency when either is updated.
/// </remarks>
public class Relationship<S, T> : IRelationship<S, T>
    where S : class, INode
    where T : class, INode
{
    private string sourceId = string.Empty;
    private string targetId = string.Empty;
    private S? source;
    private T? target;

    /// <summary>
    /// Initializes a new instance of the <see cref="Relationship{S, T}"/> class with default values.
    /// </summary>
    /// <param name="isBidirectional">Indicates whether the relationship is bidirectional.</param>
    /// <remarks>
    /// The default value for <see cref="IsBidirectional"/> is false.
    /// </remarks>
    public Relationship(bool isBidirectional = false) 
        : this(null, null, isBidirectional)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Relationship{S, T}"/> class 
    /// with specified source and target nodes.
    /// </summary>
    /// <param name="source">The source node of the relationship.</param>
    /// <param name="target">The target node of the relationship.</param>
    /// <param name="isBidirectional">Indicates whether the relationship is bidirectional.</param>
    /// <remarks>
    /// The default value for <see cref="IsBidirectional"/> is false.
    /// When source or target is provided, their IDs are automatically used to set SourceId and TargetId.
    /// </remarks>
    public Relationship(S? source, T? target, bool isBidirectional = false)
    {
        Source = source;
        Target = target;
        IsBidirectional = isBidirectional;
    }

    /// <inheritdoc/>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <inheritdoc/>
    public string SourceId
    {
        get => sourceId;
        set
        {
            sourceId = value;
            // Only reset Source if the ID actually changed
            if (source is not null && source.Id != value)
            {
                source = null;
            }
        }
    }

    /// <inheritdoc/>
    public string TargetId
    {
        get => targetId;
        set
        {
            targetId = value;
            // Only reset Target if the ID actually changed
            if (target is not null && target.Id != value)
            {
                target = null;
            }
        }
    }

    /// <inheritdoc/>
    public bool IsBidirectional { get; set; }

    /// <inheritdoc/>
    public S? Source
    {
        get => source;
        set
        {
            source = value;
            sourceId = value?.Id ?? sourceId; // Keep existing ID if value is null
        }
    }

    /// <inheritdoc/>
    public T? Target
    {
        get => target;
        set
        {
            target = value;
            targetId = value?.Id ?? targetId; // Keep existing ID if value is null
        }
    }
}