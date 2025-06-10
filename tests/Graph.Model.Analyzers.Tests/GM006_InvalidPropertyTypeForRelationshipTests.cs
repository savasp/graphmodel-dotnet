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
/// Tests for the GM006 diagnostic rule: Invalid property type for IRelationship implementation.
/// </summary>
public class GM006_InvalidPropertyTypeForRelationshipTests
{
    [Fact]
    public async Task SimplePropertyTypes_DoNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System;

public class MyRelationship : IRelationship
{
    public string Id { get; set; }
    public string SourceId { get; set; }
    public string TargetId { get; set; }
    public bool IsBidirectional { get; set; }
    public string Name { get; set; }
    public int Weight { get; set; }
    public double Score { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid UniqueId { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task CollectionsOfSimpleTypes_DoNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System.Collections.Generic;

public class MyRelationship : IRelationship
{
    public string Id { get; set; }
    public string SourceId { get; set; }
    public string TargetId { get; set; }
    public bool IsBidirectional { get; set; }
    public List<string> Tags { get; set; }
    public int[] Weights { get; set; }
    public HashSet<double> Scores { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ComplexPropertyType_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyRelationship : IRelationship
{
    public string Id { get; set; }
    public string SourceId { get; set; }
    public string TargetId { get; set; }
    public bool IsBidirectional { get; set; }
    public Address Address { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
}";

        var expected = Verify.Diagnostic("GM006")
            .WithSpan(10, 19, 10, 26)
            .WithArguments("Address", "Address");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task CollectionOfComplexType_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System.Collections.Generic;

public class MyRelationship : IRelationship
{
    public string Id { get; set; }
    public string SourceId { get; set; }
    public string TargetId { get; set; }
    public bool IsBidirectional { get; set; }
    public List<Address> Addresses { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
}";

        var expected = Verify.Diagnostic("GM006")
            .WithSpan(11, 22, 11, 31)
            .WithArguments("Addresses", "List<Address>");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task UnsupportedPropertyType_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System.Collections.Generic;
using System.IO;

public class MyRelationship : IRelationship
{
    public string Id { get; set; }
    public string SourceId { get; set; }
    public string TargetId { get; set; }
    public bool IsBidirectional { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}";

        var expected = Verify.Diagnostic("GM006")
            .WithSpan(12, 35, 12, 43)
            .WithArguments("Metadata", "Dictionary<string, object>");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task StreamPropertyType_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System.IO;

public class MyRelationship : IRelationship
{
    public string Id { get; set; }
    public string SourceId { get; set; }
    public string TargetId { get; set; }
    public bool IsBidirectional { get; set; }
    public Stream Data { get; set; }
}";

        var expected = Verify.Diagnostic("GM006")
            .WithSpan(11, 19, 11, 23)
            .WithArguments("Data", "Stream");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task InterfacePropertyType_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System.Collections;

public class MyRelationship : IRelationship
{
    public string Id { get; set; }
    public string SourceId { get; set; }
    public string TargetId { get; set; }
    public bool IsBidirectional { get; set; }
    public IEnumerable Data { get; set; }
}";

        var expected = Verify.Diagnostic("GM006")
            .WithSpan(11, 23, 11, 27)
            .WithArguments("Data", "IEnumerable");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NodeType_DoesNotTriggerGM006()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    // This should trigger GM005, not GM006
    public Address Address { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
}";

        // Should not trigger GM006 since it's INode, not IRelationship
        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ClassNotImplementingGraphInterfaces_DoesNotReportError()
    {
        var test = @"
using System.IO;

public class MyClass
{
    public Stream Data { get; set; }
    public MyClass Complex { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }
}