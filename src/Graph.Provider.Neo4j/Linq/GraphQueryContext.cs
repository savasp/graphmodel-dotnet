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

using Cvoya.Graph.Model;

namespace Cvoya.Graph.Provider.Neo4j.Linq;

/// <summary>
/// Neo4j implementation of graph query execution context
/// </summary>
internal class GraphQueryContext : IGraphQueryContext
{
    public Guid QueryId { get; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<string> Hints { get; init; } = Array.Empty<string>();
    public IGraphQueryCacheConfig? CacheConfig { get; init; }
    public TimeSpan? Timeout { get; init; }
    public bool ProfilingEnabled { get; init; }
    public bool CascadeDeleteEnabled { get; init; }
    public GraphMetadataTypes MetadataTypes { get; init; } = GraphMetadataTypes.None;

    public GraphQueryContext WithHints(IReadOnlyList<string> hints)
    {
        return new GraphQueryContext
        {
            Hints = hints,
            CacheConfig = CacheConfig,
            Timeout = Timeout,
            ProfilingEnabled = ProfilingEnabled,
            CascadeDeleteEnabled = CascadeDeleteEnabled,
            MetadataTypes = MetadataTypes
        };
    }

    public GraphQueryContext WithHint(string hint)
    {
        var newHints = new List<string>(Hints) { hint };
        return WithHints(newHints);
    }

    public GraphQueryContext WithHints(params string[] hints)
    {
        var allHints = new List<string>(Hints);
        allHints.AddRange(hints);
        return WithHints(allHints);
    }

    public GraphQueryContext WithIndexHint(string indexName)
    {
        return WithHint($"USE_INDEX({indexName})");
    }

    public GraphQueryContext WithCaching(TimeSpan duration)
    {
        var cacheConfig = new GraphQueryCacheConfig(Guid.NewGuid().ToString(), duration);
        return WithCacheConfig(cacheConfig);
    }

    public GraphQueryContext WithCaching(string cacheKey, TimeSpan duration)
    {
        var cacheConfig = new GraphQueryCacheConfig(cacheKey, duration);
        return WithCacheConfig(cacheConfig);
    }

    public GraphQueryContext WithMetadata(GraphMetadataTypes metadata)
    {
        return WithMetadataTypes(metadata);
    }

    public GraphQueryContext WithTimeout(TimeSpan timeout)
    {
        return WithTimeout((TimeSpan?)timeout);
    }

    public GraphQueryContext WithProfiling(bool enabled)
    {
        return new GraphQueryContext
        {
            Hints = Hints,
            CacheConfig = CacheConfig,
            Timeout = Timeout,
            ProfilingEnabled = enabled,
            CascadeDeleteEnabled = CascadeDeleteEnabled,
            MetadataTypes = MetadataTypes
        };
    }

    public GraphQueryContext WithCascadeDelete(bool enabled)
    {
        return new GraphQueryContext
        {
            Hints = Hints,
            CacheConfig = CacheConfig,
            Timeout = Timeout,
            ProfilingEnabled = ProfilingEnabled,
            CascadeDeleteEnabled = enabled,
            MetadataTypes = MetadataTypes
        };
    }

    public GraphQueryContext WithCacheConfig(IGraphQueryCacheConfig? cacheConfig)
    {
        return new GraphQueryContext
        {
            Hints = Hints,
            CacheConfig = cacheConfig,
            Timeout = Timeout,
            ProfilingEnabled = ProfilingEnabled,
            CascadeDeleteEnabled = CascadeDeleteEnabled,
            MetadataTypes = MetadataTypes
        };
    }

    public GraphQueryContext WithTimeout(TimeSpan? timeout)
    {
        return new GraphQueryContext
        {
            Hints = Hints,
            CacheConfig = CacheConfig,
            Timeout = timeout,
            ProfilingEnabled = ProfilingEnabled,
            CascadeDeleteEnabled = CascadeDeleteEnabled,
            MetadataTypes = MetadataTypes
        };
    }

    public GraphQueryContext WithMetadataTypes(GraphMetadataTypes metadataTypes)
    {
        return new GraphQueryContext
        {
            Hints = Hints,
            CacheConfig = CacheConfig,
            Timeout = Timeout,
            ProfilingEnabled = ProfilingEnabled,
            CascadeDeleteEnabled = CascadeDeleteEnabled,
            MetadataTypes = metadataTypes
        };
    }
}
