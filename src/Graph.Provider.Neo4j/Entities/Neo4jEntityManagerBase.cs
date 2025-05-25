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

using System.Collections;
using System.Reflection;
using Cvoya.Graph.Model;
using Cvoya.Graph.Provider.Neo4j.Conversion;
using Cvoya.Graph.Provider.Neo4j.Query;
using Cvoya.Graph.Provider.Neo4j.Schema;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace Cvoya.Graph.Provider.Neo4j.Entities;

/// <summary>
/// Base class for Neo4j entity managers that provides common functionality.
/// </summary>
internal abstract class Neo4jEntityManagerBase
{
    protected readonly Neo4jQueryExecutor QueryExecutor;
    protected readonly Neo4jConstraintManager ConstraintManager;
    protected readonly Neo4jEntityConverter EntityConverter;
    protected readonly Microsoft.Extensions.Logging.ILogger? Logger;

    /// <summary>
    /// Initializes a new instance of the Neo4jEntityManagerBase class.
    /// </summary>
    /// <param name="queryExecutor">The query executor service</param>
    /// <param name="constraintManager">The constraint manager service</param>
    /// <param name="entityConverter">The entity converter service</param>
    /// <param name="logger">Optional logger</param>
    protected Neo4jEntityManagerBase(
        Neo4jQueryExecutor queryExecutor,
        Neo4jConstraintManager constraintManager,
        Neo4jEntityConverter entityConverter,
        Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        QueryExecutor = queryExecutor ?? throw new ArgumentNullException(nameof(queryExecutor));
        ConstraintManager = constraintManager ?? throw new ArgumentNullException(nameof(constraintManager));
        EntityConverter = entityConverter ?? throw new ArgumentNullException(nameof(entityConverter));
        Logger = logger;
    }

    /// <summary>
    /// Gets the simple and complex properties of an object.
    /// </summary>
    /// <param name="obj">The object to examine</param>
    /// <returns>A tuple containing dictionaries of simple and complex properties</returns>
    protected (Dictionary<PropertyInfo, object?>, Dictionary<PropertyInfo, object?>) GetSimpleAndComplexProperties(object obj)
    {
        var simpleProperties = new Dictionary<PropertyInfo, object?>();
        var complexProperties = new Dictionary<PropertyInfo, object?>();

        var properties = obj.GetType().GetProperties();

        foreach (var property in properties)
        {
            if (IsRelationshipType(property.PropertyType) || IsCollectionOfRelationshipType(property.PropertyType))
            {
                continue;
            }
            else if (IsPrimitiveOrSimple(property.PropertyType) || IsCollectionOfSimple(property.PropertyType))
            {
                simpleProperties[property] = property.GetValue(obj);
            }
            else
            {
                complexProperties[property] = property.GetValue(obj);
            }
        }

        return (simpleProperties, complexProperties);
    }

    /// <summary>
    /// Checks if a type is a relationship type.
    /// </summary>
    protected static bool IsRelationshipType(Type type) =>
        Helpers.IsRelationshipType(type);

    /// <summary>
    /// Checks if a type is a collection of relationship types.
    /// </summary>
    protected static bool IsCollectionOfRelationshipType(Type type) =>
        type != typeof(string)
        && typeof(IEnumerable).IsAssignableFrom(type)
        && type switch
        {
            { IsArray: true } => typeof(Model.IRelationship).IsAssignableFrom(type.GetElementType()!),
            { IsGenericType: true } => type.GetGenericArguments().FirstOrDefault() is { } arg && typeof(Model.IRelationship).IsAssignableFrom(arg),
            _ => false
        };

    /// <summary>
    /// Checks if a type is a primitive or simple type.
    /// </summary>
    protected static bool IsPrimitiveOrSimple(Type type) => type switch
    {
        _ when type.IsPrimitive => true,
        _ when type.IsEnum => true,
        _ when type == typeof(string) => true,
        _ when type.IsValueType => true,
        _ when type == typeof(decimal) => true,
        _ when type == typeof(Model.Point) => true,
        _ => false
    };

    /// <summary>
    /// Checks if a type is a collection of simple types.
    /// </summary>
    protected static bool IsCollectionOfSimple(Type type) =>
        type != typeof(string)
        && typeof(IEnumerable).IsAssignableFrom(type)
        && type switch
        {
            { IsArray: true } => type.GetElementType()!.IsPrimitiveOrSimple(),
            { IsGenericType: true } => type.GetGenericArguments().FirstOrDefault() is { } arg && arg.IsPrimitiveOrSimple(),
            _ => false
        };

    /// <summary>
    /// Converts property-value pairs to a Neo4j-compatible dictionary.
    /// </summary>
    /// <param name="props">The properties to convert</param>
    /// <returns>A dictionary with property names and Neo4j-compatible values</returns>
    protected Dictionary<string, object?> ConvertPropertiesToNeo4j(Dictionary<PropertyInfo, object?> props)
    {
        var result = new Dictionary<string, object?>();
        foreach (var kvp in props)
        {
            var name = kvp.Key.GetCustomAttribute<PropertyAttribute>()?.Label ?? kvp.Key.Name;
            result[name] = EntityConverter.ConvertToNeo4jValue(kvp.Value);
        }
        return result;
    }

    /// <summary>
    /// Logs a message with the provided action if a logger is available.
    /// </summary>
    /// <param name="logAction">The action to execute with the logger</param>
    protected void Log(Action<Microsoft.Extensions.Logging.ILogger> logAction)
    {
        if (Logger != null)
        {
            logAction(Logger);
        }
    }

    /// <summary>
    /// Checks if a node exists with the specified ID.
    /// </summary>
    /// <param name="nodeId">The ID of the node to check</param>
    /// <param name="tx">The transaction to use</param>
    /// <returns>True if the node exists, false otherwise</returns>
    protected async Task<bool> NodeExists(string nodeId, IAsyncTransaction tx)
    {
        var cypher = $"MATCH (n) WHERE n.{nameof(Model.INode.Id)} = '{nodeId}' RETURN COUNT(n) as count";
        var result = await tx.RunAsync(cypher);
        var record = await result.SingleAsync();
        return record["count"].As<long>() > 0;
    }

    /// <summary>
    /// Checks if a relationship exists with the specified ID.
    /// </summary>
    /// <param name="relId">The ID of the relationship to check</param>
    /// <param name="tx">The transaction to use</param>
    /// <returns>True if the relationship exists, false otherwise</returns>
    protected async Task<bool> RelationshipExists(string relId, IAsyncTransaction tx)
    {
        var cypher = $"MATCH ()-[r]->() WHERE r.{nameof(Model.IRelationship.Id)} = '{relId}' RETURN COUNT(r) as count";
        var result = await tx.RunAsync(cypher);
        var record = await result.SingleAsync();
        return record["count"].As<long>() > 0;
    }
}