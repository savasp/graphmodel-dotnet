// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying.Cypher.Lowering;

using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;
using Cvoya.Graph.Cypher.Validation;

/// <summary>
/// Lowers logical node labels and relationship types to AGE inheritance-label predicates.
/// </summary>
internal sealed class AgeLabelPatternPass : ICypherPass
{
    private const string InheritanceLabels = "inheritance_labels";
    private const string HopAlias = "age_hop";

    /// <inheritdoc />
    public CypherStatement Run(CypherStatement input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var aliasGenerator = new AliasGenerator();
        var clauses = RewriteClauses(input.Clauses, aliasGenerator, out var changed);
        return changed
            ? new CypherStatement(clauses, input.Parameters, input.PathTypes)
            : input;
    }

    private static ICypherClause[] RewriteClauses(
        IReadOnlyList<ICypherClause> clauses,
        AliasGenerator aliasGenerator,
        out bool changed)
    {
        var output = new List<ICypherClause>(clauses.Count);
        changed = false;

        for (var index = 0; index < clauses.Count; index++)
        {
            switch (clauses[index])
            {
                case MatchClause match:
                    {
                        var rewritten = RewriteMatch(match, aliasGenerator, out var predicates);
                        output.Add(rewritten);
                        if (predicates.Count == 0)
                        {
                            continue;
                        }

                        changed = true;
                        if (index + 1 < clauses.Count && clauses[index + 1] is WhereClause where)
                        {
                            predicates.Add(where.Predicate);
                            index++;
                        }

                        output.Add(new WhereClause(Conjoin(predicates)));
                        break;
                    }

                case CallSubqueryClause subquery:
                    {
                        var body = RewriteClauses(subquery.Body, aliasGenerator, out var bodyChanged);
                        output.Add(bodyChanged
                            ? new CallSubqueryClause(subquery.ImportedVariables, body)
                            : subquery);
                        changed |= bodyChanged;
                        break;
                    }

                default:
                    output.Add(clauses[index]);
                    break;
            }
        }

        return output.ToArray();
    }

    private static MatchClause RewriteMatch(
        MatchClause match,
        AliasGenerator aliasGenerator,
        out List<CypherExpression> predicates)
    {
        var nodePredicates = new List<CypherExpression>();
        var relationshipPredicates = new List<CypherExpression>();
        var patterns = new PathPattern[match.Patterns.Count];
        var changed = false;

        for (var patternIndex = 0; patternIndex < match.Patterns.Count; patternIndex++)
        {
            var pattern = match.Patterns[patternIndex];
            var elements = new PatternElement[pattern.Elements.Count];
            var patternChanged = false;

            for (var elementIndex = 0; elementIndex < pattern.Elements.Count; elementIndex++)
            {
                switch (pattern.Elements[elementIndex])
                {
                    case NodePattern { Labels.Count: > 0 } node:
                        {
                            var alias = node.Alias ?? aliasGenerator.NextNodeAlias();
                            nodePredicates.Add(LabelPredicate(new VariableRef(alias), node.Labels));
                            elements[elementIndex] = new NodePattern(alias, []);
                            patternChanged = true;
                            break;
                        }

                    case RelationshipPattern { Types.Count: > 0 } relationship:
                        {
                            var alias = relationship.Alias ?? aliasGenerator.NextRelationshipAlias();
                            relationshipPredicates.Add(RelationshipPredicate(alias, relationship, match.Optional));
                            elements[elementIndex] = new RelationshipPattern(
                                alias,
                                relationship.Direction,
                                relationship.Depth,
                                []);
                            patternChanged = true;
                            break;
                        }

                    default:
                        elements[elementIndex] = pattern.Elements[elementIndex];
                        break;
                }
            }

            patterns[patternIndex] = patternChanged
                ? new PathPattern(elements, pattern.Alias)
                : pattern;
            changed |= patternChanged;
        }

        // The legacy rendered-text rewrite processed all node patterns before relationships. Keep
        // that stable ordering so the structured renderer remains byte-identical where its AST can
        // express the same redundant parentheses.
        predicates = [.. nodePredicates, .. relationshipPredicates];
        return changed ? new MatchClause(patterns, match.Optional) : match;
    }

    private static CypherExpression RelationshipPredicate(
        string alias,
        RelationshipPattern relationship,
        bool optional)
    {
        var relationships = new VariableRef(alias);
        if (relationship.Depth is null)
        {
            return LabelPredicate(relationships, relationship.Types);
        }

        if (optional)
        {
            return LabelPredicate(
                new IndexExpression(relationships, ToInteger(new Literal(0))),
                relationship.Types);
        }

        var indexes = Function(
            "range",
            new Literal(0),
            new BinaryExpression(
                CypherBinaryOperator.Subtract,
                Function("size", relationships),
                new Literal(1)));
        var relationshipAtIndex = new IndexExpression(
            relationships,
            ToInteger(new VariableRef(HopAlias)));
        var matchingIndexes = new ListComprehensionExpression(
            indexes,
            HopAlias,
            predicate: LabelPredicate(relationshipAtIndex, relationship.Types));
        return new BinaryExpression(
            CypherBinaryOperator.Equal,
            Function("size", matchingIndexes),
            Function("size", relationships));
    }

    private static CypherExpression LabelPredicate(CypherExpression target, IReadOnlyList<string> labels)
    {
        var conditions = labels.Select(label => (CypherExpression)new BinaryExpression(
            CypherBinaryOperator.In,
            new Literal(label),
            Function(
                "coalesce",
                new PropertyAccess(target, InheritanceLabels),
                new ListExpression([])))).ToArray();
        return conditions.Aggregate((left, right) =>
            new BinaryExpression(CypherBinaryOperator.Or, left, right));
    }

    private static CypherExpression Conjoin(List<CypherExpression> predicates) =>
        predicates.Count == 1 ? predicates[0] : new ConjunctionExpression(predicates);

    private static FunctionCall ToInteger(CypherExpression expression) => Function("toInteger", expression);

    private static FunctionCall Function(string name, params CypherExpression[] arguments) => new(name, arguments);

    private sealed class AliasGenerator
    {
        private int nodeIndex;
        private int relationshipIndex;

        public string NextNodeAlias() => $"age_node_{nodeIndex++}";

        public string NextRelationshipAlias() => $"age_relationship_{relationshipIndex++}";
    }
}
