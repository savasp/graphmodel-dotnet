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

using System.Collections.ObjectModel;
using Cvoya.Graph.Model.Cypher.Internal;

namespace Cvoya.Graph.Model.Cypher.Ast;

/// <summary>
/// Represents a complete Cypher statement and its parameter values.
/// </summary>
public sealed record CypherStatement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CypherStatement"/> class.
    /// </summary>
    /// <param name="clauses">The ordered clauses that make up the statement.</param>
    /// <param name="parameters">The parameter values available to the statement.</param>
    public CypherStatement(IReadOnlyList<ICypherClause> clauses, IReadOnlyDictionary<string, object?> parameters)
        : this(clauses, parameters, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CypherStatement"/> class.
    /// </summary>
    /// <param name="clauses">The ordered clauses that make up the statement.</param>
    /// <param name="parameters">The parameter values available to the statement.</param>
    /// <param name="pathTypes">The path materialization types, when the statement returns decomposed paths.</param>
    public CypherStatement(
        IReadOnlyList<ICypherClause> clauses,
        IReadOnlyDictionary<string, object?> parameters,
        CypherPathTypes? pathTypes)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        Clauses = ArgumentValidation.RequiredList(clauses, nameof(clauses));
        Parameters = CopyParameters(parameters);
        PathTypes = pathTypes;
    }

    /// <summary>
    /// Gets the ordered clauses that make up the statement.
    /// </summary>
    public IReadOnlyList<ICypherClause> Clauses { get; }

    /// <summary>
    /// Gets the parameter values available to the statement.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; }

    /// <summary>Gets path materialization metadata, when present.</summary>
    public CypherPathTypes? PathTypes { get; }

    private static IReadOnlyDictionary<string, object?> CopyParameters(IReadOnlyDictionary<string, object?> parameters)
    {
        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var (name, value) in parameters)
        {
            var validatedName = ArgumentValidation.RequiredName(name, nameof(parameters));
            copy.Add(validatedName, value);
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }
}
