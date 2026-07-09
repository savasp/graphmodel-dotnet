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
/// Configures the graph relationship used to store a complex property.
/// </summary>
/// <remarks>
/// By default, a complex property is connected to its owning node by a relationship whose
/// type is the property name. Use this attribute when the graph relationship needs a different
/// semantic name.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class ComplexPropertyAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the relationship type used for the complex property.
    /// </summary>
    public string? RelationshipType { get; set; }
}
