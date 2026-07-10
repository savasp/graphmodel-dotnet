// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.
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

namespace Cvoya.Graph.Cypher.Ast;

/// <summary>
/// Represents a node pattern in a Cypher path.
/// </summary>
public sealed record NodePattern : PatternElement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NodePattern"/> class.
    /// </summary>
    /// <param name="alias">The optional node alias.</param>
    /// <param name="labels">The node labels.</param>
    public NodePattern(string? alias, IReadOnlyList<string> labels)
    {
        Alias = ArgumentValidation.OptionalName(alias, nameof(alias));
        Labels = ArgumentValidation.StringList(labels, nameof(labels));
    }

    /// <summary>
    /// Gets the optional node alias.
    /// </summary>
    public string? Alias { get; }

    /// <summary>
    /// Gets the node labels.
    /// </summary>
    public IReadOnlyList<string> Labels { get; }
}
