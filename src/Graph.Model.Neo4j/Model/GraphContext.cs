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

using Cvoya.Graph.Model.Neo4j.Serialization;
using global::Neo4j.Driver;
using Microsoft.Extensions.Logging;

namespace Cvoya.Graph.Model.Neo4j;

internal record GraphContext(
    Graph Graph,
    IDriver Driver,
    string DatabaseName,
    ILoggerFactory? LoggerFactory
)
{
    private Neo4jConstraintManager? _constraintManager;
    private Neo4jNodeManager? _nodeManager;
    private Neo4jRelationshipManager? _relationshipManager;

    internal Neo4jConstraintManager ConstraintManager => _constraintManager ??= new(this);
    internal Neo4jNodeManager NodeManager => _nodeManager ??= new(this);
    internal Neo4jRelationshipManager RelationshipManager => _relationshipManager ??= new(this);
}