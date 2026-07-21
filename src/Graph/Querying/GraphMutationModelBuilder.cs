// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

using System.Linq.Expressions;
using System.Reflection;

/// <summary>Builds the provider-neutral graph-mutation envelope from an internal marker expression.</summary>
internal static class GraphMutationModelBuilder
{
    private static readonly MethodInfo UpdateDefinition = typeof(GraphMutationMarkers)
        .GetMethod(nameof(GraphMutationMarkers.UpdateMarker), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo DeleteDefinition = typeof(GraphMutationMarkers)
        .GetMethod(nameof(GraphMutationMarkers.DeleteMarker), BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>Gets whether the expression is one of the graph mutation markers.</summary>
    public static bool IsMutation(Expression expression) =>
        expression is MethodCallExpression call && ResolveDefinition(call.Method) is { } definition &&
        (definition == UpdateDefinition || definition == DeleteDefinition);

    /// <summary>Builds and validates a mutation model.</summary>
    public static GraphMutationModel Build(Expression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        if (expression is not MethodCallExpression call || ResolveDefinition(call.Method) is not { } definition ||
            (definition != UpdateDefinition && definition != DeleteDefinition))
        {
            throw new GraphQueryTranslationException("The expression is not a recognized graph mutation marker.");
        }

        var query = GraphQueryModelBuilder.Build(call.Arguments[0]);
        var selection = new GraphElementSelectionModel(query, GraphElementSelectionMode.Set);
        var kind = definition == UpdateDefinition ? GraphMutationKind.Update : GraphMutationKind.Delete;
        var assignments = kind == GraphMutationKind.Update
            ? ParseAssignments(call.Arguments[1], call.Method.GetGenericArguments()[0])
            : [];
        var cascadeDelete = kind == GraphMutationKind.Delete && Evaluate<bool>(call.Arguments[1], "cascade delete");
        var model = new GraphMutationModel(kind, selection, assignments, cascadeDelete);
        GraphMutationModelValidator.Validate(model);
        return model;
    }

    private static List<GraphPropertyAssignment> ParseAssignments(
        Expression expression,
        Type entityType)
    {
        var lambda = RequireLambda(expression, "update setter chain");
        if (lambda.Parameters.Count != 1 ||
            lambda.Parameters[0].Type != typeof(GraphPropertySetters<>).MakeGenericType(entityType))
        {
            throw new GraphQueryTranslationException(
                $"The update setter expression must accept GraphPropertySetters<{entityType.Name}>.");
        }

        var assignments = new List<GraphPropertyAssignment>();
        ParseSetterChain(StripConvert(lambda.Body), lambda.Parameters[0], entityType, assignments);
        return assignments;
    }

    private static void ParseSetterChain(
        Expression expression,
        ParameterExpression setters,
        Type entityType,
        List<GraphPropertyAssignment> assignments)
    {
        expression = StripConvert(expression);
        if (expression == setters)
        {
            return;
        }

        if (expression is not MethodCallExpression call || call.Object is null ||
            call.Method.Name != nameof(GraphPropertySetters<IEntity>.SetProperty) ||
            call.Method.DeclaringType is not { IsGenericType: true } declaring ||
            declaring.GetGenericTypeDefinition() != typeof(GraphPropertySetters<>))
        {
            throw new GraphQueryTranslationException(
                "The update setter expression must be a chain of SetProperty calls.");
        }

        ParseSetterChain(call.Object, setters, entityType, assignments);
        if (call.Arguments.Count != 2)
        {
            throw new GraphQueryTranslationException("SetProperty requires one property selector and one value.");
        }

        var propertySelector = RequireLambda(call.Arguments[0], "SetProperty selector");
        var target = ParseTarget(propertySelector, entityType);
        var valueExpression = TryGetLambda(call.Arguments[1]);
        var constantValue = valueExpression is null
            ? Evaluate<object?>(call.Arguments[1], $"value for '{target.StorageName}'")
            : null;
        var isComplex = target.Complex ||
            target.Dynamic && constantValue is not null && IsDynamicComplexValue(constantValue.GetType());
        if (valueExpression is null)
        {
            ValidateConstantValue(
                constantValue,
                target.Property,
                target.Dynamic,
                isComplex,
                entityType,
                target.StorageName);
        }
        else if (target.Complex)
        {
            throw new GraphQueryTranslationException(
                $"Complex property '{target.StorageName}' requires a constant or captured value; " +
                "provider-computed complex values are not supported.");
        }

        GraphPropertyAssignment assignment = valueExpression is null
            ? new GraphConstantPropertyAssignment(
                propertySelector,
                target.Property,
                target.StorageName,
                target.Dynamic,
                constantValue,
                isComplex)
            : new GraphComputedPropertyAssignment(
                propertySelector,
                target.Property,
                target.StorageName,
                target.Dynamic,
                ValidateComputedValue(valueExpression, entityType, target.StorageName));
        assignments.Add(assignment);
    }

    private static (PropertyInfo? Property, string StorageName, bool Dynamic, bool Complex) ParseTarget(
        LambdaExpression selector,
        Type entityType)
    {
        if (selector.Parameters.Count != 1 || selector.Parameters[0].Type != entityType)
        {
            throw new GraphQueryTranslationException(
                $"A property selector must accept the current entity type '{entityType.FullName}'.");
        }

        var body = StripConvert(selector.Body);
        if (body is MemberExpression
            {
                Expression: { } instance,
                Member: PropertyInfo property,
            } && StripConvert(instance) == selector.Parameters[0])
        {
            return (
                property,
                Labels.GetLabelFromProperty(property),
                Dynamic: false,
                Complex: ValidateMappedProperty(property));
        }

        if (TryGetDynamicPropertyKey(body, selector.Parameters[0], out var dynamicKey))
        {
            return (null, dynamicKey, Dynamic: true, Complex: false);
        }

        throw new GraphQueryTranslationException(
            "A SetProperty selector must target one direct mapped property or a constant dynamic Properties key.");
    }

    private static bool TryGetDynamicPropertyKey(
        Expression expression,
        ParameterExpression entity,
        out string key)
    {
        key = string.Empty;
        Expression? properties;
        Expression? index;
        switch (expression)
        {
            case IndexExpression { Object: { } target, Arguments: [var argument] }:
                properties = target;
                index = argument;
                break;
            case MethodCallExpression
            {
                Object: { } target,
                Method.Name: "get_Item",
                Arguments: [var argument],
            }:
                properties = target;
                index = argument;
                break;
            default:
                return false;
        }

        if (StripConvert(properties) is not MemberExpression
            {
                Expression: { } owner,
                Member.Name: nameof(DynamicNode.Properties),
            } || StripConvert(owner) != entity ||
            entity.Type != typeof(DynamicNode) && entity.Type != typeof(DynamicRelationship))
        {
            return false;
        }

        key = Evaluate<string>(index, "dynamic property key");
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new GraphQueryTranslationException("A dynamic property key cannot be empty or whitespace.");
        }

        return true;
    }

    private static bool ValidateMappedProperty(PropertyInfo property)
    {
        if (property.Name is nameof(INode.Labels) or nameof(IRelationship.Type))
        {
            throw new GraphQueryTranslationException(
                $"Structural graph member '{property.Name}' cannot be updated by SetProperty.");
        }

        var domainId = property.Name == nameof(IEntity.Id);
        if (!domainId && property.DeclaringType is { } declaring &&
            (declaring == typeof(IEntity) || declaring == typeof(INode) || declaring == typeof(IRelationship) ||
             declaring == typeof(Node) || declaring == typeof(Relationship) ||
             declaring == typeof(DynamicNode) || declaring == typeof(DynamicRelationship)))
        {
            throw new GraphQueryTranslationException(
                $"Structural graph member '{property.Name}' cannot be updated by SetProperty.");
        }

        var attribute = property.GetCustomAttribute<PropertyAttribute>(inherit: true);
        if (attribute?.Ignore == true)
        {
            throw new GraphQueryTranslationException(
                $"Property '{property.Name}' is ignored and has no mapped storage property.");
        }

        if (GraphDataModel.IsSimple(property.PropertyType) ||
            GraphDataModel.IsCollectionOfSimple(property.PropertyType))
        {
            return false;
        }

        if (GraphDataModel.IsComplex(property.PropertyType) ||
            GraphDataModel.IsCollectionOfComplex(property.PropertyType))
        {
            return true;
        }

        throw new GraphQueryTranslationException(
            $"Property '{property.Name}' has unsupported graph value type '{property.PropertyType}'.");
    }

    private static LambdaExpression ValidateComputedValue(
        LambdaExpression expression,
        Type entityType,
        string storageName)
    {
        if (expression.Parameters.Count != 1 || expression.Parameters[0].Type != entityType)
        {
            throw new GraphQueryTranslationException(
                $"The computed value for '{storageName}' must accept the current entity type '{entityType.FullName}'.");
        }

        if (!GraphDataModel.IsSimple(StripConvert(expression.Body).Type))
        {
            throw new GraphQueryTranslationException(
                $"The computed value for '{storageName}' must return a scalar graph value; provider-translated collection computations are not supported.");
        }

        return expression;
    }

    private static void ValidateConstantValue(
        object? value,
        PropertyInfo? property,
        bool dynamic,
        bool complex,
        Type entityType,
        string storageName)
    {
        if (!complex)
        {
            return;
        }

        if (typeof(IRelationship).IsAssignableFrom(entityType))
        {
            throw new GraphQueryTranslationException(
                $"Complex property '{storageName}' cannot be updated on a graph relationship; " +
                "relationships cannot own complex-property value nodes.");
        }

        if (value is null)
        {
            return;
        }

        var valueType = value.GetType();
        if (!dynamic && property is not null)
        {
            ValidateTypedComplexValue(value, valueType, property, storageName);
        }
        else if (!IsSupportedDynamicComplexType(valueType))
        {
            throw new GraphQueryTranslationException(
                $"The dynamic value for '{storageName}' has unsupported complex runtime type '{valueType}'.");
        }

        GraphDataModel.EnsureComplexPropertyValueDepth(value);
    }

    private static void ValidateTypedComplexValue(
        object value,
        Type valueType,
        PropertyInfo property,
        string storageName)
    {
        if (!GraphDataModel.IsCollectionOfComplex(property.PropertyType))
        {
            if (!property.PropertyType.IsAssignableFrom(valueType))
            {
                throw new GraphQueryTranslationException(
                    $"Complex property '{storageName}' expects '{property.PropertyType}', " +
                    $"but the assigned value has runtime type '{valueType}'.");
            }

            return;
        }

        if (value is not System.Collections.IEnumerable values)
        {
            throw new GraphQueryTranslationException(
                $"Complex collection property '{storageName}' requires an enumerable value.");
        }

        var elementType = GetCollectionElementType(property.PropertyType);
        var index = 0;
        foreach (var item in values)
        {
            if (item is null || !elementType.IsAssignableFrom(item.GetType()))
            {
                throw new GraphQueryTranslationException(
                    $"Complex collection property '{storageName}' contains " +
                    $"{(item is null ? "a null" : $"an incompatible '{item.GetType()}'")} element at index {index}; " +
                    $"the element type is '{elementType}'.");
            }

            index++;
        }
    }

    private static bool IsDynamicComplexValue(Type valueType) =>
        !GraphDataModel.IsSimple(valueType) && !GraphDataModel.IsCollectionOfSimple(valueType);

    private static bool IsSupportedDynamicComplexType(Type valueType)
    {
        if (IsDynamicDictionary(valueType) || GraphDataModel.IsComplex(valueType))
        {
            return true;
        }

        if (GraphDataModel.IsCollectionOfComplex(valueType))
        {
            return true;
        }

        if (valueType == typeof(string) ||
            !typeof(System.Collections.IEnumerable).IsAssignableFrom(valueType) ||
            GraphDataModel.IsDictionary(valueType))
        {
            return false;
        }

        return IsDynamicDictionary(GetCollectionElementType(valueType));
    }

    private static bool IsDynamicDictionary(Type type) =>
        GraphDataModel.IsDictionary(type) &&
        typeof(IEnumerable<KeyValuePair<string, object?>>).IsAssignableFrom(type);

    private static Type GetCollectionElementType(Type type) => type switch
    {
        { IsArray: true } => type.GetElementType()!,
        { IsGenericType: true } => type.GetGenericArguments()[0],
        _ => typeof(object),
    };

    private static MethodInfo? ResolveDefinition(MethodInfo method) =>
        method.IsGenericMethod ? method.GetGenericMethodDefinition() : method;

    private static LambdaExpression RequireLambda(Expression expression, string description) =>
        TryGetLambda(expression) ?? throw new GraphQueryTranslationException($"The {description} must be a lambda expression.");

    private static LambdaExpression? TryGetLambda(Expression expression) => expression switch
    {
        LambdaExpression lambda => lambda,
        UnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression lambda } => lambda,
        _ => null,
    };

    private static T Evaluate<T>(Expression expression, string description)
    {
        expression = StripConvert(expression);
        if (expression is ConstantExpression { Value: T value })
        {
            return value;
        }

        try
        {
            var result = Expression.Lambda(expression).Compile().DynamicInvoke();
            return (T)result!;
        }
        catch (Exception exception) when (exception is not GraphQueryTranslationException)
        {
            throw new GraphQueryTranslationException(
                $"The {description} must be a constant or captured value.", exception);
        }
    }

    private static Expression StripConvert(Expression expression)
    {
        while (expression is UnaryExpression
            { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            expression = unary.Operand;
        }

        return expression;
    }
}
