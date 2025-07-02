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

namespace Cvoya.Graph.Model.Neo4j.Core;

using Cvoya.Graph.Model.Configuration;
using Cvoya.Graph.Model.Neo4j.Entities;
using Cvoya.Graph.Model.Neo4j.Schema;
using Cvoya.Graph.Model.Serialization;
using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;

internal class GraphContext
{
    private Neo4jNodeManager? _nodeManager;
    private Neo4jRelationshipManager? _relationshipManager;
    private EntityFactory? _entityFactory;
    private Neo4jSchemaManager? _schemaManager;
    private readonly PropertyConfigurationRegistry _propertyConfigurationRegistry;

    public Neo4jGraph Graph { get; }
    public IDriver Driver { get; }
    public string DatabaseName { get; }
    public ILoggerFactory? LoggerFactory { get; }
    public PropertyConfigurationRegistry PropertyConfigurationRegistry => _propertyConfigurationRegistry;

    internal Neo4jNodeManager NodeManager => _nodeManager ??= new(this);
    internal Neo4jRelationshipManager RelationshipManager => _relationshipManager ??= new(this);
    internal EntityFactory EntityFactory => _entityFactory ??= new(LoggerFactory);
    internal Neo4jSchemaManager SchemaManager => _schemaManager ??= new(this, _propertyConfigurationRegistry);

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphContext"/> class.
    /// </summary>
    public GraphContext(
        Neo4jGraph graph,
        IDriver driver,
        string databaseName,
        ILoggerFactory? loggerFactory = null,
        PropertyConfigurationRegistry? registry = null)
    {
        Graph = graph ?? throw new ArgumentNullException(nameof(graph));
        Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        DatabaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
        LoggerFactory = loggerFactory;
        _propertyConfigurationRegistry = registry ?? new PropertyConfigurationRegistry();
    }
}