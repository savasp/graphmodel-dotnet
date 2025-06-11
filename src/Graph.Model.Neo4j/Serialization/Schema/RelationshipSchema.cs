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

using System;
using System.Collections.Generic;

namespace Cvoya.Graph.Model.Neo4j.Serialization;

/// <summary>
/// Schema information for a relationship type, mapping .NET types to Neo4j relationship structure.
/// </summary>
/// <param name="Type">The .NET type this schema represents.</param>
/// <param name="Label">The Neo4j relationship type.</param>
/// <param name="Properties">Property mapping information, keyed by Neo4j property name.</param>
/// <param name="StartNodeLabel">Expected label of the start node (optional for validation).</param>
/// <param name="EndNodeLabel">Expected label of the end node (optional for validation).</param>
public record RelationshipSchema(
    Type Type,
    string Label,
    IReadOnlyDictionary<string, PropertySchema> Properties,
    string? StartNodeLabel = null,
    string? EndNodeLabel = null
);