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

using System.Linq.Expressions;

namespace Cvoya.Graph.Model;

/// <summary>
/// Interface for graph analysis and algorithm operations
/// </summary>
/// <typeparam name="T">The type of entities being analyzed</typeparam>
public interface IGraphAnalysis<T> where T : class, IEntity, new()
{
    /// <summary>
    /// Calculates centrality measures for nodes
    /// </summary>
    /// <param name="centralityType">The type of centrality to calculate</param>
    /// <returns>A queryable for centrality results</returns>
    IGraphQueryable<INodeCentrality<T>> Centrality(CentralityType centralityType);

    /// <summary>
    /// Detects communities within the graph
    /// </summary>
    /// <param name="algorithm">The community detection algorithm to use</param>
    /// <returns>A queryable for community results</returns>
    IGraphQueryable<ICommunity<T>> Communities(CommunityDetectionAlgorithm algorithm = CommunityDetectionAlgorithm.Louvain);

    /// <summary>
    /// Finds strongly connected components
    /// </summary>
    /// <returns>A queryable for strongly connected components</returns>
    IGraphQueryable<IConnectedComponent<T>> StronglyConnectedComponents();

    /// <summary>
    /// Finds weakly connected components
    /// </summary>
    /// <returns>A queryable for weakly connected components</returns>
    IGraphQueryable<IConnectedComponent<T>> WeaklyConnectedComponents();

    /// <summary>
    /// Calculates graph similarity metrics
    /// </summary>
    /// <param name="other">The other graph to compare with</param>
    /// <param name="metric">The similarity metric to use</param>
    /// <returns>The similarity score</returns>
    Task<double> SimilarityTo(IGraphQueryable<T> other, SimilarityMetric metric);

    /// <summary>
    /// Performs graph clustering
    /// </summary>
    /// <param name="algorithm">The clustering algorithm to use</param>
    /// <param name="parameters">Algorithm-specific parameters</param>
    /// <returns>A queryable for clustering results</returns>
    IGraphQueryable<ICluster<T>> Clustering(ClusteringAlgorithm algorithm, IDictionary<string, object>? parameters = null);

    /// <summary>
    /// Finds bridges in the graph (edges whose removal increases connected components)
    /// </summary>
    /// <returns>A queryable for bridge relationships</returns>
    IGraphQueryable<IRelationship> Bridges();

    /// <summary>
    /// Finds articulation points (nodes whose removal increases connected components)
    /// </summary>
    /// <returns>A queryable for articulation point nodes</returns>
    IGraphQueryable<T> ArticulationPoints();

    /// <summary>
    /// Calculates graph density
    /// </summary>
    /// <returns>The graph density value</returns>
    Task<double> Density();

    /// <summary>
    /// Calculates graph diameter (longest shortest path)
    /// </summary>
    /// <returns>The graph diameter</returns>
    Task<int> Diameter();

    /// <summary>
    /// Calculates graph radius (shortest eccentricity)
    /// </summary>
    /// <returns>The graph radius</returns>
    Task<int> Radius();

    /// <summary>
    /// Performs motif detection in the graph
    /// </summary>
    /// <param name="motifPattern">The motif pattern to search for</param>
    /// <returns>A queryable for motif instances</returns>
    IGraphQueryable<IMotifInstance<T>> Motifs(IMotifPattern motifPattern);

    /// <summary>
    /// Calculates node similarity based on various metrics
    /// </summary>
    /// <param name="sourceNode">The source node</param>
    /// <param name="metric">The similarity metric to use</param>
    /// <returns>A queryable for node similarity results</returns>
    IGraphQueryable<INodeSimilarity<T>> NodeSimilarity(T sourceNode, NodeSimilarityMetric metric);

    /// <summary>
    /// Performs link prediction analysis
    /// </summary>
    /// <param name="algorithm">The link prediction algorithm to use</param>
    /// <returns>A queryable for link prediction results</returns>
    IGraphQueryable<ILinkPrediction<T>> LinkPrediction(LinkPredictionAlgorithm algorithm);
}

/// <summary>
/// Interface for node centrality results
/// </summary>
/// <typeparam name="T">The type of the node</typeparam>
public interface INodeCentrality<T> where T : class, IEntity, new()
{
    /// <summary>
    /// Gets the node for which centrality was calculated
    /// </summary>
    T Node { get; }

    /// <summary>
    /// Gets the centrality score
    /// </summary>
    double Score { get; }

    /// <summary>
    /// Gets the rank of this node based on centrality (1 = highest)
    /// </summary>
    int Rank { get; }

