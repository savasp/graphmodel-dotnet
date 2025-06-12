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
/// Tests for the GM002 diagnostic rule: Type must have a parameterless constructor.
/// </summary>
public class GM002_MustHaveParameterlessConstructorTests
{
    [Fact]
    public async Task ClassWithoutConstructor_DoesNotReportError()
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
    public async Task ClassWithExplicitParameterlessConstructor_DoesNotReportError()
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
    public async Task ClassWithOnlyParameterizedConstructor_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public MyNode(string id) 
    {
        Id = id;
    }
    
    public string Id { get; set; }
}";

        var expected = Verify.Diagnostic("GM002")
            .WithSpan(4, 14, 4, 20)
            .WithArguments("MyNode", "INode");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ClassWithBothParameterlessAndParameterizedConstructors_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public MyNode() { }
    
    public MyNode(string id) 
    {
        Id = id;
    }
    
    public string Id { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task RelationshipWithoutConstructor_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyRelationship : IRelationship
{
    public string Id { get; set; }
    public string StartNodeId { get; set; }
    public string EndNodeId { get; set; }
    public bool IsBidirectional { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task RelationshipWithOnlyParameterizedConstructor_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyRelationship : IRelationship
{
    public MyRelationship(string startNodeId, string endNodeId) 
    {
        StartNodeId = startNodeId;
        EndNodeId = endNodeId;
    }
    
    public string Id { get; set; }
    public string StartNodeId { get; set; }
    public string EndNodeId { get; set; }
    public bool IsBidirectional { get; set; }
}";

        var expected = Verify.Diagnostic("GM002")
            .WithSpan(4, 14, 4, 28)
            .WithArguments("MyRelationship", "IRelationship");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task InternalParameterlessConstructor_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    internal MyNode() { }
    public string Id { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PrivateParameterlessConstructor_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    private MyNode() { }
    public string Id { get; set; }
}";

        var expected = Verify.Diagnostic("GM002")
            .WithSpan(4, 14, 4, 20)
            .WithArguments("MyNode", "INode");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }
}
