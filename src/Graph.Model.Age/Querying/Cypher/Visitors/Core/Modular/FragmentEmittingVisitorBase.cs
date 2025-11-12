// Copyright 2025 Savas Parastatidis

namespace Cvoya.Graph.Model.Age.Querying.Cypher.Visitors.Core.Modular;

using System.Linq.Expressions;
using Cvoya.Graph.Model.Cypher.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Base class for specialized fragment-emitting visitors.
/// Provides common functionality for emitting fragments and accessing shared context.
/// </summary>
internal abstract class FragmentEmittingVisitorBase
{
    protected readonly CypherQueryContext Context;
    protected readonly ILogger Logger;

    protected FragmentEmittingVisitorBase(CypherQueryContext context, ILogger logger)
    {
        Context = context;
        Logger = logger;
    }

    /// <summary>
    /// Emits a fragment to the context's fragment sequence.
    /// Handles exceptions gracefully and logs failures.
    /// </summary>
    protected void EmitFragment(QueryFragment fragment, string fragmentType)
    {
        try
        {
            Context.AddFragment(fragment);
            Logger.LogDebug("Emitted {FragmentType}: {Fragment}", fragmentType, fragment);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to emit {FragmentType} (non-fatal)", fragmentType);
        }
    }

    /// <summary>
    /// Creates an AgeExpressionToCypherVisitor for translating property expressions to Cypher.
    /// </summary>
    protected AgeExpressionToCypherVisitor CreateExpressionVisitor()
    {
        var alias = Context.Scope.CurrentAlias ?? "src0";
        Logger.LogDebug("CreateExpressionVisitor: Using alias '{Alias}'", alias);
    return new AgeExpressionToCypherVisitor(Context, Logger, alias);
    }

    /// <summary>
    /// Extracts a lambda expression from a method call argument.
    /// </summary>
    protected static LambdaExpression? ExtractLambda(Expression argument)
    {
        if (argument is UnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression lambda })
        {
            return lambda;
        }

        return argument as LambdaExpression;
    }
}
