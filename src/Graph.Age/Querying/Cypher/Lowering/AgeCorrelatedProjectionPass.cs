// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying.Cypher.Lowering;

using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;
using Cvoya.Graph.Cypher.Validation;

/// <summary>
/// Lowers correlated collection and pattern-count projections to AGE-compatible grouped matches.
/// </summary>
/// <remarks>
/// AGE 1.7 supports neither pattern comprehensions nor <c>CALL { }</c> subqueries. Correlated
/// collection projections already have one shared traversal pattern, so the pass matches that
/// traversal once and groups its rows with <c>collect</c>/<c>count</c>. Independent pattern counts
/// use sequential optional matches so zero-degree roots survive and multiple counts cannot multiply
/// one another through a Cartesian product.
/// </remarks>
internal sealed class AgeCorrelatedProjectionPass : ICypherPass
{
    private const string ProjectionAliasPrefix = "__age_projection";
    private const string CountAliasPrefix = "__age_count";

    /// <inheritdoc />
    public CypherStatement Run(CypherStatement input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (TryFindCorrelatedAnchor(input.Clauses, out _, out var anchor, out _) &&
            IsCorrelatedCollection(input.Clauses, anchor))
        {
            return LowerCorrelatedCollection(input);
        }

        return LowerPatternCountProjections(input);
    }

    private static CypherStatement LowerCorrelatedCollection(CypherStatement input)
    {
        if (!TryFindCorrelatedAnchor(input.Clauses, out var whereIndex, out var anchor, out var rootPredicate))
        {
            return input;
        }

        var returnIndex = FindReturnIndex(input.Clauses);
        var outerReturn = (ReturnClause)input.Clauses[returnIndex];
        var calls = input.Clauses
            .Take(returnIndex)
            .OfType<CallSubqueryClause>()
            .Select(ParseCall)
            .ToArray();
        var nestedCalls = calls.Where(call => call.Kind == CorrelatedCallKind.NestedGrouping).ToArray();
        if (nestedCalls.Length > 0)
        {
            return LowerNestedGrouping(input, whereIndex, anchor, rootPredicate, returnIndex, outerReturn, calls);
        }

        var orderedCalls = calls.Where(call => call.Kind == CorrelatedCallKind.OrderedCollection).ToArray();
        if (orderedCalls.Length > 1 && !orderedCalls.Skip(1).All(call => call.OrderBy == orderedCalls[0].OrderBy))
        {
            throw Unsupported("multiple correlated collections with different orderings");
        }

        var clauses = BuildCorrelatedPrefix(input.Clauses, whereIndex, rootPredicate, anchor);
        if (orderedCalls is [var ordered])
        {
            clauses.Add(ordered.Carry!);
            clauses.Add(ordered.OrderBy!);
        }

        var callExpressions = calls.ToDictionary(
            call => call.Alias,
            call => call.Expression,
            StringComparer.Ordinal);
        var aggregationItems = new List<ReturnItem>
        {
            new(new VariableRef(SourceAlias(anchor.Pattern)), null),
        };
        var rewrittenItems = new ReturnItem[outerReturn.Items.Count];
        for (var index = 0; index < outerReturn.Items.Count; index++)
        {
            var expression = outerReturn.Items[index].Expression;
            if (TryLowerCorrelatedExpression(expression, callExpressions, out var aggregate))
            {
                var alias = $"{ProjectionAliasPrefix}{index}";
                aggregationItems.Add(new ReturnItem(aggregate, alias));
                expression = new VariableRef(alias);
            }

            rewrittenItems[index] = new ReturnItem(expression, outerReturn.Items[index].Alias);
        }

        clauses.Add(new WithClause(aggregationItems, distinct: false));
        clauses.Add(new ReturnClause(rewrittenItems, outerReturn.Distinct));
        AppendTrailingClauses(clauses, input.Clauses, returnIndex);
        return new CypherStatement(clauses, input.Parameters, input.PathTypes);
    }

