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
/// Tests for the GM001 diagnostic rule: Must have parameterless constructor or property-initializing constructor.
/// </summary>
public class GM001_ConstructorTests
{
    [Fact]
    public async Task ClassWithParameterlessConstructor_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public MyNode() { }
    public string Id { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithoutExplicitConstructor_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task StructWithParameterizedConstructor_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;

public struct MyNode : INode
{
    public string Id { get; set; }
    
    public MyNode(string id)
    {
        Id = id;
    }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithParameterizedConstructorAndProperties_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public string Name { get; set; }
    
    public MyNode(string name)
    {
        Id = System.Guid.NewGuid().ToString();
        Name = name;
    }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassWithBothParameterlessAndParameterizedConstructors_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public string Name { get; set; }
    
    public MyNode() { }
    
    public MyNode(string name) : this()
    {
        Name = name;
    }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task RelationshipWithParameterlessConstructor_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyRelationship : IRelationship
{
    public MyRelationship() { }
    public string Id { get; set; }
    public string SourceId { get; set; }
    public string TargetId { get; set; }
    public bool IsBidirectional { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task RecordImplementingINode_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;

public record MyNode : INode
{
    public MyNode() { }
    public string Id { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task TypeNotImplementingGraphInterfaces_DoesNotReportError()
    {
        var test = @"
public class MyClass
{
    public MyClass(string value)
    {
        Value = value;
    }
    
    public string Value { get; private set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }
}