    /// <summary>
    /// Gets the centrality type that was calculated
    /// </summary>
    CentralityType CentralityType { get; }

    /// <summary>
    /// Gets additional metadata about the centrality calculation
    /// </summary>
    IReadOnlyDictionary<string, object> Metadata { get; }
}

/// <summary>
/// Interface for community detection results
/// </summary>
/// <typeparam name="T">The type of nodes in the community</typeparam>
public interface ICommunity<T> where T : class, IEntity, new()
{
    /// <summary>
    /// Gets the unique identifier for this community
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the nodes that belong to this community
    /// </summary>
    IReadOnlyList<T> Nodes { get; }

    /// <summary>
    /// Gets the size of this community
    /// </summary>
    int Size { get; }

    /// <summary>
    /// Gets the modularity score of this community
    /// </summary>
    double Modularity { get; }

    /// <summary>
    /// Gets the density of connections within this community
    /// </summary>
    double InternalDensity { get; }

    /// <summary>
    /// Gets the algorithm used to detect this community
    /// </summary>
    CommunityDetectionAlgorithm Algorithm { get; }

    /// <summary>
    /// Gets additional properties of this community
    /// </summary>
    IReadOnlyDictionary<string, object> Properties { get; }
}

/// <summary>
/// Interface for connected component results
/// </summary>
/// <typeparam name="T">The type of nodes in the component</typeparam>
public interface IConnectedComponent<T> where T : class, IEntity, new()
{
    /// <summary>
    /// Gets the unique identifier for this component
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the nodes that belong to this component
    /// </summary>
    IReadOnlyList<T> Nodes { get; }

    /// <summary>
    /// Gets the size of this component
    /// </summary>
    int Size { get; }

    /// <summary>
    /// Gets whether this is a strongly or weakly connected component
    /// </summary>
    bool IsStronglyConnected { get; }

    /// <summary>
    /// Gets the diameter of this component
    /// </summary>
    int? Diameter { get; }
}

/// <summary>
/// Interface for clustering results
/// </summary>
/// <typeparam name="T">The type of nodes in the cluster</typeparam>
public interface ICluster<T> where T : class, IEntity, new()
{
    /// <summary>
    /// Gets the unique identifier for this cluster
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the nodes that belong to this cluster
    /// </summary>
    IReadOnlyList<T> Nodes { get; }

    /// <summary>
    /// Gets the centroid of this cluster (if applicable)
    /// </summary>
    T? Centroid { get; }

    /// <summary>
    /// Gets the quality score of this cluster
    /// </summary>
    double Quality { get; }

    /// <summary>
    /// Gets the algorithm used to create this cluster
    /// </summary>
    ClusteringAlgorithm Algorithm { get; }

    /// <summary>
    /// Gets additional properties of this cluster
    /// </summary>
    IReadOnlyDictionary<string, object> Properties { get; }
}

/// <summary>
/// Interface for motif pattern definition
/// </summary>
public interface IMotifPattern
{
    /// <summary>
    /// Gets the unique identifier for this motif pattern
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the size of this motif (number of nodes)
    /// </summary>
    int Size { get; }

    /// <summary>
    /// Gets the description of this motif pattern
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the pattern definition
    /// </summary>
    string Pattern { get; }
}

/// <summary>
/// Interface for motif instance results
/// </summary>
/// <typeparam name="T">The type of nodes in the motif</typeparam>
public interface IMotifInstance<T> where T : class, IEntity, new()
{
    /// <summary>
    /// Gets the motif pattern that was matched
    /// </summary>
    IMotifPattern Pattern { get; }

    /// <summary>
    /// Gets the nodes that form this motif instance
    /// </summary>
    IReadOnlyList<T> Nodes { get; }

    /// <summary>
    /// Gets the relationships that form this motif instance
    /// </summary>
    IReadOnlyList<IRelationship> Relationships { get; }

    /// <summary>
    /// Gets the confidence score for this motif match
    /// </summary>
    double Confidence { get; }
}

/// <summary>
/// Interface for node similarity results
/// </summary>
/// <typeparam name="T">The type of the nodes</typeparam>
public interface INodeSimilarity<T> where T : class, IEntity, new()
{
    /// <summary>
    /// Gets the source node
    /// </summary>
    T SourceNode { get; }

    /// <summary>
    /// Gets the target node
    /// </summary>
    T TargetNode { get; }

    /// <summary>
    /// Gets the similarity score
    /// </summary>
    double Score { get; }

    /// <summary>
    /// Gets the similarity metric that was used
    /// </summary>
    NodeSimilarityMetric Metric { get; }

    /// <summary>
    /// Gets additional details about the similarity calculation
    /// </summary>
    IReadOnlyDictionary<string, object> Details { get; }
}

