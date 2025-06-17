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

internal sealed class DistinctVisitor
{
    private readonly QueryScope _scope;
    private readonly CypherQueryBuilder _builder;
    private readonly ILogger<DistinctVisitor> _logger;

    public DistinctVisitor(QueryScope scope, CypherQueryBuilder builder, ILoggerFactory? loggerFactory = null)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _logger = loggerFactory?.CreateLogger<DistinctVisitor>() ?? NullLogger<DistinctVisitor>.Instance;
    }

    public void ApplyDistinct()
    {
        // Mark the query as needing DISTINCT
        _builder.SetDistinct(true);

        // If we don't have a RETURN clause yet, add one
        if (!_builder.HasReturnClause)
        {
            var alias = _scope.CurrentAlias ?? "n";
            _builder.AddReturn($"DISTINCT {alias}");
        }
    }
}