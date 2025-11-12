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

using System.Globalization;
using System.Linq.Expressions;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Builders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Apache AGE implementation of <see cref="ICypherExpressionProcessor"/>. AGE exposes the
/// openCypher surface, so the processor focuses on translating common LINQ expressions to
/// standard Cypher constructs without relying on APOC or database-specific extensions.
/// </summary>
internal sealed class AgeCypherExpressionProcessor : ICypherExpressionProcessor
{
    private readonly ICypherQueryScope scope;
    private readonly ILogger logger;

    public AgeCypherExpressionProcessor(ICypherQueryScope scope, ILoggerFactory? loggerFactory = null)
    {
        this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
        logger = loggerFactory?.CreateLogger<AgeCypherExpressionProcessor>() ?? NullLogger<AgeCypherExpressionProcessor>.Instance;
    }

    public string ProcessExpression(Expression expression, string alias)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);

        var processor = new SimpleExpressionProcessor(scope, alias, logger);
        return processor.VisitExpression(expression);
    }

    private sealed class SimpleExpressionProcessor
    {
        private readonly ICypherQueryScope scope;
        private readonly string alias;
        private readonly ILogger logger;

        public SimpleExpressionProcessor(ICypherQueryScope scope, string alias, ILogger logger)
        {
            this.scope = scope;
            this.alias = alias;
            this.logger = logger;
        }

        public string VisitExpression(Expression expression)
            => expression switch
            {
                BinaryExpression binary => VisitBinary(binary),
                MemberExpression member => VisitMember(member),
                ConstantExpression constant => FormatConstant(constant.Value),
                ParameterExpression => alias,
                MethodCallExpression method => VisitMethodCall(method),
                UnaryExpression unary => VisitUnary(unary),
                LambdaExpression lambda => VisitExpression(lambda.Body),
                _ => throw new NotSupportedException($"Expression of type {expression.NodeType} is not supported in AGE expression translation.")
            };

        private string VisitBinary(BinaryExpression binary)
        {
            var left = VisitExpression(binary.Left);
            var right = VisitExpression(binary.Right);

            var op = binary.NodeType switch
            {
                ExpressionType.Equal => "=",
                ExpressionType.NotEqual => "<>",
                ExpressionType.GreaterThan => ">",
                ExpressionType.GreaterThanOrEqual => ">=",
                ExpressionType.LessThan => "<",
                ExpressionType.LessThanOrEqual => "<=",
                ExpressionType.AndAlso => "AND",
                ExpressionType.OrElse => "OR",
                ExpressionType.Add => "+",
                ExpressionType.Subtract => "-",
                ExpressionType.Multiply => "*",
                ExpressionType.Divide => "/",
                _ => throw new NotSupportedException($"Binary operator {binary.NodeType} is not supported in AGE expression translation.")
            };

            if (op is "AND" or "OR")
            {
                return $"({left}) {op} ({right})";
            }

            return $"({left} {op} {right})";
        }

        private string VisitMember(MemberExpression member)
        {
            if (member.Expression is ParameterExpression parameter)
            {
                if (typeof(IRelationship).IsAssignableFrom(parameter.Type))
                {
                    return member.Member.Name switch
                    {
                        nameof(IRelationship.StartNodeId) => "src.Id",
                        nameof(IRelationship.EndNodeId) => "tgt.Id",
                        _ => $"{alias}.{member.Member.Name}"
                    };
                }

                return $"{alias}.{member.Member.Name}";
            }

            if (member.Expression is MemberExpression nestedMember &&
                nestedMember.Expression is ParameterExpression pathParameter &&
                typeof(IGraphPathSegment).IsAssignableFrom(pathParameter.Type))
            {
                var pathAlias = nestedMember.Member.Name switch
                {
                    nameof(IGraphPathSegment.StartNode) => "src0",
                    nameof(IGraphPathSegment.EndNode) => "tgt0",
                    nameof(IGraphPathSegment.Relationship) => "r0",
                    _ => throw new NotSupportedException($"Path segment member '{nestedMember.Member.Name}' is not supported by the AGE provider.")
                };

                return $"{pathAlias}.{member.Member.Name}";
            }

            // Closure captured values or computed members
            try
            {
                var value = Expression.Lambda(member).Compile().DynamicInvoke();
                return FormatConstant(value);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to evaluate member expression {Expression}", member);
                var parent = member.Expression is null ? alias : VisitExpression(member.Expression);
                return $"{parent}.{member.Member.Name}";
            }
        }

        private string VisitMethodCall(MethodCallExpression method)
        {
            if (method.Method.DeclaringType == typeof(string))
            {
                var target = VisitExpression(method.Object!);
                return method.Method.Name switch
                {
                    nameof(string.Contains) when method.Arguments.Count == 1 =>
                        $"{target} CONTAINS {VisitExpression(method.Arguments[0])}",
                    nameof(string.StartsWith) when method.Arguments.Count == 1 =>
                        $"{target} STARTS WITH {VisitExpression(method.Arguments[0])}",
                    nameof(string.EndsWith) when method.Arguments.Count == 1 =>
                        $"{target} ENDS WITH {VisitExpression(method.Arguments[0])}",
                    _ => throw new NotSupportedException($"String method '{method.Method.Name}' is not supported by the AGE provider.")
                };
            }

            if (method.Method.DeclaringType == typeof(DateTime) || method.Method.DeclaringType == typeof(DateTimeOffset))
            {
                return VisitDateTimeMethod(method);
            }

            if (method.Method.DeclaringType?.Name == "DynamicEntityExtensions")
            {
                return VisitDynamicEntityExtension(method);
            }

            try
            {
                var value = Expression.Lambda(method).Compile().DynamicInvoke();
                return FormatConstant(value);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to evaluate method call {Method}", method.Method.Name);
                throw new NotSupportedException($"Method '{method.Method.Name}' could not be translated for AGE.");
            }
        }

        private string VisitDateTimeMethod(MethodCallExpression method)
        {
            var target = method.Object is null ? null : VisitExpression(method.Object);
            var methodName = method.Method.Name;

            // AGE inherits PostgreSQL temporal functions; use ISO strings the same way as openCypher.
            if (methodName is "AddYears" or "AddMonths" or "AddDays" or "AddHours" or "AddMinutes" or "AddSeconds")
            {
                string durationField = methodName switch
                {
                    "AddYears" => "years",
                    "AddMonths" => "months",
                    "AddDays" => "days",
                    "AddHours" => "hours",
                    "AddMinutes" => "minutes",
                    "AddSeconds" => "seconds",
                    _ => throw new NotSupportedException()
                };

                var amount = VisitExpression(method.Arguments[0]);
                var baseExpression = target ?? FormatConstant(DateTime.UtcNow);
                return $"{baseExpression} + duration({{ {durationField}: {amount} }})";
            }

            try
            {
                var evaluated = Expression.Lambda(method).Compile().DynamicInvoke();
                return FormatConstant(evaluated);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to evaluate DateTime method {Method}", method.Method.Name);
                throw new NotSupportedException($"DateTime method '{method.Method.Name}' is not supported by the AGE provider.");
            }
        }

        private string VisitDynamicEntityExtension(MethodCallExpression method)
        {
            if (method.Arguments.Count < 1)
            {
                throw new NotSupportedException($"Dynamic entity extension '{method.Method.Name}' requires at least one argument.");
            }

            var target = VisitExpression(method.Arguments[0]);
            var args = method.Arguments.Skip(1).Select(VisitExpression).ToArray();

            return method.Method.Name switch
            {
                "HasLabel" when args.Length == 1 => $"{args[0]} IN labels({target})",
                "HasType" when args.Length == 1 => $"type({target}) = {args[0]}",
                "HasProperty" when args.Length == 1 => $"{target}.{{{TrimQuotes(args[0])}}} IS NOT NULL",
                "GetProperty" when args.Length == 1 => $"{target}.{{{TrimQuotes(args[0])}}}",
                _ => throw new NotSupportedException($"Dynamic entity extension '{method.Method.Name}' is not supported by the AGE provider.")
            };
        }

        private string VisitUnary(UnaryExpression unary)
        {
            var operand = VisitExpression(unary.Operand);
            return unary.NodeType switch
            {
                ExpressionType.Not => $"NOT ({operand})",
                ExpressionType.Convert => operand,
                _ => throw new NotSupportedException($"Unary operator {unary.NodeType} is not supported by the AGE provider.")
            };
        }

        private static string FormatConstant(object? value)
        {
            return value switch
            {
                null => "null",
                string s => $"'{s.Replace("'", "\\'")}'",
                bool b => b ? "true" : "false",
                Guid g => $"'{g}'",
                DateTime dt => $"datetime('{dt.ToUniversalTime():yyyy-MM-ddTHH:mm:ss.fffZ}')",
                DateTimeOffset dto => $"datetime('{dto.UtcDateTime:yyyy-MM-ddTHH:mm:ss.fffZ}')",
                Enum e => Convert.ToInt64(e, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
                byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal =>
                    Convert.ToString(value, CultureInfo.InvariantCulture)!,
                _ => $"'{value}'"
            };
        }

        private static string TrimQuotes(string value)
            => value.Trim('"', '\'');
    }
}
