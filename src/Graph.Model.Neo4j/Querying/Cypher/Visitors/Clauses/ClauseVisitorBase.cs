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

internal abstract class ClauseVisitorBase<T> : ExpressionVisitor
{
    protected readonly CypherQueryScope Scope;
    protected readonly CypherQueryBuilder Builder;
    protected readonly ILogger Logger;

    protected ClauseVisitorBase(CypherQueryScope scope, CypherQueryBuilder builder, ILoggerFactory? loggerFactory = null)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Builder = builder ?? throw new ArgumentNullException(nameof(builder));
        Logger = loggerFactory?.CreateLogger<T>() ?? NullLogger<T>.Instance;
    }

    protected string GetCurrentAlias() =>
        Scope.CurrentAlias ?? throw new InvalidOperationException($"No current alias set when processing {GetType().Name}");
}