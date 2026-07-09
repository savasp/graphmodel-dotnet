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

using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Cypher.Ast;
using Cvoya.Graph.Model.Cypher.Ast.Expressions;

namespace Cvoya.Graph.Model.Cypher.Validation;

/// <summary>
/// Validates basic Cypher AST well-formedness.
/// </summary>
public sealed class CypherAstValidator : ICypherPass
{
    /// <inheritdoc />
    public CypherStatement Run(CypherStatement input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var scope = new HashSet<string>(StringComparer.Ordinal);

        foreach (var clause in input.Clauses)
        {
            switch (clause)
            {
                case MatchClause match:
                    BindMatchAliases(scope, match);
                    break;

                case WhereClause where:
                    ValidateExpression(where.Predicate, scope, input.Parameters);
                    break;

                case WithClause with:
                    ValidateReturnItems(with.Items, scope, input.Parameters);
                    scope = ProjectWithScope(with);
                    break;

                case UnwindClause unwind:
                    ValidateExpression(unwind.Source, scope, input.Parameters);
                    scope.Add(unwind.Alias);
                    break;

                case CallClause call:
                    ValidateExpressions(call.Arguments, scope, input.Parameters);
                    foreach (var yield in call.Yields)
                    {
                        scope.Add(yield);
                    }

                    break;

                case ReturnClause @return:
                    ValidateReturnItems(@return.Items, scope, input.Parameters);
                    break;

                case OrderByClause orderBy:
                    foreach (var item in orderBy.Items)
                    {
                        ValidateExpression(item.Expression, scope, input.Parameters);
                    }

                    break;

                case SkipClause skip:
                    ValidateExpression(skip.Count, scope, input.Parameters);
                    break;

                case LimitClause limit:
                    ValidateExpression(limit.Count, scope, input.Parameters);
                    break;

                default:
                    throw new GraphException($"Unsupported Cypher clause '{clause.GetType().Name}'.");
            }
        }

        return input;
    }

    private static void BindMatchAliases(HashSet<string> scope, MatchClause match)
    {
        foreach (var pattern in match.Patterns)
        {
            foreach (var element in pattern.Elements)
            {
                switch (element)
                {
                    case NodePattern { Alias: not null } node:
                        scope.Add(node.Alias);
                        break;

                    case RelationshipPattern { Alias: not null } relationship:
                        scope.Add(relationship.Alias);
                        break;
                }
            }
        }
    }

    private static HashSet<string> ProjectWithScope(WithClause with)
    {
        var scope = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in with.Items)
        {
            if (item.Alias is not null)
            {
                scope.Add(item.Alias);
            }
            else if (item.Expression is VariableRef variable)
            {
                scope.Add(variable.Alias);
            }
        }

        return scope;
    }

    private static void ValidateReturnItems(
        IReadOnlyList<ReturnItem> items,
        IReadOnlySet<string> scope,
        IReadOnlyDictionary<string, object?> parameters)
    {
        foreach (var item in items)
        {
            ValidateExpression(item.Expression, scope, parameters);
        }
    }

    private static void ValidateExpressions(
        IReadOnlyList<CypherExpression> expressions,
        IReadOnlySet<string> scope,
        IReadOnlyDictionary<string, object?> parameters)
    {
        foreach (var expression in expressions)
        {
            ValidateExpression(expression, scope, parameters);
        }
    }

    private static void ValidateExpression(
        CypherExpression expression,
        IReadOnlySet<string> scope,
        IReadOnlyDictionary<string, object?> parameters)
    {
        switch (expression)
        {
            case VariableRef variable:
                ValidateAlias(variable.Alias, scope);
                break;

            case PropertyAccess property:
                ValidateExpression(property.Target, scope, parameters);
                break;

            case QueryParameter parameter:
                ValidateParameter(parameter.Name, parameters);
                break;

            case FunctionCall function:
                ValidateExpressions(function.Arguments, scope, parameters);
                break;

            case BinaryExpression binary:
                ValidateExpression(binary.Left, scope, parameters);
                ValidateExpression(binary.Right, scope, parameters);
                break;

            case UnaryExpression unary:
                ValidateExpression(unary.Operand, scope, parameters);
                break;

            case LabelTest label:
                ValidateExpression(label.Target, scope, parameters);
                break;

            case Literal:
                break;

            default:
                throw new GraphException($"Unsupported Cypher expression '{expression.GetType().Name}'.");
        }
    }

    private static void ValidateAlias(string alias, IReadOnlySet<string> scope)
    {
        if (!scope.Contains(alias))
        {
            throw new GraphException($"Cypher variable '{alias}' is not bound by a preceding clause.");
        }
    }

    private static void ValidateParameter(string name, IReadOnlyDictionary<string, object?> parameters)
    {
        if (!parameters.ContainsKey(name))
        {
            throw new GraphException($"Cypher parameter '{name}' is not defined in the statement parameters.");
        }
    }
}