/// <summary>
/// Interface for link prediction results
/// </summary>
/// <typeparam name="T">The type of the nodes</typeparam>
public interface ILinkPrediction<T> where T : class, IEntity, new()
{
    /// <summary>
    /// Gets the source node
    /// </summary>
    T SourceNode { get; }

    /// <summary>
    /// Gets the target node
    /// </summary>
    T TargetNode { get; }

    /// <summary>
    /// Gets the prediction score (probability of link formation)
    /// </summary>
    double Score { get; }

    /// <summary>
    /// Gets the confidence in this prediction
    /// </summary>
    double Confidence { get; }

    /// <summary>
    /// Gets the algorithm used for prediction
    /// </summary>
    LinkPredictionAlgorithm Algorithm { get; }

    /// <summary>
    /// Gets additional features used in the prediction
    /// </summary>
    IReadOnlyDictionary<string, double> Features { get; }
}

/// <summary>
/// Types of centrality measures
/// </summary>
public enum CentralityType
{
    /// <summary>Degree centrality (number of connections)</summary>
    Degree,
    
    /// <summary>Betweenness centrality (measure of bridge importance)</summary>
    Betweenness,
    
    /// <summary>Closeness centrality (measure of reach efficiency)</summary>
    Closeness,
    
    /// <summary>Eigenvector centrality (importance based on connection quality)</summary>
    Eigenvector,
    
    /// <summary>PageRank centrality</summary>
    PageRank,
    
    /// <summary>Katz centrality</summary>
    Katz,
    
    /// <summary>Harmonic centrality</summary>
    Harmonic
}

/// <summary>
/// Community detection algorithms
/// </summary>
public enum CommunityDetectionAlgorithm
{
    /// <summary>Louvain algorithm</summary>
    Louvain,
    
    /// <summary>Leiden algorithm</summary>
    Leiden,
    
    /// <summary>Label propagation algorithm</summary>
    LabelPropagation,
    
    /// <summary>Infomap algorithm</summary>
    Infomap,
    
    /// <summary>Girvan-Newman algorithm</summary>
    GirvanNewman,
    
    /// <summary>Walktrap algorithm</summary>
    Walktrap
}

/// <summary>
/// Graph similarity metrics
/// </summary>
public enum SimilarityMetric
{
    /// <summary>Jaccard similarity</summary>
    Jaccard,
    
    /// <summary>Cosine similarity</summary>
    Cosine,
    
    /// <summary>Graph edit distance</summary>
    GraphEditDistance,
    
    /// <summary>Spectral similarity</summary>
    Spectral,
    
    /// <summary>Structural similarity</summary>
    Structural
}

/// <summary>
/// Clustering algorithms
/// </summary>
public enum ClusteringAlgorithm
{
    /// <summary>K-means clustering</summary>
    KMeans,
    
    /// <summary>Hierarchical clustering</summary>
    Hierarchical,
    
    /// <summary>DBSCAN clustering</summary>
    DBSCAN,
    
    /// <summary>Spectral clustering</summary>
    Spectral,
    
    /// <summary>Affinity propagation</summary>
    AffinityPropagation
}

/// <summary>
/// Node similarity metrics
/// </summary>
public enum NodeSimilarityMetric
{
    /// <summary>Jaccard similarity based on neighbors</summary>
    JaccardNeighbors,
    
    /// <summary>Common neighbors count</summary>
    CommonNeighbors,
    
    /// <summary>Adamic-Adar index</summary>
    AdamicAdar,
    
    /// <summary>Resource allocation index</summary>
    ResourceAllocation,
    
    /// <summary>Preferential attachment</summary>
    PreferentialAttachment,
    
    /// <summary>Cosine similarity based on features</summary>
    CosineFeatures,
    
    /// <summary>Euclidean distance based on features</summary>
    EuclideanFeatures
}

/// <summary>
/// Link prediction algorithms
/// </summary>
public enum LinkPredictionAlgorithm
{
    /// <summary>Common neighbors based prediction</summary>
    CommonNeighbors,
    
    /// <summary>Adamic-Adar based prediction</summary>
    AdamicAdar,
    
    /// <summary>Resource allocation based prediction</summary>
    ResourceAllocation,
    
    /// <summary>Preferential attachment based prediction</summary>
    PreferentialAttachment,
    
    /// <summary>Katz index based prediction</summary>
    KatzIndex,
    
    /// <summary>Machine learning based prediction</summary>
    MachineLearning,
    
    /// <summary>Matrix factorization based prediction</summary>
    MatrixFactorization
}