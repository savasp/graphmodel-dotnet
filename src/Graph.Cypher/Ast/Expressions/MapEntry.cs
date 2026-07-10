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

using Cvoya.Graph.Cypher.Internal;

namespace Cvoya.Graph.Cypher.Ast.Expressions;

/// <summary>Represents one entry in a Cypher map expression.</summary>
public sealed record MapEntry
{
    /// <summary>Initializes a map entry.</summary>
    public MapEntry(string key, CypherExpression value)
    {
        Key = ArgumentValidation.RequiredName(key, nameof(key));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Gets the map key.</summary>
    public string Key { get; }

    /// <summary>Gets the map value.</summary>
    public CypherExpression Value { get; }
}
