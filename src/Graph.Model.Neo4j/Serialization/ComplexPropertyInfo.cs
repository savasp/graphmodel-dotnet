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
/// Represents information about a complex property that needs to be serialized as a separate node
/// </summary>
/// <param name="PropertyName">The name of the property containing the complex value</param>
/// <param name="PropertyValue">The actual complex property value (not an INode)</param>
/// <param name="SerializedNode">The serialization result for the complex property</param>
/// <param name="RelationshipType">The relationship type name to use in Neo4j</param>
/// <param name="CollectionIndex">The index in the collection if this is part of a collection property (null for single properties)</param>
internal record ComplexPropertyInfo(
    string PropertyName,
    object PropertyValue,
    NodeSerializationResult SerializedNode,
    string RelationshipType,
    int? CollectionIndex = null);