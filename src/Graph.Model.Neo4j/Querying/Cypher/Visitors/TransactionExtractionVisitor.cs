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
using System.Reflection;
using Cvoya.Graph.Model.Neo4j.Core;


/// <summary>
/// Extracts GraphTransaction instances from an expression tree.
/// </summary>
internal sealed class TransactionExtractionVisitor : ExpressionVisitor
{
    public HashSet<GraphTransaction> Transactions { get; } = new();

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.Name == "WithTransaction" && node.Arguments.Count > 0)
        {
            if (GetConstantValue(node.Arguments[0]) is GraphTransaction transaction)
            {
                Transactions.Add(transaction);
            }
        }

        return base.VisitMethodCall(node);
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        // Check if the constant is a queryable that might have a transaction
        if (node.Value is IGraphQueryable queryable)
        {
            // Use reflection to check if there's a transaction field/property
            var type = queryable.GetType();
            var transactionField = type.GetField("_transaction", BindingFlags.NonPublic | BindingFlags.Instance);

            if (transactionField?.GetValue(queryable) is GraphTransaction transaction)
            {
                Transactions.Add(transaction);
            }
        }

        return base.VisitConstant(node);
    }

    private static object? GetConstantValue(Expression expression)
    {
        return expression switch
        {
            ConstantExpression constant => constant.Value,
            MemberExpression member when member.Expression is ConstantExpression constantExpr =>
                GetMemberValue(member.Member, constantExpr.Value),
            _ => null
        };
    }

    private static object? GetMemberValue(MemberInfo member, object? instance)
    {
        return member switch
        {
            FieldInfo field => field.GetValue(instance),
            PropertyInfo property => property.GetValue(instance),
            _ => null
        };
    }
}