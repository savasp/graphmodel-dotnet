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
/// Represents a queryable graph data source that supports LINQ operations over nodes.
/// This interface extends IGraphQueryable&lt;T&gt; with additional functionality specific to graph nodes.
/// It allows for traversing relationships and querying nodes in a graph context.
/// This interface is designed to be used with graph databases and provides methods for traversing relationships
/// </summary>
/// <typeparam name="TNode">An <see cref="INode"/>-derived type.</typeparam>
public interface IGraphNodeQueryable<out TNode> : IGraphQueryable<TNode>, IGraphNodeQueryable
    where TNode : INode
{
}