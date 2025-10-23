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

namespace Cvoya.Graph.Model.Cypher.Querying.Cypher.Builders;

using Microsoft.Extensions.Logging;

/// <summary>
/// Abstraction for query scope management used by the CypherQueryBuilder.
/// This allows different providers to implement their own scope tracking logic.
/// </summary>
public interface ICypherQueryScope
{
    /// <summary>
    /// Gets a value indicating whether the current scope is within a path segment context.
    /// </summary>
    /// <returns>True if in path segment context, false otherwise.</returns>
    bool IsInPathSegmentContext();

    /// <summary>
    /// Gets the current alias being used in the query scope.
    /// </summary>
    string? CurrentAlias { get; }

    /// <summary>
    /// Pushes a new alias onto the scope stack.
    /// </summary>
    /// <param name="alias">The alias to push.</param>
    void PushAlias(string alias);

    /// <summary>
    /// Pops the most recent alias from the scope stack.
    /// </summary>
    void PopAlias();
}

/// <summary>
/// Abstraction for the minimal context needed by CypherQueryBuilder.
/// This removes the circular dependency and allows different providers to supply their own context.
/// </summary>
public interface ICypherQueryBuilderContext
{
    /// <summary>
    /// Gets the query scope for variable tracking.
    /// </summary>
    ICypherQueryScope Scope { get; }

    /// <summary>
    /// Gets the logger factory for diagnostic output.
    /// </summary>
    ILoggerFactory? LoggerFactory { get; }

    /// <summary>
    /// Gets the collection provider for provider-specific collection operations.
    /// </summary>
    ICypherCollectionProvider CollectionProvider { get; }

    /// <summary>
    /// Gets the expression processor for converting .NET expressions to Cypher.
    /// </summary>
    ICypherExpressionProcessor ExpressionProcessor { get; }
}