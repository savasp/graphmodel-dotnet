// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying.Cypher.Lowering;

using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;
using Cvoya.Graph.Cypher.Validation;

/// <summary>
/// Expands inline node projections into structured complex-property hydration clauses.
/// </summary>
internal sealed class AgeInlineComplexPropertyProjectionPass : ICypherPass
{
    /// <inheritdoc />
    public CypherStatement Run(CypherStatement input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var rewriter = new ClauseRewriter(AliasCollector.Collect(input.Clauses));
        var clauses = rewriter.RewriteClauses(input.Clauses);
        return rewriter.Changed
            ? new CypherStatement(clauses, input.Parameters, input.PathTypes)
            : input;
    }

    private static ReturnItem Variable(string alias) => new(new VariableRef(alias), null);

    private static FunctionCall Function(string name, params CypherExpression[] arguments) =>
        new(name, arguments);

    private static MapExpression NodeProjection(
        string alias,
        CypherExpression complexProperties) => new(
    [
        new MapEntry("Node", new VariableRef(alias)),
        new MapEntry("ComplexProperties", complexProperties),
    ]);

    private static IEnumerable<string> LegacyCarryCandidates()
    {
        yield return "pathIndex";
        yield return "hopIndex";
        yield return "src";
        yield return "r";
        yield return "tgt";
        for (var suffix = 2; suffix <= GraphDataModel.DefaultDepthAllowed; suffix++)
        {
            yield return $"r_{suffix}";
            yield return $"tgt_{suffix}";
        }
    }

    private sealed class ClauseRewriter(HashSet<string> statementAliases)
    {
        public bool Changed { get; private set; }

        public ICypherClause[] RewriteClauses(IReadOnlyList<ICypherClause> clauses)
        {
            var rewritten = new List<ICypherClause>();
            foreach (var clause in clauses)
            {
                switch (clause)
                {
                    case WithClause { Wildcard: false } with:
                        RewriteProjectionClause(
                            rewritten,
                            with.Items,
                            items => new WithClause(items, with.Distinct));
                        break;

                    case ReturnClause @return:
                        RewriteProjectionClause(
                            rewritten,
                            @return.Items,
                            items => new ReturnClause(items, @return.Distinct));
                        break;

                    case CallSubqueryClause subquery:
                        rewritten.Add(RewriteSubquery(subquery));
                        break;

                    default:
                        rewritten.Add(clause);
                        break;
                }
            }

            return rewritten.ToArray();
        }

        private void RewriteProjectionClause(
            List<ICypherClause> clauses,
            IReadOnlyList<ReturnItem> items,
            Func<IReadOnlyList<ReturnItem>, ICypherClause> createClause)
        {
            var freeAliases = FreeAliasCollector.Collect(items.Select(item => item.Expression));
            var expressionRewriter = new ProjectionExpressionRewriter();
            var rewrittenItems = items
                .Select(item => new ReturnItem(
                    expressionRewriter.Rewrite(item.Expression),
                    item.Alias))
                .ToArray();
            if (!expressionRewriter.Changed)
            {
                clauses.Add(createClause(items));
                return;
            }

            Changed = true;
            if (expressionRewriter.HydrationAliases.Count > 0)
            {
                AppendHydrationClauses(
                    clauses,
                    expressionRewriter.HydrationAliases,
                    BuildCarryAliases(freeAliases));
            }

            clauses.Add(createClause(rewrittenItems));
        }

        private CallSubqueryClause RewriteSubquery(CallSubqueryClause subquery)
        {
            var nested = new ClauseRewriter(AliasCollector.Collect(subquery.Body, subquery.ImportedVariables));
            var body = nested.RewriteClauses(subquery.Body);
            if (!nested.Changed)
            {
                return subquery;
            }

            Changed = true;
            return new CallSubqueryClause(subquery.ImportedVariables, body);
        }

