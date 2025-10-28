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
/// Provides provider-specific value conversion from EntityInfo Property objects to strongly-typed .NET values.
/// This interface enables different graph database providers (Neo4j, PostgreSQL AGE, Gremlin, etc.) 
/// to handle their specific type systems while sharing common materialization logic.
/// </summary>
public interface IValueConverter
{
    /// <summary>
    /// Converts a Property from EntityInfo to the target type T.
    /// Uses generics to avoid boxing of value types and provides rich context through Property metadata.
    /// </summary>
    /// <typeparam name="T">Target type for conversion</typeparam>
    /// <param name="property">Property containing SimpleValue, SimpleCollection, EntityInfo, or EntityCollection</param>
    /// <returns>Converted value of type T, or null if conversion fails</returns>
    /// <exception cref="ArgumentNullException">Thrown when property is null</exception>
    /// <exception cref="NotSupportedException">Thrown when property.Value type is not supported</exception>
    /// <exception cref="InvalidOperationException">Thrown when conversion fails due to incompatible types</exception>
    T? ConvertValue<T>(Property property);
}