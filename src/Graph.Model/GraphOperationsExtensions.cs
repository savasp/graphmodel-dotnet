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
/// Extension methods for the <see cref="GraphOperationOptions"/> struct,
/// providing a fluent API for configuration.
/// </summary>
public static class GraphOperationExtensions
{
    /// <summary>
    /// Configures options to include immediate relationships (depth = 1).
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <returns>The updated options with immediate relationship traversal enabled.</returns>
    /// <example>
    /// <code>
    /// var options = new GraphOperationOptions().WithRelationships();
    /// await graph.GetNode&lt;Person&gt;(id, options);
    /// </code>
    /// </example>
    public static GraphOperationOptions WithRelationships(this GraphOperationOptions options)
    {
        options.TraversalDepth = 1;
        return options;
    }
    
    /// <summary>
    /// Configures options to traverse the entire graph (depth = -1).
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <returns>The updated options with full graph traversal enabled.</returns>
    /// <remarks>
    /// Be careful with this option as it could lead to loading large portions of the graph.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = new GraphOperationOptions().WithFullGraph();
    /// await graph.GetNode&lt;Person&gt;(id, options);
    /// </code>
    /// </example>
    public static GraphOperationOptions WithFullGraph(this GraphOperationOptions options)
    {
        options.TraversalDepth = -1;
        return options;
    }
    
    /// <summary>
    /// Configures options to traverse to a specific depth.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="depth">The maximum traversal depth.</param>
    /// <returns>The updated options with the specified traversal depth.</returns>
    /// <example>
    /// <code>
    /// var options = new GraphOperationOptions().WithDepth(2);
    /// await graph.GetNode&lt;Person&gt;(id, options);
    /// </code>
    /// </example>
    public static GraphOperationOptions WithDepth(this GraphOperationOptions options, int depth)
    {
        options.TraversalDepth = depth;
        return options;
    }
    
    /// <summary>
    /// Configures options to create missing nodes when processing relationships.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <returns>The updated options with automatic node creation enabled.</returns>
    /// <example>
    /// <code>
    /// var options = new GraphOperationOptions().WithCreateMissingNodes();
    /// await graph.CreateRelationship(relationship, options);
    /// </code>
    /// </example>
    public static GraphOperationOptions WithCreateMissingNodes(this GraphOperationOptions options)
    {
        options.CreateMissingNodes = true;
        return options;
    }
    
    /// <summary>
    /// Configures options to update existing nodes during traversal.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <returns>The updated options with automatic node updates enabled.</returns>
    /// <example>
    /// <code>
    /// var options = new GraphOperationOptions().WithUpdateExistingNodes();
    /// await graph.CreateRelationship(relationship, options);
    /// </code>
    /// </example>
    public static GraphOperationOptions WithUpdateExistingNodes(this GraphOperationOptions options)
    {
        options.UpdateExistingNodes = true;
        return options;
    }
    
    /// <summary>
    /// Configures options to only process specific relationship types during traversal.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="types">The relationship types to include.</param>
    /// <returns>The updated options with relationship type filtering enabled.</returns>
    /// <example>
    /// <code>
    /// var options = new GraphOperationOptions().WithRelationshipTypes("FOLLOWS", "FRIENDS_WITH");
    /// await graph.GetNode&lt;Person&gt;(id, options);
    /// </code>
    /// </example>
    public static GraphOperationOptions WithRelationshipTypes(this GraphOperationOptions options, params string[] types)
    {
        options.RelationshipTypes = new HashSet<string>(types);
        return options;
    }
}