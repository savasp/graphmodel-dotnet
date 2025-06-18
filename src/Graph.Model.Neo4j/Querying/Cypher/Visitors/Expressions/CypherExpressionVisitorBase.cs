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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Expressions;

using System.Linq.Expressions;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

internal abstract class CypherExpressionVisitorBase<T> : CypherVisitorBase<T>, ICypherExpressionVisitor
{
    protected CypherExpressionVisitorBase(CypherQueryContext context, ICypherExpressionVisitor? nextVisitor = null)
        : base(context)
    {
        NextVisitor = nextVisitor;
    }

    protected ICypherExpressionVisitor? NextVisitor { get; }

    public new virtual string Visit(Expression node)
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

    public new abstract string VisitBinary(BinaryExpression node);
    public new abstract string VisitUnary(UnaryExpression node);
    public new abstract string VisitMember(MemberExpression node);
    public new abstract string VisitMethodCall(MethodCallExpression node);
    public new abstract string VisitConstant(ConstantExpression node);
    public new abstract string VisitParameter(ParameterExpression node);
}