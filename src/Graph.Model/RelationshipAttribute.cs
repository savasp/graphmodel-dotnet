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

namespace Cvoya.Graph.Model;

/// <summary>
/// Attribute to customize aspects of a relationship in the graph.
/// </summary>
/// <remarks>
/// Creates a new RelationshipAttribute.
/// </remarks>
/// <param name="label">The label to apply to the relationship.</param>
[AttributeUsage(AttributeTargets.Class)]
public class RelationshipAttribute(string label) : Attribute
{
    /// <summary>
    /// The label to apply to the relationship.
    /// </summary>
    public string Label { get; } = label ?? throw new ArgumentNullException(nameof(label));
}
