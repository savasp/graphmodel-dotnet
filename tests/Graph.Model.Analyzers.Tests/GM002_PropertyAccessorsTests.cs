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
/// Tests for the GM002 diagnostic rule: Property must have public getter and setter or initializer.
/// </summary>
public class GM002_PropertyAccessorsTests
{
    [Fact]
    public async Task PropertyWithPublicGetterAndSetter_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public string Name { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PropertyWithPublicGetterAndInitializer_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; init; }
    public string Name { get; init; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task PropertyWithPrivateGetter_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public string Name { private get; set; }
}";

        var expected = Verify.Diagnostic("GM002")
            .WithSpan(7, 19, 7, 23)
            .WithArguments("Name");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PropertyWithPrivateSetter_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public string Name { get; private set; }
}";

        var expected = Verify.Diagnostic("GM002")
            .WithSpan(7, 19, 7, 23)
            .WithArguments("Name");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PropertyWithNoSetter_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public string Name { get; }
}";

        var expected = Verify.Diagnostic("GM002")
            .WithSpan(7, 19, 7, 23)
            .WithArguments("Name");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithValidProperties_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyRelationship : IRelationship
{
    public string Id { get; set; }
    public string SourceId { get; set; }
    public string TargetId { get; set; }
    public bool IsBidirectional { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task StructWithValidProperties_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;

public struct MyNode : INode
{
    public string Id { get; set; }
    public string Name { get; init; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassNotImplementingGraphInterfaces_DoesNotReportError()
    {
        var test = @"
public class MyClass
{
    public string Value { get; private set; }
    private string Secret { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }
}
