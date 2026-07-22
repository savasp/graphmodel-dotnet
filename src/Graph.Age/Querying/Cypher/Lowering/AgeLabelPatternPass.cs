// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying.Cypher.Lowering;

using Cvoya.Graph.Age.Entities;
using Cvoya.Graph.Age.Serialization;
using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;
using Cvoya.Graph.Cypher.Validation;

/// <summary>
/// Lowers logical node labels and relationship types to AGE native and hierarchy predicates.
/// </summary>
internal sealed class AgeLabelPatternPass : ICypherPass
{
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
                            predicates.Add(RewriteLogicalRelationshipType(where.Predicate));
                            index++;
                        }

                        output.Add(new WhereClause(Conjoin(predicates)));
                        break;
                    }

                case WhereClause where:
                    output.Add(new WhereClause(RewriteLogicalRelationshipType(where.Predicate)));
                    changed = true;
                    break;

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

    private static CypherExpression RewriteLogicalRelationshipType(CypherExpression expression) =>
        expression switch
        {
            BinaryExpression binary => new BinaryExpression(
                binary.Op,
                RewriteLogicalRelationshipType(binary.Left),
                RewriteLogicalRelationshipType(binary.Right)),
            UnaryExpression unary => new UnaryExpression(
                unary.Op,
                RewriteLogicalRelationshipType(unary.Operand)),
            PropertyAccess property => new PropertyAccess(
                RewriteLogicalRelationshipType(property.Target),
                property.Property),
            EscapedPropertyAccess property => new EscapedPropertyAccess(
                RewriteLogicalRelationshipType(property.Target),
                property.Property),
            PhysicalPropertyAccess property => new PhysicalPropertyAccess(
                RewriteLogicalRelationshipType(property.Target),
                property.Property),
            CollectionPropertyAccess property => new CollectionPropertyAccess(
                RewriteLogicalRelationshipType(property.Target),
                property.Property,
                property.Escape),
            CollectionContainsExpression contains => new CollectionContainsExpression(
                RewriteLogicalRelationshipType(contains.Collection),
                RewriteLogicalRelationshipType(contains.Item)),
            FunctionCall { Name: "type", Arguments: [var target] } => LogicalRelationshipType(target),
            FunctionCall function => new FunctionCall(
                function.Name,
                function.Arguments.Select(RewriteLogicalRelationshipType).ToArray()),
            LabelTest label => new LabelTest(
                RewriteLogicalRelationshipType(label.Target),
                label.Labels),
            ListExpression list => new ListExpression(
                list.Items.Select(RewriteLogicalRelationshipType).ToArray()),
            ListComprehensionExpression comprehension => new ListComprehensionExpression(
                RewriteLogicalRelationshipType(comprehension.Source),
                comprehension.IteratorAlias,
                comprehension.Predicate is null
                    ? null
                    : RewriteLogicalRelationshipType(comprehension.Predicate),
                comprehension.Projection is null
                    ? null
                    : RewriteLogicalRelationshipType(comprehension.Projection)),
            ReduceExpression reduce => new ReduceExpression(
                reduce.AccumulatorAlias,
                RewriteLogicalRelationshipType(reduce.Seed),
                reduce.IteratorAlias,
                RewriteLogicalRelationshipType(reduce.Source),
                RewriteLogicalRelationshipType(reduce.Reducer)),
            AllExpression all => new AllExpression(
                all.IteratorAlias,
                RewriteLogicalRelationshipType(all.Source),
                RewriteLogicalRelationshipType(all.Predicate)),
            MapExpression map => new MapExpression(map.Entries
                .Select(entry => new MapEntry(entry.Key, RewriteLogicalRelationshipType(entry.Value)))
                .ToArray()),
            IndexExpression index => new IndexExpression(
                RewriteLogicalRelationshipType(index.Target),
                RewriteLogicalRelationshipType(index.Index)),
            CaseExpression @case => new CaseExpression(
                RewriteLogicalRelationshipType(@case.Condition),
                RewriteLogicalRelationshipType(@case.WhenTrue),
                @case.WhenFalse is null
                    ? null
                    : RewriteLogicalRelationshipType(@case.WhenFalse)),
            ConjunctionExpression conjunction => new ConjunctionExpression(
                conjunction.Predicates.Select(RewriteLogicalRelationshipType).ToArray()),
            PatternSubqueryExpression subquery => new PatternSubqueryExpression(
                subquery.Kind,
                subquery.Pattern,
                subquery.Predicate is null
                    ? null
                    : RewriteLogicalRelationshipType(subquery.Predicate)),
            PatternComprehensionExpression comprehension => new PatternComprehensionExpression(
                comprehension.Pattern,
                RewriteLogicalRelationshipType(comprehension.Projection),
                comprehension.Predicate is null
                    ? null
                    : RewriteLogicalRelationshipType(comprehension.Predicate)),
            _ => expression,
        };

    private static IndexExpression LogicalRelationshipType(CypherExpression target)
    {
        var rewrittenTarget = RewriteLogicalRelationshipType(target);
        return new IndexExpression(
            new FunctionCall(
                "coalesce",
                [
                    new PropertyAccess(rewrittenTarget, AgeElementMatcher.InheritanceLabelsProperty),
                    new ListExpression([new FunctionCall("type", [rewrittenTarget])]),
                ]),
            ToInteger(new Literal(0)));
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
            // Provider-owned value nodes are connected only through relationships carrying the
            // complex-property marker. A relationship pattern already excludes those edges below,
            // which also excludes the value nodes at either end. Keep the more expensive incoming
            // ownership check for node-only root scans; adding it to optional/lowered traversals
            // would turn their zero-row semantics into another correlated subquery problem.
            var requiresRootIsolation = !match.Optional &&
                !pattern.Elements.OfType<RelationshipPattern>().Any();
            var elements = new PatternElement[pattern.Elements.Count];
            var patternChanged = false;

            for (var elementIndex = 0; elementIndex < pattern.Elements.Count; elementIndex++)
            {
                switch (pattern.Elements[elementIndex])
                {
                    case NodePattern node:
                        {
                            var alias = node.Alias ?? aliasGenerator.NextNodeAlias();
                            if (node.Labels.Count > 0)
                            {
                                nodePredicates.Add(AgeElementMatcher.NodePredicate(
                                    new VariableRef(alias),
                                    node.Labels));
                            }

                            if (requiresRootIsolation)
                            {
                                nodePredicates.Add(UserRootPredicate(alias, aliasGenerator));
                            }
                            elements[elementIndex] = new NodePattern(alias, []);
                            patternChanged = true;
                            break;
                        }

                    case RelationshipPattern relationship:
                        {
                            var alias = relationship.Alias ?? aliasGenerator.NextRelationshipAlias();
                            relationshipPredicates.Add(RelationshipPredicate(
                                alias,
                                relationship,
                                match.Optional));
                            elements[elementIndex] = new RelationshipPattern(
                                alias,
                                relationship.Direction,
                                relationship.Depth,
                                [],
                                relationship.IsComplexProperty);
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

        // Keep node predicates before relationship predicates so rendering remains stable.
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
            return ConjoinRelationshipPredicates(
                relationship.Types.Count == 0
                    ? null
                    : AgeElementMatcher.RelationshipPredicate(relationships, relationship.Types),
                StorageRelationshipPredicate(relationships, relationship.IsComplexProperty));
        }

        if (optional)
        {
            var optionalRelationship = new IndexExpression(
                relationships,
                ToInteger(new Literal(0)));
            return ConjoinRelationshipPredicates(
                relationship.Types.Count == 0
                    ? null
                    : AgeElementMatcher.RelationshipPredicate(optionalRelationship, relationship.Types),
                StorageRelationshipPredicate(optionalRelationship, relationship.IsComplexProperty));
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
            predicate: ConjoinRelationshipPredicates(
                relationship.Types.Count == 0
                    ? null
                    : AgeElementMatcher.RelationshipPredicate(relationshipAtIndex, relationship.Types),
                StorageRelationshipPredicate(relationshipAtIndex, relationship.IsComplexProperty)));
        return new BinaryExpression(
            CypherBinaryOperator.Equal,
            Function("size", matchingIndexes),
            Function("size", relationships));
    }

    private static BinaryExpression UserRootPredicate(string alias, AliasGenerator aliasGenerator)
    {
        var (ownerAlias, relationshipAlias) = aliasGenerator.NextOwnerAliases();
        var isOwnedComplexValue = new PatternSubqueryExpression(
            PatternSubqueryKind.Exists,
            new PathPattern(
            [
                new NodePattern(ownerAlias, []),
                new RelationshipPattern(
                    relationshipAlias,
                    CypherDirection.Outgoing,
                    depth: null,
                    types: []),
                new NodePattern(alias, []),
            ]),
            new BinaryExpression(
                CypherBinaryOperator.Equal,
                new FunctionCall(
                    "coalesce",
                    [
                        new PropertyAccess(
                            new VariableRef(relationshipAlias),
                            ComplexPropertyStorage.RelationshipMarkerProperty),
                        new Literal(false),
                    ]),
                new Literal(true)));
        var usesComplexStorageLabel = new BinaryExpression(
            CypherBinaryOperator.In,
            new Literal(SerializationBridge.ComplexNodeLabel),
            new FunctionCall("labels", [new VariableRef(alias)]));
        return new BinaryExpression(
            CypherBinaryOperator.And,
            new UnaryExpression(CypherUnaryOperator.Not, isOwnedComplexValue),
            new UnaryExpression(CypherUnaryOperator.Not, usesComplexStorageLabel));
    }

    private static BinaryExpression StorageRelationshipPredicate(
        CypherExpression relationship,
        bool isComplexProperty)
    {
        var hasExpectedMarker = new BinaryExpression(
            CypherBinaryOperator.Equal,
            new FunctionCall(
                "coalesce",
                [
                    new PropertyAccess(
                        relationship,
                        ComplexPropertyStorage.RelationshipMarkerProperty),
                    new Literal(false),
                ]),
            new Literal(isComplexProperty));
        if (isComplexProperty)
        {
            return hasExpectedMarker;
        }

        var avoidsReservedStorageType = new BinaryExpression(
            CypherBinaryOperator.NotEqual,
            new FunctionCall("type", [relationship]),
            new Literal(SerializationBridge.ComplexRelationshipType));
        return new BinaryExpression(
            CypherBinaryOperator.And,
            hasExpectedMarker,
            avoidsReservedStorageType);
    }

    private static CypherExpression ConjoinRelationshipPredicates(
        CypherExpression? logicalType,
        CypherExpression userRelationship) => logicalType is null
            ? userRelationship
            : new BinaryExpression(CypherBinaryOperator.And, logicalType, userRelationship);

    private static CypherExpression Conjoin(List<CypherExpression> predicates) =>
        predicates.Count == 1 ? predicates[0] : new ConjunctionExpression(predicates);

    private static FunctionCall ToInteger(CypherExpression expression) => Function("toInteger", expression);

    private static FunctionCall Function(string name, params CypherExpression[] arguments) => new(name, arguments);

    private sealed class AliasGenerator
    {
        private int nodeIndex;
        private int relationshipIndex;
        private int ownerIndex;

        public string NextNodeAlias() => $"age_node_{nodeIndex++}";

        public string NextRelationshipAlias() => $"age_relationship_{relationshipIndex++}";

        public (string Owner, string Relationship) NextOwnerAliases() =>
            ($"age_owner_{ownerIndex}", $"age_owner_relationship_{ownerIndex++}");
    }
}
