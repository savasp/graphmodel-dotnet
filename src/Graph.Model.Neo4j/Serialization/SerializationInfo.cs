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

using System.Reflection;

namespace Cvoya.Graph.Model.Neo4j.Serialization;

/// <summary>
/// Represents metadata about a property for serialization purposes
/// </summary>
/// <param name="IsSimple"></param>
/// <param name="PropertyInfo"></param>
/// <param name="IsNullable"></param>
/// <param name="IsCollection"></param>
/// <param name="CollectionElementType"></param>
/// <param name="IsCollectionOfSimple"></param>
/// <param name="Value"></param>
public record IntermediateRepresentation(
    PropertyInfo PropertyInfo,
    bool IsSimple = false,
    bool IsNullable = false,
    bool IsCollection = false,
    Type? CollectionElementType = null,
    bool IsCollectionOfSimple = false,
    object? Value = null);

