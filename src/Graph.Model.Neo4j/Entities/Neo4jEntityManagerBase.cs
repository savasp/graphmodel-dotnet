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

using System.Reflection;
using Neo4j.Driver;

namespace Cvoya.Graph.Model.Neo4j;

/// <summary>
/// Base class for Neo4j entity managers that provides common functionality.
/// </summary>
internal abstract class Neo4jEntityManagerBase
{
    protected readonly GraphContext GraphContext;

    /// <summary>
    /// Initializes a new instance of the Neo4jEntityManagerBase class.
    /// </summary>
    /// <param name="graphContext">The graph context</param>
    protected Neo4jEntityManagerBase(GraphContext graphContext)
    {
        GraphContext = graphContext ?? throw new ArgumentNullException(nameof(graphContext));
    }

    protected (IDictionary<PropertyInfo, object?> simpleProps, IDictionary<PropertyInfo, object?> complexProps)
        GetSimpleAndComplexProperties(object entity) => GraphDataModel.GetSimpleAndComplexProperties(entity);

    /// <summary>
    /// Converts property-value pairs to a Neo4j-compatible dictionary.
    /// </summary>
    /// <param name="props">The properties to convert</param>
    /// <returns>A dictionary with property names and Neo4j-compatible values</returns>
    public IDictionary<string, object?> ConvertPropertiesToNeo4j(IDictionary<PropertyInfo, object?> props)
    {
        var result = new Dictionary<string, object?>();
        foreach (var kvp in props)
        {
            var name = kvp.Key.GetCustomAttribute<PropertyAttribute>()?.Label ?? kvp.Key.Name;
            result[name] = GraphContext.EntityConverter.ConvertToNeo4jValue(kvp.Value);
        }
        return result;
    }

    /// <summary>
    /// Logs a message with the provided action if a logger is available.
    /// </summary>
    /// <param name="logAction">The action to execute with the logger</param>
    protected void Log(Action<Microsoft.Extensions.Logging.ILogger> logAction)
    {
        if (GraphContext.LoggerFactory != null)
        {
            logAction(GraphContext.LoggerFactory);
        }
    }

    /// <summary>
    /// Checks if a node exists with the specified ID.
    /// </summary>
    /// <param name="nodeId">The ID of the node to check</param>
    /// <param name="tx">The transaction to use</param>
    /// <returns>True if the node exists, false otherwise</returns>
    protected static async Task<bool> NodeExists(string nodeId, IAsyncTransaction tx)
    {
        var cypher = $"MATCH (n) WHERE n.{nameof(Model.INode.Id)} = '{nodeId}' RETURN COUNT(n) as count";
        var result = await tx.RunAsync(cypher);
        var record = await result.SingleAsync();
        return record["count"].As<long>() > 0;
    }
}
