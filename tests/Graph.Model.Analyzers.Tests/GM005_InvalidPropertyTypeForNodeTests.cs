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
/// Tests for the GM005 diagnostic rule: Invalid property type for INode implementation.
/// </summary>
public class GM005_InvalidPropertyTypeForNodeTests
{
    [Fact]
    public async Task SimplePropertyTypes_DoNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System;
using System.Collections.Generic;

public class MyNode : INode
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }
    public double Score { get; set; }
    public bool IsActive { get; set; }
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

public class MyNode : INode
{
    public string Id { get; set; }
    public List<string> Tags { get; set; }
    public int[] Numbers { get; set; }
    public HashSet<double> Scores { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValidComplexPropertyTypes_DoNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System.Collections.Generic;

public class MyNode : INode
{
    public string Id { get; set; }
    public Address Address { get; set; }
    public List<Address> Addresses { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task UnsupportedPropertyType_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System.Collections.Generic;
using System.IO;

public class MyNode : INode
{
    public string Id { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}";

        var expected = Verify.Diagnostic("GM005")
            .WithSpan(9, 35, 9, 43)
            .WithArguments("Metadata", "Dictionary<string, object>");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task InterfacePropertyType_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System.Collections.Generic;

public class MyNode : INode
{
    public string Id { get; set; }
    public IEnumerable Data { get; set; }
}";

        var expected = Verify.Diagnostic("GM005")
            .WithSpan(8, 23, 8, 27)
            .WithArguments("Data", "IEnumerable");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task InvalidComplexType_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public InvalidComplex Complex { get; set; }
}

public class InvalidComplex
{
    public InvalidComplex(string value) { Value = value; } // No parameterless constructor
    public string Value { get; private set; } // Private setter
}";

        var expected = Verify.Diagnostic("GM005")
            .WithSpan(7, 27, 7, 34)
            .WithArguments("Complex", "InvalidComplex");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task StructPropertyType_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public MyStruct Data { get; set; }
}

public struct MyStruct
{
    public string Value { get; set; }
}";

        var expected = Verify.Diagnostic("GM005")
            .WithSpan(7, 21, 7, 25)
            .WithArguments("Data", "MyStruct");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task MultidimensionalArray_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public string[,] Matrix { get; set; }
}";

        var expected = Verify.Diagnostic("GM005")
            .WithSpan(7, 22, 7, 28)
            .WithArguments("Matrix", "string[,]");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task CollectionOfUnsupportedType_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System.Collections.Generic;
using System.IO;

public class MyNode : INode
{
    public string Id { get; set; }
    public List<Stream> Streams { get; set; }
}";

        var expected = Verify.Diagnostic("GM005")
            .WithSpan(9, 21, 9, 28)
            .WithArguments("Streams", "List<Stream>");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipType_DoesNotTriggerGM005()
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
    // This should trigger GM006, not GM005
    public Stream Data { get; set; }
}";

        // Should not trigger GM005 since it's IRelationship, not INode
        await Verify.VerifyAnalyzerAsync(test);
    }
}
