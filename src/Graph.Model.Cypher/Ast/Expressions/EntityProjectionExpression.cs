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

using Cvoya.Graph.Model.Cypher.Internal;

namespace Cvoya.Graph.Model.Cypher.Ast.Expressions;

/// <summary>
/// Represents a node-valued expression in the provider materialization shape.
/// </summary>
public sealed record EntityProjectionExpression : CypherExpression
{
    /// <summary>Initializes a node-valued projection expression.</summary>
    /// <param name="alias">The node variable to project.</param>
    /// <param name="loadComplexProperties">Whether declared complex properties must be included.</param>
    public EntityProjectionExpression(string alias, bool loadComplexProperties)
    {
        Alias = ArgumentValidation.RequiredName(alias, nameof(alias));
        LoadComplexProperties = loadComplexProperties;
    }

    /// <summary>Gets the node variable to project.</summary>
    public string Alias { get; }

    /// <summary>Gets whether declared complex properties must be included.</summary>
    public bool LoadComplexProperties { get; }
}
