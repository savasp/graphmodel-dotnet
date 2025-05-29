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
/// Interface for graph query execution context and metadata
/// </summary>
public interface IGraphQueryContext
{
    /// <summary>
    /// Gets the unique identifier for this query context
    /// </summary>
    Guid QueryId { get; }

    /// <summary>
    /// Gets the timestamp when the query was created
    /// </summary>
    DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets the optimization hints applied to this query
    /// </summary>
    IReadOnlyList<string> Hints { get; }

    /// <summary>
    /// Gets the cache configuration for this query
    /// </summary>
    IGraphQueryCacheConfig? CacheConfig { get; }

    /// <summary>
    /// Gets the timeout configuration for this query
    /// </summary>
    TimeSpan? Timeout { get; }

    /// <summary>
    /// Gets whether profiling is enabled for this query
    /// </summary>
    bool ProfilingEnabled { get; }

    /// <summary>
    /// Gets whether cascade delete is enabled for this query
    /// </summary>
    bool CascadeDeleteEnabled { get; }

    /// <summary>
    /// Gets the metadata types to include in results
    /// </summary>
    GraphMetadataTypes MetadataTypes { get; }
}
