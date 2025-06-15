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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;

internal sealed class TraverseBuilder
{
    private readonly QueryScope _scope;
    private readonly CypherQueryBuilder _queryBuilder;

    public TraverseBuilder(QueryScope scope, CypherQueryBuilder queryBuilder)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _queryBuilder = queryBuilder ?? throw new ArgumentNullException(nameof(queryBuilder));
    }

    public void BuildTraversal(Type relationshipType, Type targetType)
    {
        // Get the current node alias (source of traversal)
        var sourceAlias = _scope.CurrentAlias ?? "n";
        
        // Create aliases for the relationship and target
        var relAlias = _scope.GetOrCreateAlias(relationshipType, "r");
        var targetAlias = _scope.GetOrCreateAlias(targetType, "t");
        
        // Get labels
        var relLabel = Labels.GetLabelFromType(relationshipType);
        var targetLabel = Labels.GetLabelFromType(targetType);
        
        // Build the traversal pattern
        var pattern = $"({sourceAlias})-[{relAlias}:{relLabel}]->({targetAlias}:{targetLabel})";
        
        _queryBuilder.AddMatch(pattern);
        
        // Update the current alias to the target
        _scope.CurrentAlias = targetAlias;
    }
}