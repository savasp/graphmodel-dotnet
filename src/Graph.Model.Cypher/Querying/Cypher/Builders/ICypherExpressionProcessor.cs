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

using System.Linq.Expressions;

/// <summary>
/// Provides provider-specific conversion of .NET expressions to Cypher syntax.
/// This abstraction allows different Cypher providers (Neo4j, AGE) to handle
/// provider-specific extensions like APOC functions.
/// </summary>
public interface ICypherExpressionProcessor
{
    /// <summary>
    /// Converts a .NET expression tree to Cypher syntax.
    /// </summary>
    /// <param name="expression">The expression to convert.</param>
    /// <param name="alias">The alias to use for the entity in the expression.</param>
    /// <returns>The Cypher representation of the expression.</returns>
    string ProcessExpression(Expression expression, string alias);
}