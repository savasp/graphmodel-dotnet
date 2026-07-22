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
        IReadOnlyList<object> nativeIdentities) =>
        Plan(mutation, nativeIdentities, []);

    /// <summary>
    /// Plans a mutation and, for an update, clears the given constrained properties behind an
    /// eager barrier before applying the already-computed final setter values.
    /// </summary>
    /// <param name="mutation">The provider-neutral mutation.</param>
    /// <param name="nativeIdentities">The opaque native identities selected in the active transaction.</param>
    /// <param name="stagedStorageNames">Constrained storage properties that require staged replacement.</param>
    /// <returns>The typed mutation statement.</returns>
    internal CypherStatement Plan(
        GraphMutationModel mutation,
        IReadOnlyList<object> nativeIdentities,
        IReadOnlyList<string> stagedStorageNames)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        ArgumentNullException.ThrowIfNull(nativeIdentities);
        ArgumentNullException.ThrowIfNull(stagedStorageNames);
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

        var scalarAssignments = mutation.Assignments
            .Where(assignment => assignment is not GraphConstantPropertyAssignment { IsComplex: true })
            .ToArray();
        if (mutation.Kind == GraphMutationKind.Update && stagedStorageNames.Count > 0)
        {
            AddStagedUpdate(
                clauses,
                scalarAssignments,
                alias,
                target,
                stagedStorageNames,
                parameters);
        }
        else if (mutation.Kind == GraphMutationKind.Update && scalarAssignments.Length > 0)
        {
            var lowerer = new ExpressionToCypherAstLowerer(parameters, dialect);
            var items = scalarAssignments
                .SelectMany(assignment => BuildSetItems(assignment, alias, target, lowerer, parameters))
                .ToArray();
            if (lowerer.NavigationMatches.Count > 0)
            {
                throw new GraphQueryTranslationException(
                    "Computed graph update values cannot navigate complex properties in the first-wave contract.");
            }

            clauses.Add(new SetClause(items));
        }
        else if (mutation.Kind == GraphMutationKind.Delete)
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

    /// <summary>
    /// Plans a read of the complete proposed values for the constrained storage properties of
    /// every frozen update target. Assigned values are computed from the original stored row;
    /// unassigned composite-key members are read from that same row.
    /// </summary>
    /// <param name="mutation">The provider-neutral update mutation.</param>
    /// <param name="nativeIdentities">The opaque native identities selected in the active transaction.</param>
    /// <param name="storageNames">The constrained properties required by final-state validation.</param>
    /// <param name="acquireWriteLock">
    /// Whether the statement should take a dependent-property write lock before reading proposed values.
    /// </param>
    /// <returns>The typed preflight statement.</returns>
    internal CypherStatement PlanConstraintValues(
        GraphMutationModel mutation,
        IReadOnlyList<object> nativeIdentities,
        IReadOnlyList<string> storageNames,
        bool acquireWriteLock)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        ArgumentNullException.ThrowIfNull(nativeIdentities);
        ArgumentNullException.ThrowIfNull(storageNames);
        GraphMutationModelValidator.Validate(mutation);
        if (mutation.Kind != GraphMutationKind.Update)
        {
            throw new ArgumentException("Constraint values can only be planned for an update mutation.", nameof(mutation));
        }

        if (storageNames.Count == 0)
        {
            throw new ArgumentException("At least one constrained storage property is required.", nameof(storageNames));
        }

        var parameters = new CypherParameterRegistry();
        var identityParameter = parameters.Add(nativeIdentities);
        var alias = mutation.Selection.ElementKind == GraphElementKind.Node ? "target" : "relationship";
        var target = new VariableRef(alias);
        var clauses = BuildTargetClauses(mutation, alias, target, identityParameter);
        if (acquireWriteLock)
        {
            clauses.Add(new WithClause(
                [new ReturnItem(target, null)],
                distinct: false));
            clauses.Add(new OrderByClause(
                [new OrderByItem(new NativeElementIdentity(target), descending: false)]));
            var lockProperty = new PropertyAccess(target, storageNames[0]);
            clauses.Add(new SetClause([new SetItem(lockProperty, lockProperty)]));
        }

        var lowerer = new ExpressionToCypherAstLowerer(parameters, dialect);
        var assignments = mutation.Assignments.ToDictionary(
            assignment => assignment.StorageName,
            StringComparer.Ordinal);
        var items = new List<ReturnItem>
        {
            new(new NativeElementIdentity(target), "__nativeId"),
        };
        for (var index = 0; index < storageNames.Count; index++)
        {
            var storageName = storageNames[index];
            var value = assignments.TryGetValue(storageName, out var assignment)
                ? LowerValue(assignment, alias, lowerer, parameters)
                : new PropertyAccess(target, storageName);
            items.Add(new ReturnItem(value, ConstraintValueColumn(index)));
        }

        if (lowerer.NavigationMatches.Count > 0)
        {
            throw new GraphQueryTranslationException(
                "Computed graph update values cannot navigate complex properties in the first-wave contract.");
        }

        clauses.Add(new ReturnClause(items, distinct: false));
        var statement = new CypherStatement(clauses, parameters.Parameters);

#if DEBUG
        new CypherAstValidator().Run(statement);
