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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors;

using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal sealed class RelationshipsVisitor
{
    private readonly QueryScope _scope;
    private readonly CypherQueryBuilder _builder;
    private readonly ILogger<RelationshipsVisitor> _logger;

    public RelationshipsVisitor(QueryScope scope, CypherQueryBuilder builder, ILoggerFactory? loggerFactory = null)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _logger = loggerFactory?.CreateLogger<RelationshipsVisitor>()
            ?? NullLogger<RelationshipsVisitor>.Instance;
    }

    public void VisitRelationships(Type? relationshipType = null, RelationshipDirection direction = RelationshipDirection.Both)
    {
        var nodeAlias = _scope.CurrentAlias ?? "n";
        var relAlias = _scope.GetOrCreateAlias(relationshipType ?? typeof(IRelationship), "r");
        var otherAlias = _scope.GetOrCreateAlias(typeof(INode), "other");

        // Build the pattern based on direction
        var pattern = direction switch
        {
            RelationshipDirection.Outgoing => $"({nodeAlias})-[{relAlias}]->({otherAlias})",
            RelationshipDirection.Incoming => $"({nodeAlias})<-[{relAlias}]-({otherAlias})",
            RelationshipDirection.Both => $"({nodeAlias})-[{relAlias}]-({otherAlias})",
            _ => throw new ArgumentException($"Unknown direction: {direction}")
        };

        // Add type constraint if specified
        if (relationshipType != null)
        {
            var relLabel = Labels.GetLabelFromType(relationshipType);
            pattern = pattern.Replace($"[{relAlias}]", $"[{relAlias}:{relLabel}]");
        }

        _builder.AddMatchPattern(pattern);
        _builder.AddReturn(relAlias);

        // Update current alias to the relationship
        _scope.CurrentAlias = relAlias;
    }
}

internal enum RelationshipDirection
{
    Both,
    Outgoing,
    Incoming
}