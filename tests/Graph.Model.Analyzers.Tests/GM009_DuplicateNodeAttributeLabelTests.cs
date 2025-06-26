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

public class GM009_DuplicateNodeAttributeLabelTests
{
    [Fact]
    public async Task ValidNodeWithUniqueLabels_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            [Node("Person")]
            public class PersonNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            [Node("Company")]
            public class CompanyNode : INode
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
            using Cvoya.Graph.Model;
            
            [Node("Person")]
            public class PersonNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            [Node("Person")]
            public class {|#0:DuplicatePersonNode|} : INode
            {
                public string Id { get; init; } = string.Empty;
                public string FullName { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM009")
            .WithLocation(0)
            .WithArguments("DuplicatePersonNode", "Person", "PersonNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task InheritedNodeWithDuplicateLabel_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            [Node("Person")]
            public class BasePersonNode : INode
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

        var expected = VerifyCS.Diagnostic("GM009")
            .WithLocation(0)
            .WithArguments("DerivedPersonNode", "Person", "BasePersonNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeInheritingLabelFromParent_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            [Node("Person")]
            public class BasePersonNode : INode
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
            using Cvoya.Graph.Model;
            
            [Node("Person")]
            public class PersonNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            [Node("person")]
            public class {|#0:LowercasePersonNode|} : INode
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            """;

        // Assuming the analyzer treats node labels as case-insensitive
        var expected = VerifyCS.Diagnostic("GM009")
            .WithLocation(0)
            .WithArguments("LowercasePersonNode", "person", "PersonNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexInheritanceHierarchyWithDuplicates_ProducesDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            [Node("Entity")]
            public class BaseEntityNode : INode
            {
                public string Id { get; init; } = string.Empty;
            }
            
            [Node("Person")]
            public class PersonNode : BaseEntityNode
            {
                public string Name { get; set; } = string.Empty;
            }
            
            [Node("Entity")]
            public class {|#0:DuplicateEntityNode|} : INode
            {
                public string Id { get; init; } = string.Empty;
                public string Type { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM009")
            .WithLocation(0)
            .WithArguments("DuplicateEntityNode", "Entity", "BaseEntityNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithoutAttribute_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            [Node("Person")]
            public class PersonNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            // Node without attribute should not cause conflicts
            public class GenericNode : INode
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
            using Cvoya.Graph.Model;
            
            [Node("Person")]
            public class PersonNode1 : INode
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            [Node("Person")]
            public class {|#0:PersonNode2|} : INode
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            [Node("Person")]
            public class {|#1:PersonNode3|} : INode
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            """;

        var expected = new[]
        {
            VerifyCS.Diagnostic("GM009").WithLocation(0).WithArguments("PersonNode2", "Person", "PersonNode1"),
            VerifyCS.Diagnostic("GM009").WithLocation(1).WithArguments("PersonNode3", "Person", "PersonNode1")
        };

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithEmptyLabel_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            [Node("")]
            public class EmptyLabelNode1 : INode
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            [Node("")]
            public class EmptyLabelNode2 : INode
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            """;

        // Empty labels might be handled differently - this test assumes they're not checked for duplicates
        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NodeWithMultipleLabels_ChecksAllLabels()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            [Node("Person", "Individual")]
            public class PersonNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            [Node("Employee", "Person")]
            public class {|#0:EmployeeNode|} : INode
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
                public string Department { get; set; } = string.Empty;
            }
            """;

        var expected = VerifyCS.Diagnostic("GM009")
            .WithLocation(0)
            .WithArguments("EmployeeNode", "Person", "PersonNode");

        await VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeWithUniqueMultipleLabels_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            [Node("Person", "Individual")]
            public class PersonNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
            }
            
            [Node("Employee", "Worker")]
            public class EmployeeNode : INode
            {
                public string Id { get; init; } = string.Empty;
                public string Name { get; set; } = string.Empty;
                public string Department { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassNotImplementingGraphInterface_NoDiagnostic()
    {
        var test = """
            using Cvoya.Graph.Model;
            
            [Node("Person")]
            public class RegularClass1
            {
                public string Name { get; set; } = string.Empty;
            }
            
            [Node("Person")]
            public class RegularClass2
            {
                public string Name { get; set; } = string.Empty;
            }
            """;

        await VerifyAnalyzerAsync(test);
    }
}

// Helper typedef for cleaner syntax
file static class VerifyCS
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => new DiagnosticResult(diagnosticId, DiagnosticSeverity.Error);
}