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

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Builders;

using Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Builders;

/// <summary>
/// Apache AGE-specific <see cref="CypherQueryBuilder"/> that wires the provider-specific
/// context, expression processor, and collection utilities required by the generic builder.
/// </summary>
internal sealed class AgeCypherQueryBuilder : CypherQueryBuilder
{
    public AgeCypherQueryBuilder(CypherQueryContext context)
        : base(
            new AgeCypherQueryBuilderContext(context),
            new AgeCypherExpressionProcessor(context.Scope, context.LoggerFactory),
            new AgeCypherCollectionProvider())
    {
    }
}

internal sealed class AgeCypherQueryBuilderContext : ICypherQueryBuilderContext
{
    private readonly CypherQueryContext context;

    public AgeCypherQueryBuilderContext(CypherQueryContext context)
    {
        this.context = context;
    }

    public ICypherQueryScope Scope => context.Scope;

    public Microsoft.Extensions.Logging.ILoggerFactory? LoggerFactory => context.LoggerFactory;

    public ICypherCollectionProvider CollectionProvider => new AgeCypherCollectionProvider();

    public ICypherExpressionProcessor ExpressionProcessor => new AgeCypherExpressionProcessor(context.Scope, context.LoggerFactory);
}
