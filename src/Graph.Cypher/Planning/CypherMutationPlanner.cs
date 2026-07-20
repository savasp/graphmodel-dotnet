// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Cypher.Planning;

using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;
using Cvoya.Graph.Cypher.Validation;
using Cvoya.Graph.Querying;
using AstBinaryExpression = Cvoya.Graph.Cypher.Ast.Expressions.BinaryExpression;

/// <summary>Lowers a frozen native target set and a simple mutation into a typed Cypher statement.</summary>
public sealed class CypherMutationPlanner
{
    private readonly ICypherDialect dialect;

    /// <summary>Initializes a mutation planner for a Cypher dialect.</summary>
    /// <param name="dialect">The target Cypher dialect.</param>
    public CypherMutationPlanner(ICypherDialect dialect) =>
        this.dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));

    /// <summary>Plans a mutation over an already-frozen set of provider-native identities.</summary>
    /// <param name="mutation">The provider-neutral mutation.</param>
    /// <param name="nativeIdentities">The opaque native identities selected in the active transaction.</param>
    /// <returns>The typed mutation statement.</returns>
    public CypherStatement Plan(
        GraphMutationModel mutation,
        IReadOnlyList<object> nativeIdentities)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        ArgumentNullException.ThrowIfNull(nativeIdentities);
        GraphMutationModelValidator.Validate(mutation);

        var parameters = new CypherParameterRegistry();
        var identityParameter = parameters.Add(nativeIdentities);
        var alias = mutation.Selection.ElementKind == GraphElementKind.Node ? "target" : "relationship";
        var target = new VariableRef(alias);
        var clauses = new List<ICypherClause>
        {
            new UnwindClause(identityParameter, "__nativeId"),
            BuildMatch(mutation.Selection.ElementKind, alias),
            new WhereClause(new AstBinaryExpression(
                CypherBinaryOperator.Equal,
                new NativeElementIdentity(target),
                new VariableRef("__nativeId"))),
        };

        if (mutation.Kind == GraphMutationKind.Update)
        {
            var lowerer = new ExpressionToCypherAstLowerer(parameters, dialect);
            var items = mutation.Assignments.Select(assignment => new SetItem(
                assignment.Dynamic
                    ? new EscapedPropertyAccess(target, assignment.StorageName)
                    : new PropertyAccess(target, assignment.StorageName),
                LowerValue(assignment, alias, lowerer, parameters))).ToArray();
            if (lowerer.NavigationMatches.Count > 0)
            {
                throw new GraphQueryTranslationException(
                    "Computed graph update values cannot navigate complex properties in the first-wave contract.");
            }

            clauses.Add(new SetClause(items));
        }
        else
        {
            clauses.Add(new DeleteClause(
                [target],
                detach: mutation.Selection.ElementKind == GraphElementKind.Node && mutation.CascadeDelete));
        }

        clauses.Add(new ReturnClause(
            [new ReturnItem(new FunctionCall("count", [target]), "affectedCount")],
            distinct: false));
        var statement = new CypherStatement(clauses, parameters.Parameters);

#if DEBUG
        new CypherAstValidator().Run(statement);
#endif

        return statement;
    }

    private static MatchClause BuildMatch(GraphElementKind kind, string alias) => kind switch
    {
        GraphElementKind.Node => new MatchClause(
            [new PathPattern([new NodePattern(alias, [])])],
            optional: false),
        GraphElementKind.Relationship => new MatchClause(
            [new PathPattern(
            [
                new NodePattern("source", []),
                new RelationshipPattern(alias, CypherDirection.Outgoing, depth: null, types: []),
                new NodePattern("target", []),
            ])],
            optional: false),
        _ => throw new GraphException($"Graph element kind '{kind}' is not supported."),
    };

    private static CypherExpression LowerValue(
        GraphPropertyAssignment assignment,
        string alias,
        ExpressionToCypherAstLowerer lowerer,
        CypherParameterRegistry parameters) => assignment switch
    {
        GraphConstantPropertyAssignment constant => parameters.Add(constant.Value),
        GraphComputedPropertyAssignment computed => lowerer.LowerLambda(computed.ValueExpression, alias),
        _ => throw new GraphException(
            $"Graph property assignment '{assignment.GetType().Name}' is not supported."),
    };
}
