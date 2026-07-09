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

namespace Cvoya.Graph.Model.Querying;

using System.Linq.Expressions;

/// <summary>
/// Describes a provider-independent equijoin between a graph query and another graph root.
/// </summary>
public sealed record JoinFragment
{
    /// <summary>
    /// Initializes a new join description.
    /// </summary>
    /// <param name="innerRoot">The joined query root.</param>
    /// <param name="outerKeySelector">The outer key selector.</param>
    /// <param name="innerKeySelector">The inner key selector.</param>
    /// <param name="resultSelector">The result selector.</param>
    public JoinFragment(
        QueryRoot innerRoot,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector)
    {
        InnerRoot = innerRoot ?? throw new ArgumentNullException(nameof(innerRoot));
        OuterKeySelector = outerKeySelector ?? throw new ArgumentNullException(nameof(outerKeySelector));
        InnerKeySelector = innerKeySelector ?? throw new ArgumentNullException(nameof(innerKeySelector));
        ResultSelector = resultSelector ?? throw new ArgumentNullException(nameof(resultSelector));
    }

    /// <summary>Gets the joined query root.</summary>
    public QueryRoot InnerRoot { get; }

    /// <summary>Gets the outer key selector.</summary>
    public LambdaExpression OuterKeySelector { get; }

    /// <summary>Gets the inner key selector.</summary>
    public LambdaExpression InnerKeySelector { get; }

    /// <summary>Gets the result selector.</summary>
    public LambdaExpression ResultSelector { get; }
}
