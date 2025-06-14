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
/// Represents a queryable graph data source that supports LINQ operations.
/// This interface extends IQueryable&lt;T&gt; with graph-specific functionality.
/// </summary>
/// <typeparam name="T">The type of elements in the graph queryable</typeparam>
public interface IGraphQueryable<T> : IQueryable<T>, IGraphQueryable
{
    /// <summary>
    /// Gets the graph instance associated with this queryable
    /// </summary>
    IGraph Graph { get; }

    /// <summary>
    /// Gets the graph query provider that handles graph-specific operations
    /// </summary>
    new IGraphQueryProvider Provider { get; }
}