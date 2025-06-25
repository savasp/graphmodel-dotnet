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

namespace Cvoya.Graph.Model.Analyzers.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Cvoya.Graph.Model.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;

public class GM004_InvalidPropertyTypeForNodeTests
{
    [Fact]
    public async Task ValidNodeWithSimpleTypes_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using System;
            using System.Collections.Generic;
            
            public class TestNode : INode
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
            using Cvoya.Graph.Model;
            using System.Collections.Generic;
            
            public class TestNode : INode
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
            using Cvoya.Graph.Model;
            using System.Collections.Generic;
            
            public class Address
            {
                public string Street { get; set; } = string.Empty;
                public string City { get; set; } = string.Empty;
                public int ZipCode { get; set; }
            }
            
            public class TestNode : INode
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
            using Cvoya.Graph.Model;
            using System.Collections.Generic;
            
            public enum Status
            {
                Active,
                Inactive,
                Pending
            }
            
            public class TestNode : INode
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
            using Cvoya.Graph.Model;
            using System.Threading.Tasks;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public Task {|#0:AsyncOperation|} { get; set; } = null!;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM004")
            .WithLocation(0)
            .WithArguments("AsyncOperation", "TestNode", "Task");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithDelegate_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using System;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public Action {|#0:Callback|} { get; set; } = null!;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM004")
            .WithLocation(0)
            .WithArguments("Callback", "TestNode", "Action");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithUnsupportedCollection_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public List<Task> {|#0:Tasks|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("GM004")
            .WithLocation(0)
            .WithArguments("Tasks", "TestNode", "List<Task>");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithComplexTypeContainingGraphInterface_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            public class InvalidAddress
            {
                public string Street { get; set; } = string.Empty;
                public INode OwnerNode { get; set; } = null!;
            }
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public InvalidAddress {|#0:Location|} { get; set; } = new();
            }
            """;

        var expected = VerifyCS.Diagnostic("GM006")  // This would be caught by GM006, not GM004
            .WithLocation(0)
            .WithArguments("InvalidAddress", "Location", "TestNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithNullableTypes_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using System;
            
            public class TestNode : INode
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
            using Cvoya.Graph.Model;
            
            public class TestNode : INode
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
            using Cvoya.Graph.Model;
            using System;
            using System.Threading.Tasks;
            
            public class TestNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public Task {|#0:Operation1|} { get; set; } = null!;
                public Action {|#1:Callback|} { get; set; } = null!;
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("GM004").WithLocation(0).WithArguments("Operation1", "TestNode", "Task"),
            VerifyCS.Diagnostic("GM004").WithLocation(1).WithArguments("Callback", "TestNode", "Action")
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipClassIgnored_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using System.Threading.Tasks;
            
            public class TestRelationship : IRelationship
            {
                public string Id { get; init; } = string.Empty;
                public RelationshipDirection Direction { get; init; }
                public string StartNodeId { get; init; } = string.Empty;
                public string EndNodeId { get; init; } = string.Empty;
                // This would be invalid for IRelationship and is caught by GM005 rule
                public Task {|#0:AsyncOperation|} { get; set; } = null!;
            }
            """;

        // This test ensures GM004 only applies to INode implementations
        // The Task property is caught by GM005 rule for relationships
        var expected = VerifyCS.Diagnostic("GM005")
            .WithLocation(0)
            .WithArguments("AsyncOperation", "TestRelationship", "Task");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task InheritedNodeWithInvalidProperty_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            using System;
            
            public class BaseNode : INode
            {
                public string Id { get; init; } = string.Empty;
            }
            
            public class DerivedNode : BaseNode
            {
                public Action {|#0:Callback|} { get; set; } = null!;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM004")
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