        private List<string> BuildCarryAliases(IReadOnlyList<string> freeAliases)
        {
            var carry = new List<string>();
            foreach (var candidate in LegacyCarryCandidates())
            {
                if (statementAliases.Contains(candidate))
                {
                    carry.Add(candidate);
                }
            }

            foreach (var alias in freeAliases)
            {
                if (!carry.Contains(alias, StringComparer.Ordinal))
                {
                    carry.Add(alias);
                }
            }

            return carry;
        }

        private static void AppendHydrationClauses(
            List<ICypherClause> clauses,
            IReadOnlyList<string> hydrationAliases,
            IReadOnlyList<string> baseCarry)
        {
            var carry = baseCarry.ToList();
            foreach (var alias in hydrationAliases)
            {
                var relationshipsAlias = $"{alias}_inline_relationships";
                var propertyAlias = $"{alias}_inline_property";
                var pathAlias = $"{alias}_inline_path";
                var propertiesAlias = $"{alias}_inline_properties";

                clauses.Add(new MatchClause(
                [
                    new PathPattern(
                    [
                        new NodePattern(alias, []),
                        new RelationshipPattern(
                            relationshipsAlias,
                            CypherDirection.Outgoing,
                            new DepthRange(1, GraphDataModel.DefaultDepthAllowed),
                            types: []),
                        new NodePattern(propertyAlias, []),
                    ]),
                ], optional: true));
                clauses.Add(new WhereClause(new AllExpression(
                    "propertyRelationship",
                    new VariableRef(relationshipsAlias),
                    new BinaryExpression(
                        CypherBinaryOperator.Equal,
                        new PropertyAccess(
                            new VariableRef("propertyRelationship"),
                            AgeDialect.Instance.ComplexPropertyRelationshipMarker),
                        new Literal(true)))));
                clauses.Add(new WithClause(
                    carry.Select(Variable)
                        .Append(new ReturnItem(PropertyPath(relationshipsAlias), pathAlias))
                        .ToArray(),
                    distinct: false));
                clauses.Add(new WithClause(
                    carry.Select(Variable)
                        .Append(new ReturnItem(
                            Function("collect", new VariableRef(pathAlias)),
                            propertiesAlias))
                        .ToArray(),
                    distinct: false));
                carry.Add(propertiesAlias);
            }
        }

        private static CaseExpression PropertyPath(string relationshipsAlias)
        {
            var index = new VariableRef("i");
            var relationship = new IndexExpression(new VariableRef(relationshipsAlias), index);
            return new CaseExpression(
                new UnaryExpression(
                    CypherUnaryOperator.IsNull,
                    new VariableRef(relationshipsAlias)),
                new ListExpression([]),
                new ListComprehensionExpression(
                    Function(
                        "range",
                        new Literal(0),
                        new BinaryExpression(
                            CypherBinaryOperator.Subtract,
                            Function("size", new VariableRef(relationshipsAlias)),
                            new Literal(1))),
                    "i",
                    projection: new MapExpression(
                    [
                        new MapEntry("ParentNode", Function("startNode", relationship)),
                        new MapEntry("Relationship", relationship),
                        new MapEntry(
                            "SequenceNumber",
                            new PropertyAccess(relationship, "SequenceNumber")),
                        new MapEntry("Property", Function("endNode", relationship)),
                    ])));
        }
    }

    private sealed class ProjectionExpressionRewriter
    {
        private readonly List<string> hydrationAliases = [];

        public bool Changed { get; private set; }

        public List<string> HydrationAliases => hydrationAliases;

