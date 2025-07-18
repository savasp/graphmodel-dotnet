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
/// Schema information for an entity type.
/// </summary>
public class EntitySchemaInfo
{
    /// <summary>
    /// Gets the .NET type of the entity.
    /// </summary>
    public Type Type { get; set; } = null!;

    /// <summary>
    /// Gets the label/type name used in the graph database.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets the property schema information for this entity.
    /// </summary>
    public IDictionary<string, PropertySchemaInfo> Properties { get; set; } = new Dictionary<string, PropertySchemaInfo>();

    /// <summary>
    /// Gets all key properties for this entity.
    /// </summary>
    /// <returns>An enumerable of key property schema information.</returns>
    public IEnumerable<PropertySchemaInfo> GetKeyProperties()
    {
        return Properties.Values.Where(p => p.IsKey).OrderBy(p => p.Name);
    }

    /// <summary>
    /// Gets whether this entity has a composite key (multiple key properties).
    /// </summary>
    /// <returns>True if the entity has multiple key properties, false otherwise.</returns>
    public bool HasCompositeKey()
    {
        return Properties.Values.Count(p => p.IsKey) > 1;
    }

    /// <summary>
    /// Gets whether this entity has any key properties.
    /// </summary>
    /// <returns>True if the entity has at least one key property, false otherwise.</returns>
    public bool HasKey()
    {
        return Properties.Values.Any(p => p.IsKey);
    }
}

