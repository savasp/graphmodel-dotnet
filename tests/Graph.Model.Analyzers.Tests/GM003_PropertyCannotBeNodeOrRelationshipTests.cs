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

using Xunit;

namespace Cvoya.Graph.Model.Analyzers.Tests;

/// <summary>
/// Tests for the GM003 diagnostic rule: Property cannot be INode or IRelationship.
/// </summary>
public class GM003_PropertyCannotBeNodeOrRelationshipTests
{
    [Fact]
    public async Task PropertyWithSimpleType_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PropertyWithComplexType_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public Address Address { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PropertyWithINodeType_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public MyNode Parent { get; set; }
}";

        var expected = Verify.Diagnostic("GM003")
            .WithSpan(7, 19, 7, 25)
            .WithArguments("Parent", "MyNode");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PropertyWithIRelationshipType_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public MyRelationship Connection { get; set; }
}

public class MyRelationship : IRelationship
{
    public string Id { get; set; }
    public string SourceId { get; set; }
    public string TargetId { get; set; }
    public bool IsBidirectional { get; set; }
}";

        var expected = Verify.Diagnostic("GM003")
            .WithSpan(7, 27, 7, 37)
            .WithArguments("Connection", "MyRelationship");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PropertyWithListOfINode_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System.Collections.Generic;

public class MyNode : INode
{
    public string Id { get; set; }
    public List<MyNode> Children { get; set; }
}";

        var expected = Verify.Diagnostic("GM003")
            .WithSpan(8, 25, 8, 33)
            .WithArguments("Children", "List<MyNode>");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PropertyWithArrayOfIRelationship_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public MyRelationship[] Connections { get; set; }
}

public class MyRelationship : IRelationship
{
    public string Id { get; set; }
    public string SourceId { get; set; }
    public string TargetId { get; set; }
    public bool IsBidirectional { get; set; }
}";

        var expected = Verify.Diagnostic("GM003")
            .WithSpan(7, 29, 7, 40)
            .WithArguments("Connections", "MyRelationship[]");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithINodeProperty_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyRelationship : IRelationship
{
    public string Id { get; set; }
    public string SourceId { get; set; }
    public string TargetId { get; set; }
    public bool IsBidirectional { get; set; }
    public MyNode Source { get; set; }
}

public class MyNode : INode
{
    public string Id { get; set; }
}";

        var expected = Verify.Diagnostic("GM003")
            .WithSpan(10, 19, 10, 25)
            .WithArguments("Source", "MyNode");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PropertyWithListOfSimpleType_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System.Collections.Generic;

public class MyNode : INode
{
    public string Id { get; set; }
    public List<string> Tags { get; set; }
    public int[] Numbers { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassNotImplementingGraphInterfaces_DoesNotReportError()
    {
        var test = @"
public class MyClass
{
    public MyClass Parent { get; set; }
    public MyClass[] Children { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }
}
