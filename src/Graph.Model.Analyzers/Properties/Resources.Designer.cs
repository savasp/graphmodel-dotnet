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

namespace Cvoya.Graph.Model.Analyzers.Properties;

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
                var temp = new global::System.Resources.ResourceManager("Cvoya.Graph.Model.Analyzers.Properties.Resources", typeof(Resources).Assembly);
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
    internal static string GM001_Description => ResourceManager.GetString("GM001_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Type '{0}' implementing {1} must have a parameterless constructor or constructors that initialize all properties.
    /// </summary>
    internal static string GM001_MessageFormat => ResourceManager.GetString("GM001_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Missing parameterless constructor or constructor that initializes properties.
    /// </summary>
    internal static string GM001_Title => ResourceManager.GetString("GM001_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Properties in INode and IRelationship implementations must have public getters and either public setters or public property initializers..
    /// </summary>
    internal static string GM002_Description => ResourceManager.GetString("GM002_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Property '{0}' in type '{1}' must have public getter and either public setter or public init accessor.
    /// </summary>
    internal static string GM002_MessageFormat => ResourceManager.GetString("GM002_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Property must have public getters and setters or initializers.
    /// </summary>
    internal static string GM002_Title => ResourceManager.GetString("GM002_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Properties of types implementing INode or IRelationship cannot be INode or IRelationship or collections of them..
    /// </summary>
    internal static string GM003_Description => ResourceManager.GetString("GM003_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Property '{0}' in type '{1}' cannot be of type '{2}' which implements INode or IRelationship.
    /// </summary>
    internal static string GM003_MessageFormat => ResourceManager.GetString("GM003_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Property cannot be INode or IRelationship type.
    /// </summary>
    internal static string GM003_Title => ResourceManager.GetString("GM003_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Properties of INode implementations must be simple types, complex types, collections of simple types, or collections of complex types, applied recursively..
    /// </summary>
    internal static string GM004_Description => ResourceManager.GetString("GM004_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Property '{0}' in INode type '{1}' has invalid type '{2}'. Properties must be simple types, complex types, collections of simple types, or collections of complex types.
    /// </summary>
    internal static string GM004_MessageFormat => ResourceManager.GetString("GM004_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Invalid property type for INode implementation.
    /// </summary>
    internal static string GM004_Title => ResourceManager.GetString("GM004_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Properties of IRelationship implementations must be simple types or collections of simple types..
    /// </summary>
    internal static string GM005_Description => ResourceManager.GetString("GM005_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Property '{0}' in IRelationship type '{1}' has invalid type '{2}'. Properties must be simple types or collections of simple types.
    /// </summary>
    internal static string GM005_MessageFormat => ResourceManager.GetString("GM005_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Invalid property type for IRelationship implementation.
    /// </summary>
    internal static string GM005_Title => ResourceManager.GetString("GM005_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Properties of complex properties cannot be INode or IRelationship or collections of them. This rule is applied recursively..
    /// </summary>
    internal static string GM006_Description => ResourceManager.GetString("GM006_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Complex type '{0}' used by property '{1}' in type '{2}' contains properties of INode or IRelationship types.
    /// </summary>
    internal static string GM006_MessageFormat => ResourceManager.GetString("GM006_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Complex type property contains graph interface types.
    /// </summary>
    internal static string GM006_Title => ResourceManager.GetString("GM006_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to A type hierarchy cannot have PropertyAttribute annotations with the same Label value across all properties in that type hierarchy..
    /// </summary>
    internal static string GM007_Description => ResourceManager.GetString("GM007_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Property '{0}' in type '{1}' uses PropertyAttribute label '{2}' which is already used by property '{3}' in base type '{4}'.
    /// </summary>
    internal static string GM007_MessageFormat => ResourceManager.GetString("GM007_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Duplicate PropertyAttribute label in type hierarchy.
    /// </summary>
    internal static string GM007_Title => ResourceManager.GetString("GM007_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to A type hierarchy cannot have RelationshipAttribute annotations with the same Label value across all types in that type hierarchy..
    /// </summary>
    internal static string GM008_Description => ResourceManager.GetString("GM008_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Type '{0}' uses RelationshipAttribute label '{1}' which is already used by base type '{2}'.
    /// </summary>
    internal static string GM008_MessageFormat => ResourceManager.GetString("GM008_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Duplicate RelationshipAttribute label in type hierarchy.
    /// </summary>
    internal static string GM008_Title => ResourceManager.GetString("GM008_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to A type hierarchy cannot have NodeAttribute annotations with the same Label value across all types in that type hierarchy..
    /// </summary>
    internal static string GM009_Description => ResourceManager.GetString("GM009_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Type '{0}' uses NodeAttribute label '{1}' which is already used by base type '{2}'.
    /// </summary>
    internal static string GM009_MessageFormat => ResourceManager.GetString("GM009_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Duplicate NodeAttribute label in type hierarchy.
    /// </summary>
    internal static string GM009_Title => ResourceManager.GetString("GM009_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to A type implementing INode or IRelationship cannot contain a type reference cycle without a nullable type..
    /// </summary>
    internal static string GM010_Description => ResourceManager.GetString("GM010_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Property '{0}' in type '{1}' creates a circular reference to type '{2}' without using a nullable type.
    /// </summary>
    internal static string GM010_MessageFormat => ResourceManager.GetString("GM010_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Circular reference without nullable type.
    /// </summary>
    internal static string GM010_Title => ResourceManager.GetString("GM010_Title", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Types should inherit from Node or Relationship base classes instead of implementing INode or IRelationship directly. The base classes provide default implementations for runtime metadata properties like Labels and Type, which are managed by the graph provider..
    /// </summary>
    internal static string GM011_Description => ResourceManager.GetString("GM011_Description", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Type '{0}' should inherit from '{1}' instead of implementing '{2}' directly. The base class provides default implementations for runtime metadata properties.
    /// </summary>
    internal static string GM011_MessageFormat => ResourceManager.GetString("GM011_MessageFormat", resourceCulture)!;

    /// <summary>
    ///   Looks up a localized string similar to Type should inherit from base class instead of implementing interface directly.
    /// </summary>
    internal static string GM011_Title => ResourceManager.GetString("GM011_Title", resourceCulture)!;
}
