// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization;

/// <summary>
/// Schema information for an entity type, mapping .NET types to graph storage structure.
/// </summary>
/// <param name="ExpectedType">The .NET <see cref="Type"/> expected for the entity. Entities might have an actual type
/// that is different from the expected type, as long as it is a derived type of the expected one.</param>
/// <param name="Label">The label for this entity.</param>
/// <param name="IsNullable">Indicates if this entity can be null.</param>
/// <param name="IsSimple">Indicates if this entity is considered simple.
/// The predicate <see cref="GraphDataModel.IsSimple(System.Type)"/> determines if a property is considered simple or complex.</param>
/// <param name="SimpleProperties">A dictionary of simple properties, where the key is the property name and the value is a <see cref="PropertySchema"/>.</param>
/// <param name="ComplexProperties">A dictionary of complex properties, where the key is the property name and the value is a <see cref="PropertySchema"/>.</param>
public record EntitySchema(
    Type ExpectedType,
    string Label,
    bool IsNullable,
    bool IsSimple,
    IReadOnlyDictionary<string, PropertySchema> SimpleProperties,
    IReadOnlyDictionary<string, PropertySchema> ComplexProperties
);