        public CypherExpression Rewrite(CypherExpression expression)
        {
            if (expression is EntityProjectionExpression entity)
            {
                Changed = true;
                if (entity.LoadComplexProperties &&
                    !hydrationAliases.Contains(entity.Alias, StringComparer.Ordinal))
                {
                    hydrationAliases.Add(entity.Alias);
                }

                return NodeProjection(
                    entity.Alias,
                    entity.LoadComplexProperties
                        ? new VariableRef($"{entity.Alias}_inline_properties")
                        : new ListExpression([]));
            }

            return expression switch
            {
                BinaryExpression binary => new BinaryExpression(
                    binary.Op,
                    Rewrite(binary.Left),
                    Rewrite(binary.Right)),
                UnaryExpression unary => new UnaryExpression(unary.Op, Rewrite(unary.Operand)),
                PropertyAccess property => new PropertyAccess(Rewrite(property.Target), property.Property),
                EscapedPropertyAccess property => new EscapedPropertyAccess(
                    Rewrite(property.Target),
                    property.Property),
                FunctionCall function => new FunctionCall(
                    function.Name,
                    function.Arguments.Select(Rewrite).ToArray()),
                LabelTest label => new LabelTest(Rewrite(label.Target), label.Labels),
                ListExpression list => new ListExpression(list.Items.Select(Rewrite).ToArray()),
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
                MapExpression map => new MapExpression(map.Entries
                    .Select(entry => new MapEntry(entry.Key, Rewrite(entry.Value)))
                    .ToArray()),
                IndexExpression index => new IndexExpression(
                    Rewrite(index.Target),
                    Rewrite(index.Index)),
                CaseExpression @case => new CaseExpression(
                    Rewrite(@case.Condition),
                    Rewrite(@case.WhenTrue),
                    @case.WhenFalse is null ? null : Rewrite(@case.WhenFalse)),
                ConjunctionExpression conjunction => new ConjunctionExpression(
                    conjunction.Predicates.Select(Rewrite).ToArray()),
                PatternSubqueryExpression subquery => new PatternSubqueryExpression(
                    subquery.Kind,
                    subquery.Pattern,
                    subquery.Predicate is null ? null : Rewrite(subquery.Predicate)),
                PatternComprehensionExpression comprehension => new PatternComprehensionExpression(
                    comprehension.Pattern,
                    Rewrite(comprehension.Projection),
                    comprehension.Predicate is null ? null : Rewrite(comprehension.Predicate)),
                _ => expression,
            };
        }
    }

    private static class AliasCollector
    {
        public static HashSet<string> Collect(
            IReadOnlyList<ICypherClause> clauses,
            IReadOnlyList<string>? initialAliases = null)
        {
            var aliases = initialAliases is null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(initialAliases, StringComparer.Ordinal);
            foreach (var clause in clauses)
            {
                switch (clause)
                {
                    case MatchClause match:
                        foreach (var pattern in match.Patterns)
                        {
                            Add(pattern.Alias, aliases);
                            foreach (var element in pattern.Elements)
                            {
                                Add(element switch
                                {
                                    NodePattern node => node.Alias,
                                    RelationshipPattern relationship => relationship.Alias,
                                    _ => null,
                                }, aliases);
                            }
                        }

                        break;

                    case WhereClause where:
                        AddFreeAliases(where.Predicate, aliases);
                        break;

                    case WithClause { Wildcard: false } with:
                        AddItems(with.Items, aliases);
                        break;

                    case ReturnClause @return:
                        AddItems(@return.Items, aliases);
                        break;

                    case UnwindClause unwind:
                        AddFreeAliases(unwind.Source, aliases);
                        Add(unwind.Alias, aliases);
                        break;

                    case CallClause call:
                        foreach (var argument in call.Arguments)
                        {
                            AddFreeAliases(argument, aliases);
                        }

                        foreach (var yield in call.Yields)
                        {
                            Add(yield.Alias ?? yield.Name, aliases);
                        }

                        break;

                    case OrderByClause orderBy:
                        foreach (var item in orderBy.Items)
                        {
                            AddFreeAliases(item.Expression, aliases);
                        }

                        break;

                    case SkipClause skip:
                        AddFreeAliases(skip.Count, aliases);
                        break;

                    case LimitClause limit:
                        AddFreeAliases(limit.Count, aliases);
                        break;
                }
            }

            return aliases;
        }

        private static void AddItems(IReadOnlyList<ReturnItem> items, HashSet<string> aliases)
        {
            foreach (var item in items)
            {
                AddFreeAliases(item.Expression, aliases);
                Add(item.Alias, aliases);
            }
        }

        private static void AddFreeAliases(CypherExpression expression, HashSet<string> aliases)
        {
            foreach (var alias in FreeAliasCollector.Collect([expression]))
            {
                aliases.Add(alias);
            }
        }

