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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

internal class StringMethodVisitor : CypherExpressionVisitorBase
{
    private readonly ICypherExpressionVisitor _innerVisitor;
    private readonly ILogger<StringMethodVisitor> _logger;

    public StringMethodVisitor(ICypherExpressionVisitor innerVisitor, ILoggerFactory? loggerFactory = null)
    {
        _innerVisitor = innerVisitor;
        _logger = loggerFactory?.CreateLogger<StringMethodVisitor>() ?? NullLogger<StringMethodVisitor>.Instance;
    }

    public override string VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(string))
        {
            return _innerVisitor.VisitMethodCall(node);
        }

        _logger.LogDebug("Visiting string method: {MethodName}", node.Method.Name);

        var target = _innerVisitor.Visit(node.Object!);
        var arguments = node.Arguments.Select(arg => _innerVisitor.Visit(arg)).ToList();

        var expression = node.Method.Name switch
        {
            "Contains" => $"{target} CONTAINS {arguments[0]}",
            "StartsWith" => $"{target} STARTS WITH {arguments[0]}",
            "EndsWith" => $"{target} ENDS WITH {arguments[0]}",
            "ToLower" => $"toLower({target})",
            "ToUpper" => $"toUpper({target})",
            "Trim" => $"trim({target})",
            "TrimStart" => $"ltrim({target})",
            "TrimEnd" => $"rtrim({target})",
            "Replace" => $"replace({target}, {arguments[0]}, {arguments[1]})",
            "Substring" => arguments.Count == 1
                ? $"substring({target}, {arguments[0]})"
                : $"substring({target}, {arguments[0]}, {arguments[1]})",
            "Length" => $"length({target})",
            _ => throw new NotSupportedException($"String method {node.Method.Name} is not supported")
        };

        _logger.LogDebug("String method result: {Expression}", expression);
        return expression;
    }

    public override string VisitBinary(BinaryExpression node) => _innerVisitor.VisitBinary(node);
    public override string VisitUnary(UnaryExpression node) => _innerVisitor.VisitUnary(node);
    public override string VisitMember(MemberExpression node) => _innerVisitor.VisitMember(node);
    public override string VisitConstant(ConstantExpression node) => _innerVisitor.VisitConstant(node);
    public override string VisitParameter(ParameterExpression node) => _innerVisitor.VisitParameter(node);
}