    private static CypherStatement LowerNestedGrouping(
        CypherStatement input,
        int whereIndex,
        PatternSubqueryExpression anchor,
        CypherExpression? rootPredicate,
        int returnIndex,
        ReturnClause outerReturn,
        IReadOnlyList<CorrelatedCall> calls)
    {
        if (calls is not [{ Kind: CorrelatedCallKind.NestedGrouping } nested] ||
            ContainsCorrelatedExpression(outerReturn.Items.Select(item => item.Expression), nested.Alias))
        {
            throw Unsupported("a nested correlated grouping combined with another correlated projection");
        }

        var clauses = BuildCorrelatedPrefix(input.Clauses, whereIndex, rootPredicate, anchor);
        var sourceAlias = SourceAlias(anchor.Pattern);
        clauses.Add(nested.NestedKey!);

        var groupedItems = new List<ReturnItem>
        {
            new(new VariableRef(sourceAlias), null),
        };
        groupedItems.AddRange(nested.NestedAggregates!.Items);
        clauses.Add(new WithClause(groupedItems, nested.NestedAggregates.Distinct));
        clauses.Add(new WithClause(
        [
            new ReturnItem(new VariableRef(sourceAlias), null),
            new ReturnItem(nested.Expression, nested.Alias),
        ],
        distinct: false));
        clauses.Add(outerReturn);
        AppendTrailingClauses(clauses, input.Clauses, returnIndex);
        return new CypherStatement(clauses, input.Parameters, input.PathTypes);
    }

    private static List<ICypherClause> BuildCorrelatedPrefix(
        IReadOnlyList<ICypherClause> clauses,
        int whereIndex,
        CypherExpression? rootPredicate,
        PatternSubqueryExpression anchor)
    {
        var output = new List<ICypherClause>();
        for (var index = 0; index < whereIndex; index++)
        {
            if (clauses[index] is not CallSubqueryClause)
            {
                output.Add(clauses[index]);
            }
        }

        if (rootPredicate is not null)
        {
            output.Add(new WhereClause(rootPredicate));
        }

        output.Add(new MatchClause([anchor.Pattern], optional: false));
        if (anchor.Predicate is not null)
        {
            output.Add(new WhereClause(anchor.Predicate));
        }

        return output;
    }

    private static CorrelatedCall ParseCall(CallSubqueryClause call)
    {
        if (call.Body[^1] is not ReturnClause { Items: [var result] } || result.Alias is null)
        {
            throw Unsupported("a correlated subquery without one named result");
        }

        var withClauses = call.Body.OfType<WithClause>().ToArray();
        var orderBy = call.Body.OfType<OrderByClause>().SingleOrDefault();
        return withClauses.Length switch
        {
            0 when orderBy is null => new CorrelatedCall(
                CorrelatedCallKind.Aggregate,
                result.Alias,
                result.Expression),
            1 when orderBy is not null => new CorrelatedCall(
                CorrelatedCallKind.OrderedCollection,
                result.Alias,
                result.Expression,
                Carry: withClauses[0],
                OrderBy: orderBy),
            2 when orderBy is null => new CorrelatedCall(
                CorrelatedCallKind.NestedGrouping,
                result.Alias,
                result.Expression,
                NestedKey: withClauses[0],
                NestedAggregates: withClauses[1]),
            _ => throw Unsupported("an unrecognized correlated subquery body"),
        };
    }

    private static bool TryLowerCorrelatedExpression(
        CypherExpression expression,
        Dictionary<string, CypherExpression> callExpressions,
        out CypherExpression aggregate)
    {
        switch (expression)
        {
            case PatternComprehensionExpression comprehension:
                aggregate = Function(
                    "collect",
                    Conditional(comprehension.Predicate, comprehension.Projection));
                return true;

            case PatternSubqueryExpression { Kind: PatternSubqueryKind.Count } count:
                aggregate = Function(
                    "count",
                    Conditional(count.Predicate, CountTarget(count.Pattern)));
                return true;

            case VariableRef variable when callExpressions.TryGetValue(variable.Alias, out var callExpression):
                aggregate = callExpression;
                return true;

            default:
                aggregate = null!;
                return false;
        }
    }

