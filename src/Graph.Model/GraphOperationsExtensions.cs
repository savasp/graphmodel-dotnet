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
/// Extension methods for GraphOperationOptions fluent API
/// </summary>
public static class GraphOperationExtensions
{
    /// <summary>
    /// Configure to include immediate relationships (depth = 1)
    /// </summary>
    public static GraphOperationOptions WithRelationships(this GraphOperationOptions options)
    {
        options.TraversalDepth = 1;
        return options;
    }
    
    /// <summary>
    /// Configure to traverse the entire graph (depth = -1)
    /// </summary>
    public static GraphOperationOptions WithFullGraph(this GraphOperationOptions options)
    {
        options.TraversalDepth = -1;
        return options;
    }
    
    /// <summary>
    /// Configure to traverse to a specific depth
    /// </summary>
    public static GraphOperationOptions WithDepth(this GraphOperationOptions options, int depth)
    {
        options.TraversalDepth = depth;
        return options;
    }
    
    /// <summary>
    /// Configure to create missing nodes when creating relationships
    /// </summary>
    public static GraphOperationOptions WithCreateMissingNodes(this GraphOperationOptions options)
    {
        options.CreateMissingNodes = true;
        return options;
    }
    
    /// <summary>
    /// Configure to update existing nodes during traversal
    /// </summary>
    public static GraphOperationOptions WithUpdateExistingNodes(this GraphOperationOptions options)
    {
        options.UpdateExistingNodes = true;
        return options;
    }
    
    /// <summary>
    /// Configure to only process specific relationship types
    /// </summary>
    public static GraphOperationOptions WithRelationshipTypes(this GraphOperationOptions options, params string[] types)
    {
        options.RelationshipTypes = new HashSet<string>(types);
        return options;
    }
}