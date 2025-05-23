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
/// Base class for graph relationships that provides default implementation for IRelationship.
/// Notice that when setting the source and target properties, the Source and Target properties are reset to null.
/// </summary>
public class Relationship<S, T> : IRelationship<S, T>
    where S : class, INode
    where T : class, INode
{
    private string sourceId = string.Empty;
    private string targetId = string.Empty;
    private S? source = null;
    private T? target = null;

    public Relationship(bool isBidirectional = false) : this(null, null, isBidirectional)
    {
    }

    public Relationship(S? source, T? target, bool isBidirectional = false)
    {
        this.Source = source;
        this.Target = target;
        this.IsBidirectional = isBidirectional;
    }

    /// <inheritdoc/>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <inheritdoc/>
    public string SourceId
    {
        get => this.sourceId;
        set
        {
            this.sourceId = value;
            // Only reset Source if the ID actually changed
            if (this.source != null && this.source.Id != value)
            {
                this.source = null;
            }
        }
    }

    /// <inheritdoc/>
    public string TargetId
    {
        get => this.targetId;
        set
        {
            this.targetId = value;
            // Only reset Target if the ID actually changed
            if (this.target != null && this.target.Id != value)
            {
                this.target = null;
            }
        }
    }

    /// <inheritdoc/>
    public bool IsBidirectional { get; set; }

    /// <inheritdoc/>
    public S? Source
    {
        get => this.source;
        set
        {
            this.source = value;
            this.sourceId = value?.Id ?? this.sourceId; // Keep existing ID if value is null
        }
    }

    /// <inheritdoc/>
    public T? Target
    {
        get => this.target;
        set
        {
            this.target = value;
            this.targetId = value?.Id ?? this.targetId; // Keep existing ID if value is null
        }
    }
}