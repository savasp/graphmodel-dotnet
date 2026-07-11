// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers.Properties;

using System;


#nullable enable
/// <summary>
///   A strongly-typed resource class, for looking up localized strings, etc.
/// </summary>
[global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
[global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
internal class Resources
{
    private static global::System.Resources.ResourceManager? resourceMan;

    private static global::System.Globalization.CultureInfo? resourceCulture;

    [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
    internal Resources()
    {
    }

    /// <summary>
    ///   Returns the cached ResourceManager instance used by this class.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
    internal static global::System.Resources.ResourceManager ResourceManager
    {
        get
        {
            if (resourceMan is null)
            {
                var temp = new global::System.Resources.ResourceManager("Cvoya.Graph.Analyzers.Properties.Resources", typeof(Resources).Assembly);
                resourceMan = temp;
            }
            return resourceMan;
        }
    }

    /// <summary>
    ///   Overrides the current thread's CurrentUICulture property for all
    ///   resource lookups using this strongly typed resource class.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
    internal static global::System.Globalization.CultureInfo? Culture
    {
        get => resourceCulture;
        set => resourceCulture = value;
    }

    /// <summary>
    ///   Looks up a localized string similar to Types implementing INode or IRelationship must have a parameterless constructor or constructors that initialize their (get/set) properties..
    /// </summary>
    internal static string CG001_Description => ResourceManager.GetString("CG001_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Type '{0}' implementing {1} must have a parameterless constructor or constructors that initialize all properties.
    /// </summary>
    internal static string CG001_MessageFormat => ResourceManager.GetString("CG001_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Missing parameterless constructor or constructor that initializes properties.
    /// </summary>
    internal static string CG001_Title => ResourceManager.GetString("CG001_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Properties in INode and IRelationship implementations must have public getters and either public setters or public property initializers..
    /// </summary>
    internal static string CG002_Description => ResourceManager.GetString("CG002_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Property '{0}' in type '{1}' must have public getter and either public setter or public init accessor.
    /// </summary>
    internal static string CG002_MessageFormat => ResourceManager.GetString("CG002_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Property must have public getters and setters or initializers.
    /// </summary>
    internal static string CG002_Title => ResourceManager.GetString("CG002_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Properties of types implementing INode or IRelationship cannot be INode or IRelationship or collections of them..
    /// </summary>
    internal static string CG003_Description => ResourceManager.GetString("CG003_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Property '{0}' in type '{1}' cannot be of type '{2}' which implements INode or IRelationship.
    /// </summary>
    internal static string CG003_MessageFormat => ResourceManager.GetString("CG003_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Property cannot be INode or IRelationship type.
    /// </summary>
    internal static string CG003_Title => ResourceManager.GetString("CG003_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Properties of INode implementations must be simple types, complex types, collections of simple types, or collections of complex types, applied recursively..
    /// </summary>
    internal static string CG004_Description => ResourceManager.GetString("CG004_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Property '{0}' in INode type '{1}' has invalid type '{2}'. Properties must be simple types, complex types, collections of simple types, or collections of complex types.
    /// </summary>
    internal static string CG004_MessageFormat => ResourceManager.GetString("CG004_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Invalid property type for INode implementation.
    /// </summary>
    internal static string CG004_Title => ResourceManager.GetString("CG004_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Properties of IRelationship implementations must be simple types or collections of simple types..
    /// </summary>
    internal static string CG005_Description => ResourceManager.GetString("CG005_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Property '{0}' in IRelationship type '{1}' has invalid type '{2}'. Properties must be simple types or collections of simple types.
    /// </summary>
    internal static string CG005_MessageFormat => ResourceManager.GetString("CG005_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Invalid property type for IRelationship implementation.
    /// </summary>
    internal static string CG005_Title => ResourceManager.GetString("CG005_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Properties of complex properties cannot be INode or IRelationship or collections of them. This rule is applied recursively..
    /// </summary>
    internal static string CG006_Description => ResourceManager.GetString("CG006_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Complex type '{0}' used by property '{1}' in type '{2}' contains properties of INode or IRelationship types.
    /// </summary>
    internal static string CG006_MessageFormat => ResourceManager.GetString("CG006_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Complex type property contains graph interface types.
    /// </summary>
    internal static string CG006_Title => ResourceManager.GetString("CG006_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to A type hierarchy cannot have PropertyAttribute annotations with the same Label value across all properties in that type hierarchy..
    /// </summary>
    internal static string CG007_Description => ResourceManager.GetString("CG007_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Property '{0}' in type '{1}' uses PropertyAttribute label '{2}' which is already used by property '{3}' in base type '{4}'.
    /// </summary>
    internal static string CG007_MessageFormat => ResourceManager.GetString("CG007_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Duplicate PropertyAttribute label in type hierarchy.
    /// </summary>
    internal static string CG007_Title => ResourceManager.GetString("CG007_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to A type hierarchy cannot have RelationshipAttribute annotations with the same Label value across all types in that type hierarchy..
    /// </summary>
    internal static string CG008_Description => ResourceManager.GetString("CG008_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Type '{0}' uses RelationshipAttribute label '{1}' which is already used by base type '{2}'.
    /// </summary>
    internal static string CG008_MessageFormat => ResourceManager.GetString("CG008_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Duplicate RelationshipAttribute label in type hierarchy.
    /// </summary>
    internal static string CG008_Title => ResourceManager.GetString("CG008_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to A type hierarchy cannot have NodeAttribute annotations with the same Label value across all types in that type hierarchy..
    /// </summary>
    internal static string CG009_Description => ResourceManager.GetString("CG009_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Type '{0}' uses NodeAttribute label '{1}' which is already used by base type '{2}'.
    /// </summary>
    internal static string CG009_MessageFormat => ResourceManager.GetString("CG009_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Duplicate NodeAttribute label in type hierarchy.
    /// </summary>
    internal static string CG009_Title => ResourceManager.GetString("CG009_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to A type implementing INode or IRelationship cannot contain a type reference cycle without a nullable type..
    /// </summary>
    internal static string CG010_Description => ResourceManager.GetString("CG010_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Property '{0}' in type '{1}' creates a circular reference to type '{2}' without using a nullable type.
    /// </summary>
    internal static string CG010_MessageFormat => ResourceManager.GetString("CG010_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Circular reference without nullable type.
    /// </summary>
    internal static string CG010_Title => ResourceManager.GetString("CG010_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Types should inherit from Node or Relationship base classes instead of implementing INode or IRelationship directly. The base classes provide default implementations for runtime metadata properties like Labels and Type, which are managed by the graph provider..
    /// </summary>
    internal static string CG011_Description => ResourceManager.GetString("CG011_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Type '{0}' should inherit from '{1}' instead of implementing '{2}' directly. The base class provides default implementations for runtime metadata properties.
    /// </summary>
    internal static string CG011_MessageFormat => ResourceManager.GetString("CG011_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Type should inherit from base class instead of implementing interface directly.
    /// </summary>
    internal static string CG011_Title => ResourceManager.GetString("CG011_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to NodeAttribute only affects types implementing INode, and RelationshipAttribute only affects types implementing IRelationship. Applying either attribute to a type that does not implement the matching interface is a silent no-op..
    /// </summary>
    internal static string CG012_Description => ResourceManager.GetString("CG012_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Type '{0}' is annotated with [{1}] but does not implement {2}; the attribute has no effect.
    /// </summary>
    internal static string CG012_MessageFormat => ResourceManager.GetString("CG012_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Attribute has no effect on a type that doesn't implement the matching interface.
    /// </summary>
    internal static string CG012_Title => ResourceManager.GetString("CG012_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to A type cannot be annotated with both NodeAttribute and RelationshipAttribute. Label resolution only honors one of the two, silently ignoring the other, so applying both is always a mistake..
    /// </summary>
    internal static string CG013_Description => ResourceManager.GetString("CG013_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Type '{0}' has both [Node] and [Relationship] attributes; a type can only be one or the other.
    /// </summary>
    internal static string CG013_MessageFormat => ResourceManager.GetString("CG013_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Type cannot have both NodeAttribute and RelationshipAttribute.
    /// </summary>
    internal static string CG013_Title => ResourceManager.GetString("CG013_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Types implementing INode or IRelationship must be reference types. Struct value semantics (copy on assignment/parameter-pass) turn load-mutate-update patterns into silent lost-update bugs, and the covariant graph query surface relies on reference-type variance conversions that do not apply to value types..
    /// </summary>
    internal static string CG014_Description => ResourceManager.GetString("CG014_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to {0} '{1}' implements {2}; graph entity types must be reference types (class or record class).
    /// </summary>
    internal static string CG014_MessageFormat => ResourceManager.GetString("CG014_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Graph entity types must be reference types.
    /// </summary>
    internal static string CG014_Title => ResourceManager.GetString("CG014_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to ComplexPropertyAttribute only configures graph-backed complex properties. It has no effect on simple values or simple collections, and an empty RelationshipType override silently falls back to the property name..
    /// </summary>
    internal static string CG015_Description => ResourceManager.GetString("CG015_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to [ComplexProperty] on property '{0}' has no effect: {1}.
    /// </summary>
    internal static string CG015_MessageFormat => ResourceManager.GetString("CG015_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to ComplexPropertyAttribute has no effect.
    /// </summary>
    internal static string CG015_Title => ResourceManager.GetString("CG015_Title", resourceCulture)!;
}
