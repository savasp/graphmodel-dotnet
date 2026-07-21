// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;


using static Cvoya.Graph.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;

public class CG004_InvalidPropertyTypeForNodeTests
{
    [Fact]
    public async Task ValidNodeWithSimpleTypes_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System;
            using System.Collections.Generic;
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
                public int Age { get; set; }
                public bool IsActive { get; set; }
                public DateTime CreatedAt { get; set; }
                public Guid UniqueId { get; set; }
                public decimal Price { get; set; }
                public double Score { get; set; }
                public float Rating { get; set; }
                public byte Status { get; set; }
                public char Category { get; set; }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValidNodeWithCollectionsOfSimpleTypes_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public List<string> Tags { get; set; } = new();
                public string[] Categories { get; set; } = Array.Empty<string>();
                public HashSet<int> Numbers { get; set; } = new();
                public IEnumerable<bool> Flags { get; set; } = Enumerable.Empty<bool>();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValidNodeWithComplexTypes_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;
            
            public class Address
            {
                public string Street { get; set; } = string.Empty;
                public string City { get; set; } = string.Empty;
                public int ZipCode { get; set; }
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public Address Location { get; set; } = new();
                public List<Address> PreviousAddresses { get; set; } = new();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValidNodeWithEnums_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;
            
