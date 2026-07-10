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

using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Validation;

/// <summary>
/// Runs an ordered set of Cypher passes.
/// </summary>
public sealed class CypherPassRunner : ICypherPass
{
    private readonly IReadOnlyList<ICypherPass> passes;

    /// <summary>
    /// Initializes a new instance of the <see cref="CypherPassRunner"/> class.
    /// </summary>
    /// <param name="passes">The passes to apply in order.</param>
    public CypherPassRunner(IReadOnlyList<ICypherPass> passes)
    {
        this.passes = ArgumentValidation.RequiredList(passes, nameof(passes));
    }

    /// <inheritdoc />
    public CypherStatement Run(CypherStatement input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var current = input;
        foreach (var pass in passes)
        {
            current = pass.Run(current);
        }

        return current;
    }
}
