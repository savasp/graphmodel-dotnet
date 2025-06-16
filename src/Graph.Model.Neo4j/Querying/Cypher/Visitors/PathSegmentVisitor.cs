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

internal sealed class PathSegmentVisitor
{
    private readonly QueryScope _scope;
    private readonly CypherQueryBuilder _builder;

    public PathSegmentVisitor(QueryScope scope, CypherQueryBuilder builder)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
    }

    public void BuildPathSegmentQuery(Type sourceType, Type relationshipType, Type targetType)
    {
        // Clear any existing matches - PathSegments should be completely self-contained
        _builder.ClearMatches();

        // Use different aliases to avoid conflicts
        var sourceAlias = "src";  // Don't use scope, use fixed aliases
        var relAlias = "r";
        var targetAlias = "tgt";

        var sourceLabel = Labels.GetLabelFromType(sourceType);
        var relLabel = Labels.GetLabelFromType(relationshipType);
        var targetLabel = Labels.GetLabelFromType(targetType);

        // Build the complete path pattern as a single match
        var pathPattern = $"({sourceAlias}:{sourceLabel})-[{relAlias}:{relLabel}]->({targetAlias}:{targetLabel})";
        _builder.AddMatchPattern(pathPattern);
        _builder.AddReturn($"{sourceAlias}, {relAlias}, {targetAlias}");
    }
}