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

namespace Cvoya.Graph.Model.Age.Core.Entities;

using Cvoya.Graph.Model.Serialization;

/// <summary>
/// Age-specific implementation of IValueConverter that delegates to AgeSerializationBridge.
/// Handles AGE's specific type conversions including temporal types (DateTime), spatial types (Point),
/// Boolean string handling, and PostgreSQL/AGE-specific type mappings.
/// </summary>
internal sealed class AgeValueConverter : IValueConverter
{
    /// <summary>
    /// Converts a Property from EntityInfo to the target type T using Age-specific type conversion logic.
    /// </summary>
    /// <typeparam name="T">Target type for conversion</typeparam>
    /// <param name="property">Property containing SimpleValue, SimpleCollection, EntityInfo, or EntityCollection</param>
    /// <returns>Converted value of type T, or null if conversion fails</returns>
    /// <exception cref="ArgumentNullException">Thrown when property is null</exception>
    /// <exception cref="NotSupportedException">Thrown when property.Value type is not supported</exception>
    public T? ConvertValue<T>(Property property)
    {
        ArgumentNullException.ThrowIfNull(property);

        if (property.Value is null)
        {
            return default;
        }

        if (property.Value is SimpleValue simpleValue)
        {
            if (simpleValue.Object is DBNull)
            {
                return default;
            }

            try
            {
                return (T?)AgeSerializationBridge.FromAgeValue(simpleValue.Object, typeof(T));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to convert SimpleValue of type {simpleValue.Type.Name} to {typeof(T).Name}", ex);
            }
        }

        throw new NotSupportedException($"Unsupported property value type: {property.Value.GetType()}");
    }
}