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

namespace Cvoya.Graph.Model.Neo4j.Linq;

/// <summary>
/// Simple implementation of IGraphPath for single-hop paths
/// </summary>
/// <typeparam name="TSource">The type of the source node</typeparam>
/// <typeparam name="TRel">The type of the relationship</typeparam>
/// <typeparam name="TTarget">The type of the target node</typeparam>
internal class SimpleGraphPath<TSource, TRel, TTarget> : IGraphPath<TSource, TRel, TTarget>
    where TSource : class, INode, new()
    where TRel : class, IRelationship, new()
    where TTarget : class, INode, new()
{
    public TSource Source { get; }
    public TRel Relationship { get; }
    public TTarget Target { get; }
    public int Length => 1;
    public double? Weight => null;
    public IGraphPathMetadata Metadata { get; }

    public SimpleGraphPath(TSource source, TRel relationship, TTarget target)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Relationship = relationship ?? throw new ArgumentNullException(nameof(relationship));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Metadata = new SimpleGraphPathMetadata();
    }
}

/// <summary>
/// Simple implementation of IGraphPathMetadata
/// </summary>
internal class SimpleGraphPathMetadata : IGraphPathMetadata
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public IGraphPathCost Cost { get; set; } = new SimpleGraphPathCost();
    public IReadOnlyDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// Simple implementation of IGraphPathCost
/// </summary>
internal class SimpleGraphPathCost : IGraphPathCost
{
    public double Distance { get; set; } = 1.0;
    public int Hops { get; set; } = 1;
    public double ComputationCost { get; set; } = 1.0;
}
