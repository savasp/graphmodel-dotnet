// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.
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

namespace Cvoya.Graph.Core.Tests;

using System.Reflection;
using System.Reflection.Emit;

public class SchemaRegistryRescanTests
{
    [Fact]
    public async Task GetNodeSchema_RescansLoadedAssembliesWhenLabelIsMissing()
    {
        var registry = new SchemaRegistry();
        var label = $"LateLoadedNode_{Guid.NewGuid():N}";

        await registry.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Null(registry.GetNodeSchema(label));

        var type = CreateLateLoadedNodeType(label);

        var schema = registry.GetNodeSchema(label);

        Assert.NotNull(schema);
        Assert.Same(type, schema.Type);
        Assert.Equal(label, schema.Label);
    }

    private static Type CreateLateLoadedNodeType(string label)
    {
        var assemblyName = new AssemblyName($"Cvoya.Graph.Core.Tests.Dynamic_{Guid.NewGuid():N}");
        var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule($"{assemblyName.Name}.dll");
        var type = module.DefineType(
            $"Cvoya.Graph.Core.Tests.Dynamic.{label}",
            TypeAttributes.Public | TypeAttributes.Class);
        type.AddInterfaceImplementation(typeof(INode));

        var labelConstructor = typeof(NodeAttribute).GetConstructor([typeof(string)])
            ?? throw new InvalidOperationException("NodeAttribute(string) constructor was not found.");

        type.SetCustomAttribute(new CustomAttributeBuilder(labelConstructor, [label]));
        ImplementIdProperty(type);
        ImplementLabelsProperty(type);
        type.DefineDefaultConstructor(MethodAttributes.Public);

        return type.CreateType()
            ?? throw new InvalidOperationException("Dynamic node type was not created.");
    }

    private static void ImplementIdProperty(TypeBuilder type)
    {
        var idField = type.DefineField("_id", typeof(string), FieldAttributes.Private);
        var property = type.DefineProperty(nameof(IEntity.Id), PropertyAttributes.None, typeof(string), null);

        var getter = type.DefineMethod(
            "get_Id",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(string),
            Type.EmptyTypes);
        var getterIl = getter.GetILGenerator();
        getterIl.Emit(OpCodes.Ldarg_0);
        getterIl.Emit(OpCodes.Ldfld, idField);
        getterIl.Emit(OpCodes.Ret);

        var setter = type.DefineMethod(
            "set_Id",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            CallingConventions.HasThis,
            typeof(void),
            [typeof(System.Runtime.CompilerServices.IsExternalInit)],
            null,
            [typeof(string)],
            null,
            null);
        var setterIl = setter.GetILGenerator();
        setterIl.Emit(OpCodes.Ldarg_0);
        setterIl.Emit(OpCodes.Ldarg_1);
        setterIl.Emit(OpCodes.Stfld, idField);
        setterIl.Emit(OpCodes.Ret);

        property.SetGetMethod(getter);
        property.SetSetMethod(setter);

        var interfaceProperty = typeof(IEntity).GetProperty(nameof(IEntity.Id))!;
        type.DefineMethodOverride(getter, interfaceProperty.GetMethod!);
        type.DefineMethodOverride(setter, interfaceProperty.SetMethod!);
    }

    private static void ImplementLabelsProperty(TypeBuilder type)
    {
        var labelsField = type.DefineField("_labels", typeof(IReadOnlyList<string>), FieldAttributes.Private);
        var property = type.DefineProperty(nameof(INode.Labels), PropertyAttributes.None, typeof(IReadOnlyList<string>), null);

        var getter = type.DefineMethod(
            "get_Labels",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(IReadOnlyList<string>),
            Type.EmptyTypes);
        var getterIl = getter.GetILGenerator();
        getterIl.Emit(OpCodes.Ldarg_0);
        getterIl.Emit(OpCodes.Ldfld, labelsField);
        getterIl.Emit(OpCodes.Ret);

        property.SetGetMethod(getter);
        type.DefineMethodOverride(getter, typeof(INode).GetProperty(nameof(INode.Labels))!.GetMethod!);
    }
}
