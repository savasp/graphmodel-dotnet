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

using Cvoya.Graph.Cypher.Ast.Expressions;
using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>
/// Represents a Cypher procedure call clause.
/// </summary>
public sealed record CallClause : ICypherClause
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CallClause"/> class.
    /// </summary>
    /// <param name="procedure">The procedure name.</param>
    /// <param name="arguments">The procedure arguments.</param>
    /// <param name="yields">The yielded variables.</param>
    public CallClause(string procedure, IReadOnlyList<CypherExpression> arguments, IReadOnlyList<string> yields)
        : this(procedure, arguments, yields.Select(name => new CallYield(name)))
    {
    }

    private CallClause(
        string procedure,
        IReadOnlyList<CypherExpression> arguments,
        IEnumerable<CallYield> yields)
    {
        Procedure = ArgumentValidation.RequiredName(procedure, nameof(procedure));
        Arguments = ArgumentValidation.List(arguments, nameof(arguments));
        Yields = ArgumentValidation.List(yields.ToArray(), nameof(yields));
    }

    /// <summary>Creates a procedure call with optionally aliased yielded values.</summary>
    /// <param name="procedure">The procedure name.</param>
    /// <param name="arguments">The procedure arguments.</param>
    /// <param name="yields">The yielded values and their optional local aliases.</param>
    /// <returns>A procedure call clause.</returns>
    public static CallClause WithAliasedYields(
        string procedure,
        IReadOnlyList<CypherExpression> arguments,
        IReadOnlyList<CallYield> yields) => new(procedure, arguments, yields);

    /// <summary>
    /// Gets the procedure name.
    /// </summary>
    public string Procedure { get; }

    /// <summary>
    /// Gets the procedure arguments.
    /// </summary>
    public IReadOnlyList<CypherExpression> Arguments { get; }

    /// <summary>
    /// Gets the yielded variables.
    /// </summary>
    public IReadOnlyList<CallYield> Yields { get; }
}
