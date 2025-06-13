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

namespace Cvoya.Graph.Model.Serialization;

/// <summary>
/// Represents a collection of values. The predicate <see cref="Model.GraphDataModel.IsSimple(System.Type)"/>
/// determines if a value is considered simple.
/// </summary>
/// <param name="Values">An <see cref="IReadOnlyCollection{SimpleValue}"/> of simple values.</param>
/// <param name="ElementType">The type of elements in the collection.</param>
public record SimpleCollection(
    IReadOnlyCollection<SimpleValue> Values,
    Type ElementType
) : Serialized;
