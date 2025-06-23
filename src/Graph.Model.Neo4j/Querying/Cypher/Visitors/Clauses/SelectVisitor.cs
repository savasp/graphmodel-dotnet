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
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Expressions;
using Microsoft.Extensions.Logging;

internal sealed class SelectVisitor(CypherQueryContext context) : ClauseVisitorBase<SelectVisitor>(context)
{
    private readonly Stack<(string Expression, string? Alias)> _projections = new();
    private string? _currentMemberName;

    protected override Expression VisitNew(NewExpression node)
    {
        // Handle anonymous type projections like: .Select(x => new { x.Name, x.Age })
        for (int i = 0; i < node.Arguments.Count; i++)
        {
            if (node.Members?[i] is { } member)
            {
                _currentMemberName = member.Name;
                Visit(node.Arguments[i]);
                _currentMemberName = null;
            }
        }

        // Add all projections to the builder
        while (_projections.TryPop(out var projection))
        {
            Builder.AddReturn(projection.Expression, projection.Alias);
        }

        return node;
    }

    protected override Expression VisitMemberInit(MemberInitExpression node)
    {
        // Handle member initialization like: .Select(x => new Person { Name = x.Name })
        Visit(node.NewExpression);

        foreach (var binding in node.Bindings.OfType<MemberAssignment>())
        {
            _currentMemberName = binding.Member.Name;
            Visit(binding.Expression);
            _currentMemberName = null;
        }

        // Add all projections to the builder
        while (_projections.TryPop(out var projection))
        {
            Builder.AddReturn(projection.Expression, projection.Alias);
        }

        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        Logger.LogDebug("SelectVisitor.VisitMember called for: {Expression}", node);

        // Use the same expression visitor chain as WhereVisitor to handle complex properties
        var expressionVisitor = new ExpressionVisitorChainFactory(Context)
            .CreateWhereClauseChain(Context.Scope.CurrentAlias ?? "src");

        var path = expressionVisitor.VisitMember(node);
        Logger.LogDebug("SelectVisitor generated path: {Path}", path);

        // If we're selecting a single property without aliasing
        if (_currentMemberName == null && node.Expression?.Type != null)
        {
            _projections.Push((path, null));
        }
        else
        {
            _projections.Push((path, _currentMemberName));
        }

        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Handle method calls in projections
        var expression = node.Method.Name switch
        {
            "Count" when node.Object == null => HandleAggregateFunction(node, "count"),
            "Sum" => HandleAggregateFunction(node, "sum"),
            "Average" => HandleAggregateFunction(node, "avg"),
            "Min" => HandleAggregateFunction(node, "min"),
            "Max" => HandleAggregateFunction(node, "max"),
            "ToLower" => HandleStringFunction(node, "toLower"),
            "ToUpper" => HandleStringFunction(node, "toUpper"),
            "ToString" => HandleStringFunction(node, "toString"),
            _ => throw new NotSupportedException($"Method {node.Method.Name} is not supported in SELECT clause")
        };

        _projections.Push((expression, _currentMemberName));
        return node;
    }

    private string HandleAggregateFunction(MethodCallExpression node, string functionName)
    {
        // Existing implementation
        throw new NotImplementedException("HandleAggregateFunction needs to be implemented");
    }

    private string HandleStringFunction(MethodCallExpression node, string functionName)
    {
        // Existing implementation  
        throw new NotImplementedException("HandleStringFunction needs to be implemented");
    }
}