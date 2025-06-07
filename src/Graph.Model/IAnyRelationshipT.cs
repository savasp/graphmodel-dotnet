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
/// Marker interface to indicate traversal through any relationship type.
/// </summary>
/// <remarks>
/// This interface is used in graph traversal operations when you want to
/// follow any relationship type, regardless of its specific implementation.
/// It does not add any additional members beyond those defined in IRelationship.
/// </remarks>
public interface IAnyRelationship : IRelationship
{
    /// <summary>
    /// Gets the label of the relationship.
    /// </summary>
    /// <remarks>
    /// This property provides the type associated with the relationship,
    /// which can be used to filter or categorize relationships during traversal.
    /// </remarks>
    public string Label { get; }
}