            public enum Status
            {
                Active,
                Inactive,
                Pending
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public Status CurrentStatus { get; set; }
                public List<Status> StatusHistory { get; set; } = new();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeWithUnsupportedType_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Threading.Tasks;
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public Task {|#0:AsyncOperation|} { get; set; } = null!;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG004")
            .WithLocation(0)
            .WithArguments("AsyncOperation", "TestNode", "Task");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithDelegate_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System;
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public Action {|#0:Callback|} { get; set; } = null!;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG004")
            .WithLocation(0)
            .WithArguments("Callback", "TestNode", "Action");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithUnsupportedCollection_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public List<Task> {|#0:Tasks|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG004")
            .WithLocation(0)
            .WithArguments("Tasks", "TestNode", "List<Task>");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithNonConstructibleSimpleCollection_ProducesDiagnostic()
    {
        // Queue<int> is a collection of simple values, but the serializer can only construct arrays,
        // List<T>/list interfaces, and HashSet<T>/set interfaces - so it is rejected rather than
        // silently generating source that fails to compile (#362).
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;

            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public Queue<int> {|#0:Pending|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG004")
            .WithLocation(0)
            .WithArguments("Pending", "TestNode", "Queue<Int32>");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithConcreteSetOtherThanHashSet_ProducesDiagnostic()
    {
        // SortedSet<int> implements ISet<int>, but a generated HashSet<int> is not assignable to it,
        // so the concrete non-HashSet set type is not constructible and must be rejected.
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;

            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public SortedSet<int> {|#0:Ranks|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG004")
            .WithLocation(0)
            .WithArguments("Ranks", "TestNode", "SortedSet<Int32>");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithNonConstructibleComplexCollection_ProducesDiagnostic()
    {
        // The element type is a valid complex type, but Queue<T> is not a constructible collection
        // shape - the property is rejected on the collection shape alone.
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;

            public class Address
            {
                public string Street { get; set; } = string.Empty;
            }

            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public Queue<Address> {|#0:History|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG004")
            .WithLocation(0)
            .WithArguments("History", "TestNode", "Queue<Address>");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithComplexTypeContainingNonConstructibleCollection_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;

            public class Address
            {
                public Queue<int> Pending { get; set; } = new();
            }

            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public Address {|#0:Location|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG004")
            .WithLocation(0)
            .WithArguments("Location", "TestNode", "Address");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithMultidimensionalArray_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;

            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public int[,] {|#0:Grid|} { get; set; } = new int[0, 0];
            }
            """;

        var expected = VerifyCS.Diagnostic("CG004")
            .WithLocation(0)
            .WithArguments("Grid", "TestNode", "Int32[,]");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithFrameworkCollectionLookalike_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;

            namespace System.Collections.Generic
            {
                public interface ISet<TItem, TTag> : global::System.Collections.Generic.IEnumerable<TItem>
                {
                }
            }

            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public System.Collections.Generic.ISet<int, string> {|#0:Values|} { get; set; } = null!;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG004")
            .WithLocation(0)
            .WithArguments("Values", "TestNode", "ISet<Int32, String>");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithUnconstructibleReadonlyStruct_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;

            public readonly struct Address
            {
                public string Street { get; }
            }

            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public Address {|#0:Location|} { get; set; }
            }
            """;

        var expected = VerifyCS.Diagnostic("CG004")
            .WithLocation(0)
            .WithArguments("Location", "TestNode", "Address");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithConstructorBoundStruct_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;

            public readonly struct Address
            {
                public Address(string street) => Street = street;
                public string Street { get; }
            }

            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public Address Location { get; set; }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeWithWrongTypedStructConstructor_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;

            public readonly struct Address
            {
                public Address(int street) => Street = street.ToString();
                public string Street { get; }
            }

            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public Address {|#0:Location|} { get; set; }
            }
            """;

        var expected = VerifyCS.Diagnostic("CG004")
            .WithLocation(0)
            .WithArguments("Location", "TestNode", "Address");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithComplexTypeContainingGraphInterface_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            public class InvalidAddress
            {
                public string Street { get; set; } = string.Empty;
                public INode OwnerNode { get; set; } = null!;
            }
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public InvalidAddress {|#0:Location|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG006")  // This would be caught by CG006, not CG004
            .WithLocation(0)
            .WithArguments("InvalidAddress", "Location", "TestNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithNullableTypes_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System;
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public int? Age { get; set; }
                public DateTime? LastLogin { get; set; }
                public bool? IsVerified { get; set; }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeWithPointType_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;

            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public Point Location { get; set; } = new();
                public Point? MaybeLocation { get; set; }
                public List<Point> Waypoints { get; set; } = new();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeWithSystemDrawingPointType_ProducesDiagnostics()
    {
        // Only Cvoya.Graph.Point is a supported spatial simple type. System.Drawing.Point has no
        // runtime, codegen, or provider support, so the analyzer must reject it instead of promising
        // a storage type the rest of the stack cannot serialize (#387).
        var test = """
            using Cvoya.Graph;
            using System.Collections.Generic;

            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public System.Drawing.Point {|#0:Location|} { get; set; }
                public System.Drawing.Point? {|#1:MaybeLocation|} { get; set; }
                public List<System.Drawing.Point> {|#2:Locations|} { get; set; } = new();
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG004").WithLocation(0).WithArguments("Location", "TestNode", "System.Drawing.Point"),
            VerifyCS.Diagnostic("CG004").WithLocation(1).WithArguments("MaybeLocation", "TestNode", "System.Drawing.Point?"),
            VerifyCS.Diagnostic("CG004").WithLocation(2).WithArguments("Locations", "TestNode", "List<System.Drawing.Point>"),
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithNativeSizedIntegerTypes_ProducesDiagnostics()
    {
        var test = """
            using System;
            using System.Collections.Generic;
            using Cvoya.Graph;

            public class TestNode : Node
            {
                public IntPtr {|#0:Pointer|} { get; set; }
                public UIntPtr? {|#1:OptionalPointer|} { get; set; }
                public List<nint> {|#2:Pointers|} { get; set; } = new();
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG004").WithLocation(0).WithArguments("Pointer", "TestNode", "IntPtr"),
            VerifyCS.Diagnostic("CG004").WithLocation(1).WithArguments("OptionalPointer", "TestNode", "UIntPtr?"),
            VerifyCS.Diagnostic("CG004").WithLocation(2).WithArguments("Pointers", "TestNode", "List<IntPtr>"),
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithComplexTypeContainingNativeSizedInteger_ProducesDiagnostics()
    {
        var test = """
            using System;
            using System.Collections.Generic;
            using Cvoya.Graph;

            public class NativeHandleHolder
            {
                public IntPtr Handle { get; set; }
            }

            public class PointerListHolder
            {
                public List<nuint> Pointers { get; set; } = new();
            }

            public class TestNode : Node
            {
                public NativeHandleHolder {|#0:Holder|} { get; set; } = new();
                public List<PointerListHolder> {|#1:Holders|} { get; set; } = new();
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG004").WithLocation(0).WithArguments("Holder", "TestNode", "NativeHandleHolder"),
            VerifyCS.Diagnostic("CG004").WithLocation(1).WithArguments("Holders", "TestNode", "List<PointerListHolder>"),
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Theory]
    [InlineData("System.Threading.Tasks.Task")]
    [InlineData("System.Threading.Tasks.ValueTask")]
    [InlineData("System.Action")]
    [InlineData("System.Collections.Generic.Dictionary<string, string>")]
    [InlineData("System.Collections.IDictionary")]
    [InlineData("System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, string>>")]
    [InlineData("System.Collections.Generic.List<System.Collections.Generic.List<System.Threading.Tasks.Task>>")]
    [InlineData("System.IO.Stream")]
    [InlineData("System.Net.IPAddress")]
    [InlineData("System.Reflection.MemberInfo")]
    [InlineData("System.Runtime.InteropServices.GCHandle")]
    public async Task NodeWithComplexTypeContainingUnsupportedShape_ProducesDiagnostic(string propertyType)
    {
        var test = $$"""
            using Cvoya.Graph;

            public sealed class UnsupportedHolder
            {
                public {{propertyType}} Unsupported { get; set; }
            }

            public sealed class TestNode : Node
            {
                public UnsupportedHolder {|#0:Holder|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG004")
            .WithLocation(0)
            .WithArguments("Holder", "TestNode", "UnsupportedHolder");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithComplexTypeContainingInheritedUnsupportedMember_ProducesDiagnostic()
    {
        var test = """
            using System.Threading.Tasks;
            using Cvoya.Graph;

            public abstract class UnsupportedHolderBase
            {
                public Task Unsupported { get; set; } = null!;
            }

            public sealed class DerivedUnsupportedHolder : UnsupportedHolderBase
            {
                public string Name { get; set; } = string.Empty;
            }

            public sealed class TestNode : Node
            {
                public DerivedUnsupportedHolder {|#0:Holder|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("CG004")
            .WithLocation(0)
            .WithArguments("Holder", "TestNode", "DerivedUnsupportedHolder");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithComplexTypeContainingIgnoredNativeSizedInteger_ProducesNoDiagnostics()
    {
        var test = """
            using System;
            using Cvoya.Graph;

            public class NativeHandleHolder
            {
                [Property(Ignore = true)]
                public IntPtr Handle { get; set; }

                public string Name { get; set; } = string.Empty;
            }

            public class TestNode : Node
            {
                public NativeHandleHolder Holder { get; set; } = new();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeWithRecursiveComplexTypeAndExcludedUnsupportedMembers_ProducesNoDiagnostics()
    {
        var test = """
            #nullable enable
            using System.Threading.Tasks;
            using Cvoya.Graph;

            public abstract class SupportedHolderBase
            {
                public string Name { get; set; } = string.Empty;
                public Task Hidden { get; set; } = null!;
            }

            public sealed class RecursiveSupportedHolder : SupportedHolderBase
            {
                public RecursiveSupportedHolder? Next { get; set; }

                [Property(Ignore = true)]
                public Task Ignored { get; set; } = null!;

                [Property(Ignore = true)]
                public new Task Hidden { get; set; } = null!;

                public static Task Static { get; } = Task.CompletedTask;

                public Task this[int index] => Task.CompletedTask;
            }

            public sealed class TestNode : Node
            {
                public RecursiveSupportedHolder Holder { get; set; } = new();
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeWithNonSerializedUnsupportedProperties_ProducesNoDiagnostics()
    {
        // The same exclusions the nested walk applies also apply to the entity's own declarations:
        // ignored properties, static properties, and indexers are never serialized, so an
        // unsupported type on one of them is not a model error.
        var test = """
            using System.Threading.Tasks;
            using Cvoya.Graph;

            public sealed class TestNode : Node
            {
                [Property(Ignore = true)]
                public Task Ignored { get; set; } = null!;

                public static Task Shared { get; set; } = Task.CompletedTask;

                public Task this[int index]
                {
                    get => Task.CompletedTask;
                    set { }
                }
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeWithInheritedUnsupportedProperty_ProducesDiagnosticOnDeclaration()
    {
        // The effective member set is the full inheritance chain, so the base declaration is
        // reported once for the base entity and once for the derived one that inherits it.
        var test = """
            using System.Threading.Tasks;
            using Cvoya.Graph;

            public abstract class UnsupportedBaseNode : Node
            {
                public Task {|#0:Pending|} { get; set; } = null!;
            }

            public sealed class TestNode : UnsupportedBaseNode
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(
            test,
            VerifyCS.Diagnostic("CG004").WithLocation(0).WithArguments("Pending", "UnsupportedBaseNode", "Task"),
            VerifyCS.Diagnostic("CG004").WithLocation(0).WithArguments("Pending", "TestNode", "Task"));
    }

    [Fact]
    public async Task NodeWithMultipleInvalidProperties_ProducesMultipleDiagnostics()
    {
        var test = """
            using Cvoya.Graph;
            using System;
            using System.Threading.Tasks;
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public Task {|#0:Operation1|} { get; set; } = null!;
                public Action {|#1:Callback|} { get; set; } = null!;
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG004").WithLocation(0).WithArguments("Operation1", "TestNode", "Task"),
            VerifyCS.Diagnostic("CG004").WithLocation(1).WithArguments("Callback", "TestNode", "Action")
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipClassIgnored_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System.Threading.Tasks;
            
            public class TestRelationship : Relationship
            {
                // This would be invalid for IRelationship and is caught by CG005 rule
                public Task {|#0:AsyncOperation|} { get; set; } = null!;
            }
            """;

        // This test ensures CG004 only applies to INode implementations
        // The Task property is caught by CG005 rule for relationships
        var expected = new[]
        {
            VerifyCS.Diagnostic("CG005")
                .WithLocation(0)
                .WithArguments("AsyncOperation", "TestRelationship", "Task"),
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task InheritedNodeWithInvalidProperty_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            using System;
            
            public class BaseNode : Node
            {
                public string Id { get; init; } = string.Empty;
            }
            
            public class DerivedNode : BaseNode
            {
                public Action {|#0:Callback|} { get; set; } = null!;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG004")
            .WithLocation(0)
            .WithArguments("Callback", "DerivedNode", "Action");

        await VerifyAnalyzerAsync(test, expected);
    }
}

// Helper typedef for cleaner syntax
file static class VerifyCS
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new DiagnosticResult(diagnosticId, DiagnosticSeverity.Error);
}