    private static CypherStatement LowerPatternCountProjections(CypherStatement input)
    {
        var returnIndex = FindReturnIndex(input.Clauses, required: false);
        if (returnIndex < 0 || input.Clauses[returnIndex] is not ReturnClause outerReturn)
        {
            return input;
        }

        var extractor = new CountProjectionExtractor();
        var rewrittenItems = outerReturn.Items
            .Select(item => new ReturnItem(extractor.Rewrite(item.Expression), item.Alias))
            .ToArray();
        var leadingClauses = input.Clauses
            .Take(returnIndex)
            .Select(clause => clause is OrderByClause orderBy
                ? new OrderByClause(orderBy.Items
                    .Select(item => new OrderByItem(extractor.Rewrite(item.Expression), item.Descending))
                    .ToArray())
                : clause)
            .ToArray();
        var trailingClauses = input.Clauses
            .Skip(returnIndex + 1)
            .Select(clause => clause is OrderByClause orderBy
                ? new OrderByClause(orderBy.Items
                    .Select(item => new OrderByItem(extractor.Rewrite(item.Expression), item.Descending))
                    .ToArray())
                : clause)
            .ToArray();
        if (extractor.Counts.Count == 0)
        {
            return input;
        }

        var clauses = leadingClauses.ToList();
        var scope = ScopeAt(input.Clauses, returnIndex);
        for (var index = 0; index < extractor.Counts.Count; index++)
        {
            var count = extractor.Counts[index];
            var prepared = PrepareCountPattern(count.Pattern, index);
            clauses.Add(new MatchClause([prepared.Pattern], optional: true));
            if (count.Predicate is not null)
            {
                clauses.Add(new WhereClause(count.Predicate));
            }

            var items = scope.Select(alias => new ReturnItem(new VariableRef(alias), null)).ToList();
            items.Add(new ReturnItem(Function("count", new VariableRef(prepared.RelationshipAlias)), count.Alias));
            clauses.Add(new WithClause(items, distinct: false));
            scope.Add(count.Alias);
        }

        clauses.Add(new ReturnClause(rewrittenItems, outerReturn.Distinct));
        clauses.AddRange(trailingClauses);
        return new CypherStatement(clauses, input.Parameters, input.PathTypes);
    }

    private static PreparedCountPattern PrepareCountPattern(PathPattern pattern, int countIndex)
    {
        var elements = new PatternElement[pattern.Elements.Count];
        string? relationshipAlias = null;
        var nodeIndex = 0;
        var relationshipIndex = 0;
        for (var index = 0; index < pattern.Elements.Count; index++)
        {
            elements[index] = pattern.Elements[index] switch
            {
                NodePattern node => new NodePattern(
                    node.Alias ?? $"{CountAliasPrefix}{countIndex}_node{nodeIndex++}",
                    node.Labels),
                RelationshipPattern relationship => PrepareRelationship(relationship),
                _ => throw Unsupported("an unknown pattern element"),
            };
        }

        return new PreparedCountPattern(
            new PathPattern(elements, pattern.Alias),
            relationshipAlias ?? throw Unsupported("a pattern count without a relationship"));

        RelationshipPattern PrepareRelationship(RelationshipPattern relationship)
        {
            var alias = relationship.Alias ?? $"{CountAliasPrefix}{countIndex}_relationship{relationshipIndex++}";
            relationshipAlias ??= alias;
            return new RelationshipPattern(alias, relationship.Direction, relationship.Depth, relationship.Types);
        }
    }