        private static void Add(string? alias, HashSet<string> aliases)
        {
            if (alias is not null)
            {
                aliases.Add(alias);
            }
        }
    }

    private sealed class FreeAliasCollector
    {
        private readonly List<string> aliases = [];
        private readonly HashSet<string> bound = new(StringComparer.Ordinal);

        public static List<string> Collect(IEnumerable<CypherExpression> expressions)
        {
            var collector = new FreeAliasCollector();
            foreach (var expression in expressions)
            {
                collector.Visit(expression);
            }

            return collector.aliases;
        }

        private void Visit(CypherExpression expression)
        {
            switch (expression)
            {
                case VariableRef variable:
                    Add(variable.Alias);
                    break;

                case EntityProjectionExpression entity:
                    Add(entity.Alias);
                    break;

                case BinaryExpression binary:
                    Visit(binary.Left);
                    Visit(binary.Right);
                    break;

                case UnaryExpression unary:
                    Visit(unary.Operand);
                    break;

                case PropertyAccess property:
                    Visit(property.Target);
                    break;

                case EscapedPropertyAccess property:
                    Visit(property.Target);
                    break;

                case FunctionCall function:
                    VisitAll(function.Arguments);
                    break;

                case LabelTest label:
                    Visit(label.Target);
                    break;

                case ListExpression list:
                    VisitAll(list.Items);
                    break;

                case ListComprehensionExpression comprehension:
                    Visit(comprehension.Source);
                    VisitBound(
                        [comprehension.IteratorAlias],
                        comprehension.Predicate,
                        comprehension.Projection);
                    break;

                case ReduceExpression reduce:
                    Visit(reduce.Seed);
                    Visit(reduce.Source);
                    VisitBound(
                        [reduce.AccumulatorAlias, reduce.IteratorAlias],
                        reduce.Reducer);
                    break;

                case AllExpression all:
                    Visit(all.Source);
                    VisitBound([all.IteratorAlias], all.Predicate);
                    break;

                case MapExpression map:
                    VisitAll(map.Entries.Select(entry => entry.Value));
                    break;

                case IndexExpression index:
                    Visit(index.Target);
                    Visit(index.Index);
                    break;

                case CaseExpression @case:
                    Visit(@case.Condition);
                    Visit(@case.WhenTrue);
                    if (@case.WhenFalse is not null)
                    {
                        Visit(@case.WhenFalse);
                    }

                    break;

                case ConjunctionExpression conjunction:
                    VisitAll(conjunction.Predicates);
                    break;

                case PatternSubqueryExpression subquery:
                    if (subquery.Predicate is not null)
                    {
                        VisitPatternBound(subquery.Pattern, subquery.Predicate);
                    }

                    break;

                case PatternComprehensionExpression comprehension:
                    VisitPatternBound(
                        comprehension.Pattern,
                        comprehension.Predicate,
                        comprehension.Projection);
                    break;
            }
        }

        private void VisitAll(IEnumerable<CypherExpression> expressions)
        {
            foreach (var expression in expressions)
            {
                Visit(expression);
            }
        }

        private void VisitPatternBound(PathPattern pattern, params CypherExpression?[] expressions)
        {
            var aliases = pattern.Elements.Select(element => element switch
            {
                NodePattern node => node.Alias,
                RelationshipPattern relationship => relationship.Alias,
                _ => null,
            }).Append(pattern.Alias).OfType<string>().ToArray();
            VisitBound(aliases, expressions);
        }

        private void VisitBound(IReadOnlyList<string> aliasesToBind, params CypherExpression?[] expressions)
        {
            var added = aliasesToBind.Where(bound.Add).ToArray();
            foreach (var expression in expressions)
            {
                if (expression is not null)
                {
                    Visit(expression);
                }
            }

            foreach (var alias in added)
            {
                bound.Remove(alias);
            }
        }

        private void Add(string alias)
        {
            if (!bound.Contains(alias) && !aliases.Contains(alias, StringComparer.Ordinal))
            {
                aliases.Add(alias);
            }
        }
    }
}
