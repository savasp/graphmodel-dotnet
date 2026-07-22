// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Age.Querying.Cypher.Lowering;

using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;
using Cvoya.Graph.Cypher.Validation;

/// <summary>Lowers universal relationship-list predicates to AGE-compatible list filtering.</summary>
internal sealed class AgeRelationshipPredicatePass : ICypherPass
{
    public CypherStatement Run(CypherStatement input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var rewriter = new Rewriter();
        var clauses = rewriter.RewriteClauses(input.Clauses);
        return rewriter.Changed
            ? new CypherStatement(clauses, input.Parameters, input.PathTypes)
            : input;
    }

    private sealed class Rewriter
    {
        private int iteratorIndex;

        public bool Changed { get; private set; }

        public ICypherClause[] RewriteClauses(IReadOnlyList<ICypherClause> clauses) =>
            clauses.Select(RewriteClause).ToArray();

        private ICypherClause RewriteClause(ICypherClause clause) => clause switch
        {
            WhereClause where => new WhereClause(Rewrite(where.Predicate)),
            WithClause { Wildcard: true } => WithClause.All,
            WithClause with => new WithClause(RewriteItems(with.Items), with.Distinct),
            ReturnClause @return => new ReturnClause(RewriteItems(@return.Items), @return.Distinct),
            CallSubqueryClause subquery => new CallSubqueryClause(
                subquery.ImportedVariables,
                RewriteClauses(subquery.Body)),
            CallClause call => CallClause.WithAliasedYields(
                call.Procedure,
                call.Arguments.Select(Rewrite).ToArray(),
                call.Yields),
            UnwindClause unwind => new UnwindClause(Rewrite(unwind.Source), unwind.Alias),
            OrderByClause orderBy => new OrderByClause(orderBy.Items
                .Select(item => new OrderByItem(Rewrite(item.Expression), item.Descending))
                .ToArray()),
            SkipClause skip => new SkipClause(Rewrite(skip.Count)),
            LimitClause limit => new LimitClause(Rewrite(limit.Count)),
            _ => clause,
        };

        private ReturnItem[] RewriteItems(IReadOnlyList<ReturnItem> items) => items
            .Select(item => new ReturnItem(Rewrite(item.Expression), item.Alias))
            .ToArray();

        private CypherExpression Rewrite(CypherExpression expression)
        {
            if (expression is AllExpression all)
            {
                Changed = true;
                var source = Rewrite(all.Source);
                var indexAlias = $"__age_relationship_hop{iteratorIndex++}";
                var relationship = new IndexExpression(
                    source,
                    new FunctionCall("toInteger", [new VariableRef(indexAlias)]));
                var predicate = ReplaceVariable(
                    Rewrite(all.Predicate),
                    all.IteratorAlias,
                    relationship);
                var indexes = new FunctionCall(
                    "range",
                    [
                        new Literal(0),
                        new BinaryExpression(
                            CypherBinaryOperator.Subtract,
                            new FunctionCall("size", [source]),
                            new Literal(1)),
                    ]);
                return new BinaryExpression(
                    CypherBinaryOperator.Equal,
                    new FunctionCall(
                        "size",
                        [new ListComprehensionExpression(
                            indexes,
                            indexAlias,
                            predicate: predicate)]),
                    new FunctionCall("size", [source]));
            }

            return expression switch
            {
                BinaryExpression binary => new BinaryExpression(binary.Op, Rewrite(binary.Left), Rewrite(binary.Right)),
                UnaryExpression unary => new UnaryExpression(unary.Op, Rewrite(unary.Operand)),
                PropertyAccess property => new PropertyAccess(Rewrite(property.Target), property.Property),
                EscapedPropertyAccess property => new EscapedPropertyAccess(Rewrite(property.Target), property.Property),
                NativeElementIdentity identity => new NativeElementIdentity(Rewrite(identity.Target)),
                FunctionCall function => new FunctionCall(function.Name, function.Arguments.Select(Rewrite).ToArray()),
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
                MapExpression map => new MapExpression(map.Entries
                    .Select(entry => new MapEntry(entry.Key, Rewrite(entry.Value)))
                    .ToArray()),
                IndexExpression index => new IndexExpression(Rewrite(index.Target), Rewrite(index.Index)),
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

        private static CypherExpression ReplaceVariable(
            CypherExpression expression,
            string alias,
            CypherExpression replacement) => expression switch
            {
                VariableRef variable when variable.Alias == alias => replacement,
                BinaryExpression binary => new BinaryExpression(
                    binary.Op,
                    ReplaceVariable(binary.Left, alias, replacement),
                    ReplaceVariable(binary.Right, alias, replacement)),
                UnaryExpression unary => new UnaryExpression(
                    unary.Op,
                    ReplaceVariable(unary.Operand, alias, replacement)),
                PropertyAccess property => new PropertyAccess(
                    ReplaceVariable(property.Target, alias, replacement),
                    property.Property),
                EscapedPropertyAccess property => new EscapedPropertyAccess(
                    ReplaceVariable(property.Target, alias, replacement),
                    property.Property),
                NativeElementIdentity identity => new NativeElementIdentity(
                    ReplaceVariable(identity.Target, alias, replacement)),
                FunctionCall function => new FunctionCall(
                    function.Name,
                    function.Arguments.Select(argument => ReplaceVariable(argument, alias, replacement)).ToArray()),
                LabelTest label => new LabelTest(ReplaceVariable(label.Target, alias, replacement), label.Labels),
                ListExpression list => new ListExpression(list.Items
                    .Select(item => ReplaceVariable(item, alias, replacement)).ToArray()),
                MapExpression map => new MapExpression(map.Entries
                    .Select(entry => new MapEntry(
                        entry.Key,
                        ReplaceVariable(entry.Value, alias, replacement))).ToArray()),
                IndexExpression index => new IndexExpression(
                    ReplaceVariable(index.Target, alias, replacement),
                    ReplaceVariable(index.Index, alias, replacement)),
                CaseExpression @case => new CaseExpression(
                    ReplaceVariable(@case.Condition, alias, replacement),
                    ReplaceVariable(@case.WhenTrue, alias, replacement),
                    @case.WhenFalse is null
                        ? null
                        : ReplaceVariable(@case.WhenFalse, alias, replacement)),
                ConjunctionExpression conjunction => new ConjunctionExpression(conjunction.Predicates
                    .Select(predicate => ReplaceVariable(predicate, alias, replacement)).ToArray()),
                _ => expression,
            };
    }
}
