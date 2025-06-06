using Cvoya.Graph.Model;

namespace Cvoya.Graph.Model.Neo4j.Linq;

/// <summary>
/// Represents a path in a graph, containing nodes and relationships traversed.
/// </summary>
/// <typeparam name="TNode">The type of nodes in the path</typeparam>
/// <typeparam name="TRelationship">The type of relationships in the path</typeparam>
public class GraphPath<TNode, TRelationship>
    where TNode : class, Cvoya.Graph.Model.INode
    where TRelationship : class, Cvoya.Graph.Model.IRelationship
{
    /// <summary>
    /// The nodes in the path in order of traversal
    /// </summary>
    public IReadOnlyList<TNode> Nodes { get; }

    /// <summary>
    /// The relationships in the path in order of traversal
    /// </summary>
    public IReadOnlyList<TRelationship> Relationships { get; }

    /// <summary>
    /// The length of the path (number of relationships)
    /// </summary>
    public int Length => Relationships.Count;

    /// <summary>
    /// The start node of the path
    /// </summary>
    public TNode StartNode => Nodes.First();

    /// <summary>
    /// The end node of the path
    /// </summary>
    public TNode EndNode => Nodes.Last();

    /// <summary>
    /// Initializes a new instance of GraphPath
    /// </summary>
    /// <param name="nodes">The nodes in the path</param>
    /// <param name="relationships">The relationships in the path</param>
    public GraphPath(IReadOnlyList<TNode> nodes, IReadOnlyList<TRelationship> relationships)
    {
        if (nodes.Count == 0)
            throw new ArgumentException("Path must contain at least one node", nameof(nodes));

        if (relationships.Count != nodes.Count - 1)
            throw new ArgumentException("Number of relationships must be one less than number of nodes", nameof(relationships));

        Nodes = nodes;
        Relationships = relationships;
    }

    /// <summary>
    /// Creates an empty path with a single node
    /// </summary>
    /// <param name="node">The single node</param>
    public GraphPath(TNode node)
    {
        Nodes = new[] { node };
        Relationships = Array.Empty<TRelationship>();
    }
}

/// <summary>
/// Non-generic version of GraphPath for cases where node/relationship types are not known at compile time
/// </summary>
public class GraphPath
{
    /// <summary>
    /// The nodes in the path in order of traversal
    /// </summary>
    public IReadOnlyList<Cvoya.Graph.Model.INode> Nodes { get; }

    /// <summary>
    /// The relationships in the path in order of traversal
    /// </summary>
    public IReadOnlyList<Cvoya.Graph.Model.IRelationship> Relationships { get; }

    /// <summary>
    /// The length of the path (number of relationships)
    /// </summary>
    public int Length => Relationships.Count;

    /// <summary>
    /// The start node of the path
    /// </summary>
    public Cvoya.Graph.Model.INode StartNode => Nodes.First();

    /// <summary>
    /// The end node of the path
    /// </summary>
    public Cvoya.Graph.Model.INode EndNode => Nodes.Last();

    /// <summary>
    /// Initializes a new instance of GraphPath
    /// </summary>
    /// <param name="nodes">The nodes in the path</param>
    /// <param name="relationships">The relationships in the path</param>
    public GraphPath(IReadOnlyList<Cvoya.Graph.Model.INode> nodes, IReadOnlyList<Cvoya.Graph.Model.IRelationship> relationships)
    {
        if (nodes.Count == 0)
            throw new ArgumentException("Path must contain at least one node", nameof(nodes));

        if (relationships.Count != nodes.Count - 1)
            throw new ArgumentException("Number of relationships must be one less than number of nodes", nameof(relationships));

        Nodes = nodes;
        Relationships = relationships;
    }

    /// <summary>
    /// Creates an empty path with a single node
    /// </summary>
    /// <param name="node">The single node</param>
    public GraphPath(Cvoya.Graph.Model.INode node)
    {
        Nodes = new[] { node };
        Relationships = Array.Empty<Cvoya.Graph.Model.IRelationship>();
    }
}
