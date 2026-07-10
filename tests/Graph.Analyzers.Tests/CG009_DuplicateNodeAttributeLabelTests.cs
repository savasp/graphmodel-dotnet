// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Analyzers.Tests;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;


using static Cvoya.Graph.Analyzers.Tests.TestHelpers.AnalyzerTestHelpers;

public class CG009_DuplicateNodeAttributeLabelTests
{
    [Fact]
    public async Task ValidNodeWithUniqueLabels_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            [Node("Person")]
            public class PersonNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            [Node("Company")]
            public class CompanyNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public string CompanyName { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeWithDuplicateLabels_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            [Node("Person")]
            public class PersonNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            [Node("Person")]
            public class {|#0:DuplicatePersonNode|} : Node
            {
                public string Id { get; init; } = string.Empty;
                public string FullName { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG009")
            .WithLocation(0)
            .WithArguments("DuplicatePersonNode", "Person", "PersonNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task InheritedNodeWithDuplicateLabel_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            [Node("Person")]
            public class BasePersonNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            [Node("Person")]
            public class {|#0:DerivedPersonNode|} : BasePersonNode
            {
                public string Title { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG009")
            .WithLocation(0)
            .WithArguments("DerivedPersonNode", "Person", "BasePersonNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeInheritingLabelFromParent_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            [Node("Person")]
            public class BasePersonNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            // This node inherits the label from parent without specifying its own
            public class DerivedPersonNode : BasePersonNode
            {
                public string Title { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeWithCaseSensitiveDuplicates_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            [Node("Person")]
            public class PersonNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            [Node("person")]
            public class {|#0:LowercasePersonNode|} : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            """;

        // Assuming the analyzer treats node labels as case-insensitive
        var expected = VerifyCS.Diagnostic("CG009")
            .WithLocation(0)
            .WithArguments("LowercasePersonNode", "person", "PersonNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexInheritanceHierarchyWithDuplicates_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            [Node("Entity")]
            public class BaseEntityNode : Node
            {
                public string Id { get; init; } = string.Empty;
            }
            
            [Node("Person")]
            public class PersonNode : BaseEntityNode
            {
                public string Name { get; set; } = string.Empty;
            }
            
            [Node("Entity")]
            public class {|#0:DuplicateEntityNode|} : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Type { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("CG009")
            .WithLocation(0)
            .WithArguments("DuplicateEntityNode", "Entity", "BaseEntityNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithoutAttribute_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            [Node("Person")]
            public class PersonNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            // Node without attribute should not cause conflicts
            public class GenericNode : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Data { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MultipleNodesWithSameLabel_ProducesMultipleDiagnostics()
    {
        var test = """
            using Cvoya.Graph;
            
            [Node("Person")]
            public class PersonNode1 : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            [Node("Person")]
            public class {|#0:PersonNode2|} : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            [Node("Person")]
            public class {|#1:PersonNode3|} : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("CG009").WithLocation(0).WithArguments("PersonNode2", "Person", "PersonNode1"),
            VerifyCS.Diagnostic("CG009").WithLocation(1).WithArguments("PersonNode3", "Person", "PersonNode1")
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithEmptyLabel_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph;
            
            [Node("")]
            public class EmptyLabelNode1 : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            [Node("")]
            public class EmptyLabelNode2 : Node
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            """;

        // Empty labels might be handled differently - this test assumes they're not checked for duplicates
        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassNotImplementingGraphInterface_NoDuplicateLabelDiagnostic()
    {
        // CG009 only compares labels across types that implement INode - neither of these classes
        // does, so CG009 stays silent (each still gets a CG012 for the misapplied attribute, which
        // is out of scope for this test class and covered by
        // CG012_MisappliedNodeOrRelationshipAttributeTests instead).
        var test = """
            using Cvoya.Graph;

            [{|#0:Node("Person")|}]
            public class RegularClass1
            {
                public string Name { get; set; } = string.Empty;
            }

            [{|#1:Node("Person")|}]
            public class RegularClass2
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        var expected = new[]
        {
            VerifyCG012.Diagnostic("CG012").WithLocation(0).WithArguments("RegularClass1", "Node", "INode"),
            VerifyCG012.Diagnostic("CG012").WithLocation(1).WithArguments("RegularClass2", "Node", "INode"),
        };

        await VerifyAnalyzerAsync(test, expected);
    }
}

// Helper typedef for the CG012 diagnostics incidentally produced by the misapplied-attribute
// fixtures above (this class's own diagnostics use the file-scoped VerifyCS below).
file static class VerifyCG012
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new DiagnosticResult(diagnosticId, DiagnosticSeverity.Warning);
}

// Helper typedef for cleaner syntax
file static class VerifyCS
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new DiagnosticResult(diagnosticId, DiagnosticSeverity.Error);
}