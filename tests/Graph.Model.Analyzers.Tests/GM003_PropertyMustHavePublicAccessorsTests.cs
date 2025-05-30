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
/// Tests for the GM003 diagnostic rule: Property must have public getter and setter.
/// </summary>
public class GM003_PropertyMustHavePublicAccessorsTests
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
    public async Task PropertyWithPrivateGetter_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public string Name { private get; set; }
}";

        var expected = Verify.Diagnostic("GM003")
            .WithSpan(7, 19, 7, 23)
            .WithArguments("Name", "MyNode");

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

        var expected = Verify.Diagnostic("GM003")
            .WithSpan(7, 19, 7, 23)
            .WithArguments("Name", "MyNode");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PropertyWithInternalGetter_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public string Name { internal get; set; }
}";

        var expected = Verify.Diagnostic("GM003")
            .WithSpan(7, 19, 7, 23)
            .WithArguments("Name", "MyNode");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PropertyWithInternalSetter_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public string Name { get; internal set; }
}";

        var expected = Verify.Diagnostic("GM003")
            .WithSpan(7, 19, 7, 23)
            .WithArguments("Name", "MyNode");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PropertyWithGetterOnly_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public string Name { get; }
}";

        var expected = Verify.Diagnostic("GM003")
            .WithSpan(7, 19, 7, 23)
            .WithArguments("Name", "MyNode");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PropertyWithSetterOnly_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public string Name { set; }
}";

        var expected = Verify.Diagnostic("GM003")
            .WithSpan(7, 19, 7, 23)
            .WithArguments("Name", "MyNode");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipPropertyAccessors_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyRelationship : IRelationship
{
    public string Id { get; set; }
    public string SourceId { get; private set; }
    public string TargetId { get; set; }
    public bool IsBidirectional { get; set; }
}";

        var expected = Verify.Diagnostic("GM003")
            .WithSpan(7, 19, 7, 27)
            .WithArguments("SourceId", "MyRelationship");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task PropertyWithExplicitBackingField_StillNeedsPublicAccessors()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    private string _name;
    
    public string Id { get; set; }
    
    public string Name 
    { 
        get => _name; 
        private set => _name = value; 
    }
}";

        var expected = Verify.Diagnostic("GM003")
            .WithSpan(10, 19, 10, 23)
            .WithArguments("Name", "MyNode");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task IndexerProperty_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    
    // Indexers should be ignored by the analyzer
    public string this[int index] { get => ""test""; set { } }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }
}
