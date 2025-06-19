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
using System.Reflection;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

internal class ConversionVisitor(
    CypherQueryContext context, ICypherExpressionVisitor nextVisitor)
    : CypherExpressionVisitorBase<ConversionVisitor>(context, nextVisitor)
{
    public override string VisitMethodCall(MethodCallExpression node)
    {
        // Handle implicit/explicit conversion operators (op_Implicit, op_Explicit)
        if (node.Method.Name.StartsWith("op_") && node.Arguments.Count == 1)
        {
            Logger.LogDebug("Visiting conversion operator: {MethodName}", node.Method.Name);
            // For conversion operators, just visit the operand and ignore the conversion
            return NextVisitor!.Visit(node.Arguments[0]);
        }

        // Handle Convert.* methods
        if (node.Method.DeclaringType == typeof(Convert))
        {
            Logger.LogDebug("Visiting Convert method: {MethodName}", node.Method.Name);
            return HandleConvertMethod(node);
        }

        // Handle other type conversion methods (ToString, Parse, etc.)
        if (IsConversionMethod(node.Method))
        {
            Logger.LogDebug("Visiting conversion method: {MethodName}", node.Method.Name);
            return HandleConversionMethod(node);
        }

        return NextVisitor!.VisitMethodCall(node);
    }

    public override string VisitUnary(UnaryExpression node)
    {
        // Handle casting operations: (int)value, (string)value, etc.
        if (node.NodeType == ExpressionType.Convert || node.NodeType == ExpressionType.ConvertChecked)
        {
            Logger.LogDebug("Visiting cast operation to {TargetType}", node.Type.Name);
            return HandleCastOperation(node);
        }

        return NextVisitor!.VisitUnary(node);
    }

    private string HandleConvertMethod(MethodCallExpression node)
    {
        var argument = NextVisitor!.Visit(node.Arguments[0]);

        var expression = node.Method.Name switch
        {
            "ToInt32" or "ToInt16" or "ToInt64" => $"toInteger({argument})",
            "ToDouble" or "ToSingle" or "ToDecimal" => $"toFloat({argument})",
            "ToString" => $"toString({argument})",
            "ToBoolean" => $"toBoolean({argument})",
            "ToDateTime" => $"datetime({argument})",
            _ => throw new NotSupportedException($"Convert method {node.Method.Name} is not supported")
        };

        Logger.LogDebug("Convert method result: {Expression}", expression);
        return expression;
    }

    private string HandleConversionMethod(MethodCallExpression node)
    {
        var methodName = node.Method.Name;
        var declaringType = node.Method.DeclaringType;

        // Handle ToString() calls on various types
        if (methodName == "ToString" && node.Object != null)
        {
            var target = NextVisitor!.Visit(node.Object);
            return $"toString({target})";
        }

        // Handle Parse methods: int.Parse, double.Parse, etc.
        if (methodName == "Parse" && node.Method.IsStatic)
        {
            var argument = NextVisitor!.Visit(node.Arguments[0]);

            var expression = declaringType?.Name switch
            {
                "Int32" or "Int16" or "Int64" => $"toInteger({argument})",
                "Double" or "Single" or "Decimal" => $"toFloat({argument})",
                "Boolean" => $"toBoolean({argument})",
                "DateTime" => $"datetime({argument})",
                _ => throw new NotSupportedException($"Parse method for {declaringType?.Name} is not supported")
            };

            return expression;
        }

        throw new NotSupportedException($"Conversion method {declaringType?.Name}.{methodName} is not supported");
    }

    private string HandleCastOperation(UnaryExpression node)
    {
        var operand = NextVisitor!.Visit(node.Operand);
        var targetType = node.Type;

        // Handle nullable types
        var actualType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        var expression = actualType.Name switch
        {
            "Int32" or "Int16" or "Int64" => $"toInteger({operand})",
            "Double" or "Single" or "Decimal" => $"toFloat({operand})",
            "String" => $"toString({operand})",
            "Boolean" => $"toBoolean({operand})",
            "DateTime" => $"datetime({operand})",
            _ => operand // For complex types or unsupported casts, just return the operand
        };

        Logger.LogDebug("Cast operation result: {Expression}", expression);
        return expression;
    }

    private static bool IsConversionMethod(MethodInfo method)
    {
        var methodName = method.Name;
        var declaringType = method.DeclaringType;

        // Check for common conversion methods
        return methodName switch
        {
            "ToString" => true,
            "Parse" when method.IsStatic => true,
            "TryParse" when method.IsStatic => true,
            _ => false
        };
    }

    public override string VisitBinary(BinaryExpression node) => NextVisitor!.VisitBinary(node);
    public override string VisitMember(MemberExpression node) => NextVisitor!.VisitMember(node);
    public override string VisitConstant(ConstantExpression node) => NextVisitor!.VisitConstant(node);
    public override string VisitParameter(ParameterExpression node) => NextVisitor!.VisitParameter(node);
}