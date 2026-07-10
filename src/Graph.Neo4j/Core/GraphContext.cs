// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.
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

namespace Cvoya.Graph.Neo4j.Core;

using Cvoya.Graph.Neo4j.Entities;
using Cvoya.Graph.Neo4j.Schema;
using Cvoya.Graph.Serialization;
using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;

internal class GraphContext
{
    private Neo4jNodeManager? _nodeManager;
    private Neo4jRelationshipManager? _relationshipManager;
    private EntityFactory? _entityFactory;
    private Neo4jSchemaManager? _schemaManager;
    private readonly SchemaRegistry _schemaRegistry;
    private readonly Func<IDriver> _driverAccessor;

    public Neo4jGraph Graph { get; }
    public IDriver Driver => _driverAccessor();
    public string DatabaseName { get; }
    public ILoggerFactory? LoggerFactory { get; }
    public SchemaRegistry SchemaRegistry => _schemaRegistry;

    internal Neo4jNodeManager NodeManager => _nodeManager ??= new(this);
    internal Neo4jRelationshipManager RelationshipManager => _relationshipManager ??= new(this);
    internal EntityFactory EntityFactory => _entityFactory ??= new(LoggerFactory);
    internal Neo4jSchemaManager SchemaManager => _schemaManager ??= new(this, _schemaRegistry);

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphContext"/> class.
    /// </summary>
    public GraphContext(
        Neo4jGraph graph,
        Func<IDriver> driverAccessor,
        string databaseName,
        ILoggerFactory? loggerFactory,
        SchemaRegistry schemaRegistry)
    {
        Graph = graph ?? throw new ArgumentNullException(nameof(graph));
        _driverAccessor = driverAccessor ?? throw new ArgumentNullException(nameof(driverAccessor));
        DatabaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
        LoggerFactory = loggerFactory;
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
    }
}
