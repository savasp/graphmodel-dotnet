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

namespace Cvoya.Graph.Model.Age.Core;

using System.Data;

using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Age.Core.Entities;

using Microsoft.Extensions.Logging;
using Npgsql;

/// <summary>
/// Central coordination point for Apache AGE services. Works with a single active connection.
/// The surface will expand as providers for schema, entities, and queries come online.
/// </summary>
internal sealed class AgeGraphContext
{
    public AgeGraphContext(
        AgeGraph graph,
        NpgsqlConnection connection,
        string graphName,
        SchemaRegistry schemaRegistry,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(graphName);
        ArgumentNullException.ThrowIfNull(schemaRegistry);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        Graph = graph;
        Connection = connection;
        GraphName = graphName;
        SchemaRegistry = schemaRegistry;
        LoggerFactory = loggerFactory;
        NodeManager = new AgeNodeManager(this);
        RelationshipManager = new AgeRelationshipManager(this);
    }

    internal AgeGraph Graph { get; }

    internal NpgsqlConnection Connection { get; }

    internal string GraphName { get; }

    internal SchemaRegistry SchemaRegistry { get; }

    internal ILoggerFactory LoggerFactory { get; }

    internal AgeNodeManager NodeManager { get; }

    internal AgeRelationshipManager RelationshipManager { get; }

    internal AgeGraphTransaction CreateTransaction(bool isReadOnly = false, IsolationLevel isolationLevel = IsolationLevel.ReadCommitted) =>
        new(this, isReadOnly, isolationLevel);
}
