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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors;

using System.Linq.Expressions;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Base class for clause visitors that provides common functionality.
/// </summary>
internal abstract class ClauseVisitorBase<T>(CypherQueryContext context) : CypherVisitorBase<T>(context)
{
    /// <summary>
    /// Gets the current alias from the scope.
    /// </summary>
    /// <exception cref="GraphException">Thrown when no current alias is set.</exception>
    protected string GetCurrentAlias() =>
        Scope.CurrentAlias ?? throw new GraphException(
            $"No current alias set when processing {GetType().Name}. " +
            "This typically indicates a query construction error.");

    /// <summary>
    /// Validates that the expression is a lambda expression with the expected number of parameters.
    /// </summary>
    protected LambdaExpression ValidateLambdaExpression(
        Expression expression,
        int expectedParameterCount = 1,
        string? operationName = null)
    {
        operationName ??= GetType().Name.Replace("Visitor", "");

        if (expression is not LambdaExpression lambda)
        {
            throw new GraphException(
                $"{operationName} requires a lambda expression, but received {expression.NodeType}");
        }

        if (lambda.Parameters.Count != expectedParameterCount)
        {
            throw new GraphException(
                $"{operationName} lambda must have exactly {expectedParameterCount} parameter(s), " +
                $"but has {lambda.Parameters.Count}");
        }

        return lambda;
    }

    /// <summary>
    /// Logs the start of a visitor operation.
    /// </summary>
    protected void LogOperationStart(string operation, Expression? expression = null)
    {
        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug(
                "Starting {Operation} in {Visitor}{Expression}",
                operation,
                GetType().Name,
                expression != null ? $" for expression: {expression}" : "");
        }
    }

    /// <summary>
    /// Logs the completion of a visitor operation.
    /// </summary>
    protected void LogOperationComplete(string operation, string? result = null)
    {
        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug(
                "Completed {Operation} in {Visitor}{Result}",
                operation,
                GetType().Name,
                result != null ? $" with result: {result}" : "");
        }
    }
}