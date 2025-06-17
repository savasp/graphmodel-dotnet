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

internal abstract class CypherExpressionVisitorBase<T> : ICypherExpressionVisitor
{
    protected CypherExpressionVisitorBase(CypherQueryScope scope, CypherQueryBuilder builder)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Builder = builder ?? throw new ArgumentNullException(nameof(builder));
        Logger = scope.LoggerFactory?.CreateLogger<CypherExpressionVisitorBase<T>>() ?? NullLogger<CypherExpressionVisitorBase<T>>.Instance;
    }

    protected CypherQueryScope Scope { get; }
    protected CypherQueryBuilder Builder { get; }
    protected ILogger<CypherExpressionVisitorBase<T>> Logger { get; }

    public virtual string Visit(Expression node)
    {
        Logger.LogDebug("Visiting expression of type: {NodeType}", node.GetType().FullName);
        Logger.LogDebug("Expression: {Expression}", node);

        return node switch
        {
            BinaryExpression binary => VisitBinary(binary),
            UnaryExpression unary => VisitUnary(unary),
            MemberExpression member => VisitMember(member),
            MethodCallExpression methodCall => VisitMethodCall(methodCall),
            ConstantExpression constant => VisitConstant(constant),
            ParameterExpression parameter => VisitParameter(parameter),
            _ => throw new NotSupportedException($"Expression type {node.NodeType} is not supported")
        };
    }

    public abstract string VisitBinary(BinaryExpression node);
    public abstract string VisitUnary(UnaryExpression node);
    public abstract string VisitMember(MemberExpression node);
    public abstract string VisitMethodCall(MethodCallExpression node);
    public abstract string VisitConstant(ConstantExpression node);
    public abstract string VisitParameter(ParameterExpression node);
}