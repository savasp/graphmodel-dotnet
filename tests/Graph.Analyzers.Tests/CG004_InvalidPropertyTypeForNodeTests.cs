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
            
            public class TestNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public Point Location { get; set; } = new();
            }
            """;

        await VerifyAnalyzerAsync(test);
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
