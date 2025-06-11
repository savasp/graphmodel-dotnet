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

namespace Cvoya.Graph.Model.Neo4j.Serialization;

/// <summary>
/// Represents a serialized entity.
/// </summary>
/// <param name="Type">The type of the entity.</param>
/// <param name="Label">The label for the entity.</param>
/// <param name="SimpleProperties">A dictionary of simple properties</param>
/// <param name="ComplexProperties">A dictionary of complex properties</param>
public record Entity(
    Type Type,
    string Label,
    IReadOnlyDictionary<string, Property> SimpleProperties,
    IReadOnlyDictionary<string, Property> ComplexProperties
) : Serialized;
