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

using System.Linq.Expressions;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal sealed class AnyVisitor : ExpressionVisitor
{
    private readonly QueryScope _scope;
    private readonly CypherQueryBuilder _builder;
    private readonly ILogger<AnyVisitor> _logger;
    private readonly ILoggerFactory? _loggerFactory;

    public AnyVisitor(QueryScope scope, CypherQueryBuilder builder, ILoggerFactory? loggerFactory = null)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _logger = loggerFactory?.CreateLogger<AnyVisitor>()
            ?? NullLogger<AnyVisitor>.Instance;
        _loggerFactory = loggerFactory;
    }

    public void VisitAny(Expression? predicate = null)
    {
        // If there's a predicate, apply it
        if (predicate != null)
        {
            var whereVisitor = new WhereVisitor(_scope, _builder, _loggerFactory);
            whereVisitor.Visit(predicate);
        }

        // For Any(), we just need to know if at least one exists
        // Use COUNT() > 0 for efficiency
        var alias = _scope.CurrentAlias
            ?? throw new InvalidOperationException("No current alias set when building Any clause");
        _builder.AddReturn($"COUNT({alias}) > 0 AS result");
    }
}