    private static List<string> ScopeAt(IReadOnlyList<ICypherClause> clauses, int endExclusive)
    {
        var scope = new List<string>();
        for (var index = 0; index < endExclusive; index++)
        {
            switch (clauses[index])
            {
                case MatchClause match:
                    foreach (var pattern in match.Patterns)
                    {
                        Add(pattern.Alias);
                        foreach (var element in pattern.Elements)
                        {
                            Add(element switch
                            {
                                NodePattern node => node.Alias,
                                RelationshipPattern relationship => relationship.Alias,
                                _ => null,
                            });
                        }
                    }

                    break;

                case WithClause { Wildcard: false } with:
                    scope.Clear();
                    foreach (var item in with.Items)
                    {
                        Add(item.Alias ?? (item.Expression as VariableRef)?.Alias);
                    }

                    break;

                case UnwindClause unwind:
                    Add(unwind.Alias);
                    break;

                case CallClause call:
                    foreach (var yield in call.Yields)
                    {
                        Add(yield.Alias ?? yield.Name);
                    }

                    break;

                case FullTextSearchClause search:
                    Add(search.Alias);
                    break;
            }
        }

        return scope;

        void Add(string? alias)
        {
            if (alias is not null && !scope.Contains(alias, StringComparer.Ordinal))
            {
                scope.Add(alias);
            }
        }
    }

    private static bool TryFindCorrelatedAnchor(
        IReadOnlyList<ICypherClause> clauses,
        out int whereIndex,
        out PatternSubqueryExpression anchor,
        out CypherExpression? remainder)
    {
        for (var index = 0; index < clauses.Count; index++)
        {
            if (clauses[index] is WhereClause where &&
                TryRemoveExists(where.Predicate, out anchor, out remainder))
            {
                whereIndex = index;
                return true;
            }
        }

        whereIndex = -1;
        anchor = null!;
        remainder = null;
        return false;
    }

    private static bool IsCorrelatedCollection(
        IReadOnlyList<ICypherClause> clauses,
        PatternSubqueryExpression anchor)
    {
        if (clauses.Any(clause => clause is CallSubqueryClause))
        {
            return true;
        }

        return clauses.OfType<ReturnClause>().Any(@return => @return.Items.Any(item =>
            ContainsExpression(item.Expression, expression =>
                expression is PatternComprehensionExpression ||
                expression is PatternSubqueryExpression
                {
                    Kind: PatternSubqueryKind.Count,
                    Pattern: var pattern,
                } && PatternsEquivalent(pattern, anchor.Pattern))));
    }

    private static bool ContainsExpression(
        CypherExpression expression,
        Func<CypherExpression, bool> predicate)
    {
        if (predicate(expression))
        {
            return true;
        }

        return expression switch
        {
            PropertyAccess property => ContainsExpression(property.Target, predicate),
            EscapedPropertyAccess property => ContainsExpression(property.Target, predicate),
            FunctionCall function => function.Arguments.Any(item => ContainsExpression(item, predicate)),
            BinaryExpression binary =>
                ContainsExpression(binary.Left, predicate) || ContainsExpression(binary.Right, predicate),
            UnaryExpression unary => ContainsExpression(unary.Operand, predicate),
            LabelTest label => ContainsExpression(label.Target, predicate),
            ListExpression list => list.Items.Any(item => ContainsExpression(item, predicate)),
            MapExpression map => map.Entries.Any(entry => ContainsExpression(entry.Value, predicate)),
            IndexExpression index =>
                ContainsExpression(index.Target, predicate) || ContainsExpression(index.Index, predicate),
            CaseExpression @case =>
                ContainsExpression(@case.Condition, predicate) ||
                ContainsExpression(@case.WhenTrue, predicate) ||
                (@case.WhenFalse is not null && ContainsExpression(@case.WhenFalse, predicate)),
            ConjunctionExpression conjunction =>
                conjunction.Predicates.Any(item => ContainsExpression(item, predicate)),
            PatternSubqueryExpression subquery =>
                subquery.Predicate is not null && ContainsExpression(subquery.Predicate, predicate),
            PatternComprehensionExpression comprehension =>
                ContainsExpression(comprehension.Projection, predicate) ||
                (comprehension.Predicate is not null && ContainsExpression(comprehension.Predicate, predicate)),
            ListComprehensionExpression comprehension =>
                ContainsExpression(comprehension.Source, predicate) ||
                (comprehension.Predicate is not null && ContainsExpression(comprehension.Predicate, predicate)) ||
                (comprehension.Projection is not null && ContainsExpression(comprehension.Projection, predicate)),
            ReduceExpression reduce =>
                ContainsExpression(reduce.Seed, predicate) ||
                ContainsExpression(reduce.Source, predicate) ||
                ContainsExpression(reduce.Reducer, predicate),
            AllExpression all =>
                ContainsExpression(all.Source, predicate) || ContainsExpression(all.Predicate, predicate),
            _ => false,
        };
    }

