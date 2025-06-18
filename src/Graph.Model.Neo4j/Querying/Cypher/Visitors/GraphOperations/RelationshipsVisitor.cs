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

using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;

internal sealed class RelationshipsVisitor(CypherQueryContext context) : CypherVisitorBase<RelationshipsVisitor>(context)
{
    public void VisitRelationships(Type? relationshipType = null, RelationshipDirection direction = RelationshipDirection.Both)
    {
        var nodeAlias = Scope.CurrentAlias ?? "n";
        var relAlias = Scope.GetOrCreateAlias(relationshipType ?? typeof(IRelationship), "r");
        var otherAlias = Scope.GetOrCreateAlias(typeof(INode), "other");

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

        Builder.AddMatchPattern(pattern);
        Builder.AddReturn(relAlias);

        // Update current alias to the relationship
        Scope.CurrentAlias = relAlias;
    }
}

internal enum RelationshipDirection
{
    Both,
    Outgoing,
    Incoming
}