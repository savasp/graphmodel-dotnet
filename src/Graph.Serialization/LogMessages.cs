// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

global using Cvoya.Graph.Logging;

using Microsoft.Extensions.Logging;

namespace Cvoya.Graph.Logging;

internal static partial class LogMessages
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "ProcessDynamicProperties: Processing {PropertyCount} properties")]
    internal static partial void LogDebugEntityFactory533(this ILogger logger, global::System.Int32 propertyCount);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "ProcessDynamicProperties: Processing property '{PropertyName}' with value '{PropertyValue}' of type '{PropertyType}'")]
    internal static partial void LogDebugEntityFactory540(this ILogger logger, global::System.String propertyName, global::System.Object? propertyValue, global::System.String propertyType);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "ProcessDynamicProperties: Adding simple property '{PropertyName}' to simpleProperties")]
    internal static partial void LogDebugEntityFactory574(this ILogger logger, global::System.String propertyName);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug, Message = "MaterializeAsync: TargetType={TargetType}, ElementType={ElementType}, IsCollectionType={IsCollectionType}, RecordCount={RecordCount}")]
    internal static partial void LogDebugGraphResultMaterializer38(this ILogger logger, global::System.String targetType, global::System.String elementType, global::System.Boolean isCollectionType, global::System.Int32 recordCount);

    [LoggerMessage(EventId = 5, Level = LogLevel.Debug, Message = "MaterializeAsync: ProcessAsync returned {EntityInfoCount} entity infos")]
    internal static partial void LogDebugGraphResultMaterializer65(this ILogger logger, global::System.Int32 entityInfoCount);

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug, Message = "MaterializeAsync: Final result type={ResultType}")]
    internal static partial void LogDebugGraphResultMaterializer71(this ILogger logger, global::System.String resultType);

    [LoggerMessage(EventId = 7, Level = LogLevel.Debug, Message = "MaterializeAsync: Collection result contains {Count} items")]
    internal static partial void LogDebugGraphResultMaterializer75(this ILogger logger, global::System.Int32 count);

    [LoggerMessage(EventId = 8, Level = LogLevel.Debug, Message = "MaterializeRecordAsync: ProcessAsync returned {EntityInfoCount} entity infos")]
    internal static partial void LogDebugGraphResultMaterializer93(this ILogger logger, global::System.Int32 entityInfoCount);

    [LoggerMessage(EventId = 9, Level = LogLevel.Debug, Message = "MaterializeAsCollection: Processing {EntityInfoCount} entity infos for element type {ElementType}")]
    internal static partial void LogDebugGraphResultMaterializer156(this ILogger logger, global::System.Int32 entityInfoCount, global::System.String elementType);

    [LoggerMessage(EventId = 10, Level = LogLevel.Debug, Message = "MaterializeAsCollection: Materialized {ElementCount} non-null elements")]
    internal static partial void LogDebugGraphResultMaterializer164(this ILogger logger, global::System.Int32 elementCount);

    [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "MaterializeAsCollection: Converted to collection type {ResultType}")]
    internal static partial void LogDebugGraphResultMaterializer167(this ILogger logger, global::System.String resultType);

    [LoggerMessage(EventId = 12, Level = LogLevel.Debug, Message = "MaterializeSingleElement: ElementType={ElementType}, ActualType={ActualType}, CanDeserialize={CanDeserialize}")]
    internal static partial void LogDebugGraphResultMaterializer181(this ILogger logger, global::System.String elementType, global::System.String actualType, global::System.Boolean canDeserialize);

    [LoggerMessage(EventId = 13, Level = LogLevel.Debug, Message = "CreateComplexObject: Creating {TypeName} from EntityInfo with {SimpleCount} simple properties, {ComplexCount} complex properties")]
    internal static partial void LogDebugGraphResultMaterializer278(this ILogger logger, global::System.String typeName, global::System.Int32 simpleCount, global::System.Int32 complexCount);

    [LoggerMessage(EventId = 14, Level = LogLevel.Debug, Message = "CreateComplexObject: Simple property names: [{SimpleProps}]")]
    internal static partial void LogDebugGraphResultMaterializer283(this ILogger logger, global::System.String simpleProps);

    [LoggerMessage(EventId = 15, Level = LogLevel.Debug, Message = "CreateComplexObject: Complex property types: [{ComplexProps}]")]
    internal static partial void LogDebugGraphResultMaterializer285(this ILogger logger, global::System.String complexProps);

    [LoggerMessage(EventId = 16, Level = LogLevel.Debug, Message = "CreateComplexObject: Trying constructor with {ParamCount} parameters: [{Params}]")]
    internal static partial void LogDebugGraphResultMaterializer306(this ILogger logger, global::System.Int32 paramCount, global::System.String @params);

    [LoggerMessage(EventId = 17, Level = LogLevel.Debug, Message = "CreateComplexObject: Parameter {ParamName} -> Property {PropertyFound} (Type: {PropertyType})")]
    internal static partial void LogDebugGraphResultMaterializer316(this ILogger logger, global::System.String paramName, global::System.String propertyFound, global::System.String propertyType);

    [LoggerMessage(EventId = 18, Level = LogLevel.Debug, Message = "CreateComplexObject: Parameter {ParamName} set to type {ValueType}")]
    internal static partial void LogDebugGraphResultMaterializer327(this ILogger logger, global::System.String paramName, global::System.String valueType);

    [LoggerMessage(EventId = 19, Level = LogLevel.Debug, Message = "CreateComplexObject: Successfully created {TypeName} instance")]
    internal static partial void LogDebugGraphResultMaterializer335(this ILogger logger, global::System.String typeName);

    [LoggerMessage(EventId = 20, Level = LogLevel.Debug, Message = "CreateComplexObject: Constructor failed: {Exception}")]
    internal static partial void LogDebugGraphResultMaterializer340(this ILogger logger, global::System.String exception);

    [LoggerMessage(EventId = 21, Level = LogLevel.Debug, Message = "Processing records for target type: {TargetType}")]
    internal static partial void LogDebugGraphResultProcessor44(this ILogger logger, global::System.String targetType);

    [LoggerMessage(EventId = 22, Level = LogLevel.Warning, Message = "No schema found for node type {NodeType}. Cannot deserialize complex properties.")]
    internal static partial void LogWarningGraphResultProcessor280(this ILogger logger, global::System.String nodeType);

    [LoggerMessage(EventId = 23, Level = LogLevel.Debug, Message = "Complex property structure node has {PropertyCount} properties: [{Properties}]")]
    internal static partial void LogDebugGraphResultProcessor528(this ILogger logger, global::System.Int32 propertyCount, global::System.String properties);

    [LoggerMessage(EventId = 24, Level = LogLevel.Debug, Message = "Complex properties found: {Count} items")]
    internal static partial void LogDebugGraphResultProcessor542(this ILogger logger, global::System.Int32 count);

    [LoggerMessage(EventId = 25, Level = LogLevel.Debug, Message = "Created EntityInfo with {SimpleCount} simple properties: [{SimpleProps}], {ComplexCount} complex properties")]
    internal static partial void LogDebugGraphResultProcessor569(this ILogger logger, global::System.Int32 simpleCount, global::System.String simpleProps, global::System.Int32 complexCount);

    [LoggerMessage(EventId = 26, Level = LogLevel.Debug, Message = "Using label-based type DynamicNode for target type {TargetType}")]
    internal static partial void LogDebugGraphResultProcessor1107(this ILogger logger, global::System.String targetType);

    [LoggerMessage(EventId = 27, Level = LogLevel.Debug, Message = "Using metadata type {MetadataType} for target type {TargetType}")]
    internal static partial void LogDebugGraphResultProcessor1116(this ILogger logger, global::System.String metadataType, global::System.String targetType);

    [LoggerMessage(EventId = 28, Level = LogLevel.Debug, Message = "Using label-based type {LabelType} for target type {TargetType}")]
    internal static partial void LogDebugGraphResultProcessor1125(this ILogger logger, global::System.String labelType, global::System.String targetType);

    [LoggerMessage(EventId = 29, Level = LogLevel.Debug, Message = "Falling back to target type {TargetType} for node with labels [{Labels}]")]
    internal static partial void LogDebugGraphResultProcessor1133(this ILogger logger, global::System.String targetType, global::System.String labels);

    [LoggerMessage(EventId = 30, Level = LogLevel.Debug, Message = "Using relationship type-based type DynamicRelationship for target type {TargetType}")]
    internal static partial void LogDebugGraphResultProcessor1147(this ILogger logger, global::System.String targetType);

    [LoggerMessage(EventId = 31, Level = LogLevel.Debug, Message = "Using metadata type {MetadataType} for target type {TargetType}")]
    internal static partial void LogDebugGraphResultProcessor1156(this ILogger logger, global::System.String metadataType, global::System.String targetType);

    [LoggerMessage(EventId = 32, Level = LogLevel.Debug, Message = "Using relationship type-based type {LabelType} for target type {TargetType}")]
    internal static partial void LogDebugGraphResultProcessor1165(this ILogger logger, global::System.String labelType, global::System.String targetType);

    [LoggerMessage(EventId = 33, Level = LogLevel.Debug, Message = "Falling back to target type {TargetType} for relationship with type {RelType}")]
    internal static partial void LogDebugGraphResultProcessor1171(this ILogger logger, global::System.String targetType, global::System.String relType);

}
