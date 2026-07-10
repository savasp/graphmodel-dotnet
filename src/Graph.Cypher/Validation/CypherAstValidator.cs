// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.
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

using Cvoya.Graph;
using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;

namespace Cvoya.Graph.Cypher.Validation;

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
                    if (!with.Wildcard)
                    {
                        scope = ProjectWithScope(with);
                    }

                    break;

                case UnwindClause unwind:
                    ValidateExpression(unwind.Source, scope, input.Parameters);
                    scope.Add(unwind.Alias);
                    break;

                case CallClause call:
                    ValidateExpressions(call.Arguments, scope, input.Parameters);
                    foreach (var yield in call.Yields)
                    {
                        scope.Add(yield.Alias ?? yield.Name);
                    }

                    break;

                case EntityProjectionClause projection:
                    ValidateAlias(projection.SourceAlias, scope);
                    if (projection.RelationshipAlias is { } relationshipAlias)
                    {
                        ValidateAlias(relationshipAlias, scope);
                    }

                    if (projection.TargetAlias is { } targetAlias)
                    {
                        ValidateAlias(targetAlias, scope);
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
            if (pattern.Alias is not null)
            {
                scope.Add(pattern.Alias);
            }

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

            case EscapedPropertyAccess property:
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

            case ListExpression list:
                ValidateExpressions(list.Items, scope, parameters);
                break;

            case MapExpression map:
                ValidateExpressions(map.Entries.Select(entry => entry.Value).ToArray(), scope, parameters);
                break;

            case EntityProjectionExpression entity:
                ValidateAlias(entity.Alias, scope);
                break;

            case IndexExpression index:
                ValidateExpression(index.Target, scope, parameters);
                ValidateExpression(index.Index, scope, parameters);
                break;

            case CaseExpression @case:
                ValidateExpression(@case.Condition, scope, parameters);
                ValidateExpression(@case.WhenTrue, scope, parameters);
                if (@case.WhenFalse is not null)
                {
                    ValidateExpression(@case.WhenFalse, scope, parameters);
                }
                break;

            case ConjunctionExpression conjunction:
                ValidateExpressions(conjunction.Predicates, scope, parameters);
                break;

            case PatternSubqueryExpression subquery:
                {
                    var subqueryScope = new HashSet<string>(scope, StringComparer.Ordinal);
                    BindPatternAliases(subqueryScope, subquery.Pattern);
                    if (subquery.Predicate is not null)
                    {
                        ValidateExpression(subquery.Predicate, subqueryScope, parameters);
                    }

                    break;
                }

            case PatternComprehensionExpression comprehension:
                {
                    var comprehensionScope = new HashSet<string>(scope, StringComparer.Ordinal);
                    BindPatternAliases(comprehensionScope, comprehension.Pattern);
                    if (comprehension.Predicate is not null)
                    {
                        ValidateExpression(comprehension.Predicate, comprehensionScope, parameters);
                    }

                    ValidateExpression(comprehension.Projection, comprehensionScope, parameters);
                    break;
                }

            case Literal:
                break;

            default:
                throw new GraphException($"Unsupported Cypher expression '{expression.GetType().Name}'.");
        }
    }

    private static void BindPatternAliases(HashSet<string> scope, PathPattern pattern)
    {
        if (pattern.Alias is not null)
        {
            scope.Add(pattern.Alias);
        }

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