    private static bool TryRemoveExists(
        CypherExpression expression,
        out PatternSubqueryExpression anchor,
        out CypherExpression? remainder)
    {
        if (expression is PatternSubqueryExpression { Kind: PatternSubqueryKind.Exists } exists)
        {
            anchor = exists;
            remainder = null;
            return true;
        }

        if (expression is ConjunctionExpression conjunction)
        {
            for (var index = 0; index < conjunction.Predicates.Count; index++)
            {
                if (!TryRemoveExists(conjunction.Predicates[index], out anchor, out var nestedRemainder))
                {
                    continue;
                }

                var predicates = conjunction.Predicates.Where((_, itemIndex) => itemIndex != index).ToList();
                if (nestedRemainder is not null)
                {
                    predicates.Insert(index, nestedRemainder);
                }

                remainder = predicates.Count switch
                {
                    0 => null,
                    1 => predicates[0],
                    _ => new ConjunctionExpression(predicates),
                };
                return true;
            }
        }

        anchor = null!;
        remainder = null;
        return false;
    }

    private static int FindReturnIndex(IReadOnlyList<ICypherClause> clauses, bool required = true)
    {
        for (var index = clauses.Count - 1; index >= 0; index--)
        {
            if (clauses[index] is ReturnClause)
            {
                return index;
            }
        }

        return required ? throw Unsupported("a statement without RETURN") : -1;
    }

    private static string SourceAlias(PathPattern pattern) =>
        (pattern.Elements[0] as NodePattern)?.Alias ?? throw Unsupported("a correlated pattern without a source alias");

    private static VariableRef CountTarget(PathPattern pattern) =>
        pattern.Elements.OfType<RelationshipPattern>().FirstOrDefault()?.Alias is { } alias
            ? new VariableRef(alias)
            : throw Unsupported("a correlated count without a relationship alias");

    private static CypherExpression Conditional(CypherExpression? predicate, CypherExpression value) =>
        predicate is null ? value : new CaseExpression(predicate, value, new Literal(null));

