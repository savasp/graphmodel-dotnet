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
/// Tests for the GM001 diagnostic rule: Only classes can implement INode or IRelationship.
/// </summary>
public class GM001_OnlyClassesCanImplementTests
{
    [Fact]
    public async Task StructImplementingINode_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public struct MyNode : INode
{
    public string Id { get; set; }
}";

        var expected = Verify.Diagnostic("GM001")
            .WithSpan(4, 15, 4, 21)
            .WithArguments("MyNode", "INode");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task StructImplementingIRelationship_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public struct MyRelationship : IRelationship
{
    public string Id { get; set; }
    public string SourceId { get; set; }
    public string TargetId { get; set; }
    public bool IsBidirectional { get; set; }
}";

        var expected = Verify.Diagnostic("GM001")
            .WithSpan(4, 15, 4, 29)
            .WithArguments("MyRelationship", "IRelationship");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ClassImplementingINode_DoesNotReportError()
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
    public async Task ClassImplementingIRelationship_DoesNotReportError()
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
    public async Task StructNotImplementingInterfaces_DoesNotReportError()
    {
        var test = @"
public struct MyStruct
{
    public string Value { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }
}