#endif

        return statement;
    }

    /// <summary>Gets the stable projection column for a constrained property index.</summary>
    /// <param name="index">The zero-based constrained property index.</param>
    /// <returns>The projection column name.</returns>
    internal static string ConstraintValueColumn(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return $"__constraintValue{index}";
    }

    private void AddStagedUpdate(
        List<ICypherClause> clauses,
        GraphPropertyAssignment[] assignments,
        string alias,
        VariableRef target,
        IReadOnlyList<string> stagedStorageNames,
        CypherParameterRegistry parameters)
    {
        var lowerer = new ExpressionToCypherAstLowerer(parameters, dialect);
        var storedValues = new List<(string StorageName, CypherExpression Value, string Alias)>();
        foreach (var assignment in assignments)
        {
            EnsureComputedCollectionCanBeStored(assignment);
            if (assignment is GraphConstantPropertyAssignment constant)
            {
                var declaredType = constant.Property?.PropertyType;
                foreach (var value in dialect.EncodePropertyValue(
                    assignment.StorageName,
                    declaredType,
                    constant.Value))
                {
                    storedValues.Add((
                        value.StorageName,
                        parameters.Add(value.Value),
                        FinalValueAlias(storedValues.Count)));
                }

                continue;
            }

            storedValues.Add((
                dialect.GetPropertyStorageName(assignment.StorageName),
                LowerValue(assignment, alias, lowerer, parameters),
                FinalValueAlias(storedValues.Count)));
            foreach (var companion in dialect.GetPropertyCompanionStorageNames(assignment.StorageName))
            {
                storedValues.Add((companion, new Literal(null), FinalValueAlias(storedValues.Count)));
            }
        }

        var finalItems = new List<ReturnItem>
        {
            new(target, alias),
        };
        foreach (var storedValue in storedValues)
        {
            finalItems.Add(new ReturnItem(
                storedValue.Value,
                storedValue.Alias));
        }

        if (lowerer.NavigationMatches.Count > 0)
        {
            throw new GraphQueryTranslationException(
                "Computed graph update values cannot navigate complex properties in the first-wave contract.");
        }

        clauses.Add(new WithClause(finalItems, distinct: false));
        clauses.Add(new SetClause(stagedStorageNames
            .Distinct(StringComparer.Ordinal)
            .Select(storageName => new SetItem(
                new PhysicalPropertyAccess(target, dialect.GetPropertyStorageName(storageName)),
                new Literal(null)))
            .ToArray()));

        var rowEntries = new List<MapEntry>
        {
            new("target", target),
        };
        foreach (var storedValue in storedValues)
        {
            rowEntries.Add(new MapEntry(storedValue.Alias, new VariableRef(storedValue.Alias)));
        }

        clauses.Add(new WithClause(
            [new ReturnItem(new FunctionCall("collect", [new MapExpression(rowEntries)]), "__rows")],
            distinct: false));
        clauses.Add(new UnwindClause(new VariableRef("__rows"), "__row"));

        var restoredItems = new List<ReturnItem>
        {
            new(new PropertyAccess(new VariableRef("__row"), "target"), alias),
        };
        foreach (var storedValue in storedValues)
        {
            restoredItems.Add(new ReturnItem(
                new PropertyAccess(new VariableRef("__row"), storedValue.Alias),
                storedValue.Alias));
        }

        clauses.Add(new WithClause(restoredItems, distinct: false));
        clauses.Add(new SetClause(storedValues.Select(storedValue => new SetItem(
            new PhysicalPropertyAccess(target, storedValue.StorageName),
            new VariableRef(storedValue.Alias))).ToArray()));
    }

    private static List<ICypherClause> BuildTargetClauses(
        GraphMutationModel mutation,
        string alias,
        VariableRef target,
        QueryParameter identityParameter) =>
        [
            new UnwindClause(identityParameter, "__nativeId"),
            BuildMatch(mutation.Selection.ElementKind, alias),
            new WhereClause(new AstBinaryExpression(
                CypherBinaryOperator.Equal,
                new NativeElementIdentity(target),
                new VariableRef("__nativeId"))),
        ];

    private static string FinalValueAlias(int index) => $"__finalValue{index}";

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
            GraphComputedPropertyAssignment computed => lowerer.LowerLambda(
                computed.ValueExpression,
                alias,
                computed.ValueExpression.Parameters.ToDictionary(parameter => parameter, _ => alias)),
            _ => throw new GraphException(
                $"Graph property assignment '{assignment.GetType().Name}' is not supported."),
        };

    private IEnumerable<SetItem> BuildSetItems(
        GraphPropertyAssignment assignment,
        string alias,
        VariableRef target,
        ExpressionToCypherAstLowerer lowerer,
        CypherParameterRegistry parameters)
    {
        EnsureComputedCollectionCanBeStored(assignment);
        if (assignment is GraphConstantPropertyAssignment constant)
        {
            var declaredType = constant.Property?.PropertyType;
            foreach (var value in dialect.EncodePropertyValue(
                assignment.StorageName,
                declaredType,
                constant.Value))
            {
                yield return new SetItem(
                    new PhysicalPropertyAccess(target, value.StorageName),
                    parameters.Add(value.Value));
            }

            yield break;
        }

        yield return new SetItem(
            new PhysicalPropertyAccess(target, dialect.GetPropertyStorageName(assignment.StorageName)),
            LowerValue(assignment, alias, lowerer, parameters));
        foreach (var companion in dialect.GetPropertyCompanionStorageNames(assignment.StorageName))
        {
            yield return new SetItem(
                new PhysicalPropertyAccess(target, companion),
                new Literal(null));
        }
    }

    private void EnsureComputedCollectionCanBeStored(GraphPropertyAssignment assignment)
    {
        if (assignment is GraphComputedPropertyAssignment computed &&
            ExpressionToCypherAstLowerer.IsReconstructedSimpleCollection(
                assignment.Property?.PropertyType ?? computed.ValueExpression.ReturnType) &&
            dialect.GetPropertyCompanionStorageNames(assignment.StorageName).Count > 0)
        {
            throw new GraphQueryTranslationException(
                "Computed simple-collection updates are not supported by this provider's physical collection encoding.");
        }
    }
}