    private static bool PatternsEquivalent(PathPattern left, PathPattern right)
    {
        if (!string.Equals(left.Alias, right.Alias, StringComparison.Ordinal) ||
            left.Elements.Count != right.Elements.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Elements.Count; index++)
        {
            var equivalent = (left.Elements[index], right.Elements[index]) switch
            {
                (NodePattern leftNode, NodePattern rightNode) =>
                    string.Equals(leftNode.Alias, rightNode.Alias, StringComparison.Ordinal) &&
                    leftNode.Labels.SequenceEqual(rightNode.Labels, StringComparer.Ordinal),
                (RelationshipPattern leftRelationship, RelationshipPattern rightRelationship) =>
                    string.Equals(leftRelationship.Alias, rightRelationship.Alias, StringComparison.Ordinal) &&
                    leftRelationship.Direction == rightRelationship.Direction &&
                    leftRelationship.Depth == rightRelationship.Depth &&
                    leftRelationship.Types.SequenceEqual(rightRelationship.Types, StringComparer.Ordinal),
                _ => false,
            };
            if (!equivalent)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsCorrelatedExpression(IEnumerable<CypherExpression> expressions, string allowedAlias) =>
        expressions.Any(expression => expression switch
        {
            PatternComprehensionExpression or PatternSubqueryExpression { Kind: PatternSubqueryKind.Count } => true,
            VariableRef variable => !string.Equals(variable.Alias, allowedAlias, StringComparison.Ordinal) &&
                variable.Alias.StartsWith("__group", StringComparison.Ordinal),
            _ => false,
        });

    private static void AppendTrailingClauses(
        List<ICypherClause> output,
        IReadOnlyList<ICypherClause> input,
        int returnIndex)
    {
        for (var index = returnIndex + 1; index < input.Count; index++)
        {
            output.Add(input[index]);
        }
    }

    private static FunctionCall Function(string name, params CypherExpression[] arguments) => new(name, arguments);

    private static GraphQueryTranslationException Unsupported(string shape) => new(
        $"Apache AGE cannot structurally lower {shape}.");

    private enum CorrelatedCallKind
    {
        Aggregate,
        OrderedCollection,
        NestedGrouping,
    }

    private sealed record CorrelatedCall(
        CorrelatedCallKind Kind,
        string Alias,
        CypherExpression Expression,
        WithClause? Carry = null,
        OrderByClause? OrderBy = null,
        WithClause? NestedKey = null,
        WithClause? NestedAggregates = null);

    private sealed record PreparedCountPattern(PathPattern Pattern, string RelationshipAlias);

    private sealed record CountProjection(string Alias, PathPattern Pattern, CypherExpression? Predicate);

    private sealed class CountProjectionExtractor
    {
        public List<CountProjection> Counts { get; } = [];

        public CypherExpression Rewrite(CypherExpression expression)
        {
            if (expression is PatternSubqueryExpression { Kind: PatternSubqueryKind.Count } count)
            {
                var existing = Counts.FirstOrDefault(item =>
                    item.Predicate == count.Predicate && PatternsEquivalent(item.Pattern, count.Pattern));
                if (existing is not null)
                {
                    return new VariableRef(existing.Alias);
                }

                var created = new CountProjection($"{CountAliasPrefix}{Counts.Count}", count.Pattern, count.Predicate);
                Counts.Add(created);
                return new VariableRef(created.Alias);
            }

            return expression switch
            {
                PropertyAccess property => new PropertyAccess(Rewrite(property.Target), property.Property),
                EscapedPropertyAccess property => new EscapedPropertyAccess(Rewrite(property.Target), property.Property),
                FunctionCall function => new FunctionCall(function.Name, function.Arguments.Select(Rewrite).ToArray()),
                BinaryExpression binary => new BinaryExpression(binary.Op, Rewrite(binary.Left), Rewrite(binary.Right)),
                UnaryExpression unary => new UnaryExpression(unary.Op, Rewrite(unary.Operand)),
                LabelTest label => new LabelTest(Rewrite(label.Target), label.Labels),
                ListExpression list => new ListExpression(list.Items.Select(Rewrite).ToArray()),
                MapExpression map => new MapExpression(map.Entries
                    .Select(entry => new MapEntry(entry.Key, Rewrite(entry.Value))).ToArray()),
                IndexExpression index => new IndexExpression(Rewrite(index.Target), Rewrite(index.Index)),
                CaseExpression @case => new CaseExpression(
                    Rewrite(@case.Condition),
                    Rewrite(@case.WhenTrue),
                    @case.WhenFalse is null ? null : Rewrite(@case.WhenFalse)),
                ConjunctionExpression conjunction => new ConjunctionExpression(
                    conjunction.Predicates.Select(Rewrite).ToArray()),
                ListComprehensionExpression comprehension => new ListComprehensionExpression(
                    Rewrite(comprehension.Source),
                    comprehension.IteratorAlias,
                    comprehension.Predicate is null ? null : Rewrite(comprehension.Predicate),
                    comprehension.Projection is null ? null : Rewrite(comprehension.Projection)),
                ReduceExpression reduce => new ReduceExpression(
                    reduce.AccumulatorAlias,
                    Rewrite(reduce.Seed),
                    reduce.IteratorAlias,
                    Rewrite(reduce.Source),
                    Rewrite(reduce.Reducer)),
                AllExpression all => new AllExpression(
                    all.IteratorAlias,
                    Rewrite(all.Source),
                    Rewrite(all.Predicate)),
                _ => expression,
            };
        }

    }
}
