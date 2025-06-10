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
/// Tests for the GM004 diagnostic rule: Complex property contains invalid nested properties.
/// </summary>
public class GM004_ComplexPropertyNestedPropertiesTests
{
    [Fact]
    public async Task ComplexPropertyWithSimpleProperties_DoesNotReportError()
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
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ComplexPropertyWithNestedINode_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public ContactInfo Contact { get; set; }
}

public class ContactInfo
{
    public string Email { get; set; }
    public MyNode EmergencyContact { get; set; }
}";

        var expected = Verify.Diagnostic("GM004")
            .WithSpan(7, 19, 7, 26)
            .WithArguments("Contact", "ContactInfo");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexPropertyWithNestedIRelationship_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public ContactInfo Contact { get; set; }
}

public class ContactInfo
{
    public string Email { get; set; }
    public MyRelationship Connection { get; set; }
}

public class MyRelationship : IRelationship
{
    public string Id { get; set; }
    public string SourceId { get; set; }
    public string TargetId { get; set; }
    public bool IsBidirectional { get; set; }
}";

        var expected = Verify.Diagnostic("GM004")
            .WithSpan(7, 19, 7, 26)
            .WithArguments("Contact", "ContactInfo");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NestedComplexPropertyWithDeepINode_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public ContactInfo Contact { get; set; }
}

public class ContactInfo
{
    public string Email { get; set; }
    public AddressInfo Address { get; set; }
}

public class AddressInfo
{
    public string Street { get; set; }
    public MyNode Neighbor { get; set; }
}";

        var expected = Verify.Diagnostic("GM004")
            .WithSpan(7, 19, 7, 26)
            .WithArguments("Contact", "ContactInfo");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexPropertyWithCollectionOfINode_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System.Collections.Generic;

public class MyNode : INode
{
    public string Id { get; set; }
    public GroupInfo Group { get; set; }
}

public class GroupInfo
{
    public string Name { get; set; }
    public List<MyNode> Members { get; set; }
}";

        var expected = Verify.Diagnostic("GM004")
            .WithSpan(7, 18, 7, 23)
            .WithArguments("Group", "GroupInfo");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task CollectionOfComplexTypeWithINode_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System.Collections.Generic;

public class MyNode : INode
{
    public string Id { get; set; }
    public List<ContactInfo> Contacts { get; set; }
}

public class ContactInfo
{
    public string Email { get; set; }
    public MyNode Person { get; set; }
}";

        var expected = Verify.Diagnostic("GM004")
            .WithSpan(7, 26, 7, 34)
            .WithArguments("Contacts", "List<ContactInfo>");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithComplexProperty_DoesNotTriggerGM004()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyRelationship : IRelationship
{
    public string Id { get; set; }
    public string SourceId { get; set; }
    public string TargetId { get; set; }
    public bool IsBidirectional { get; set; }
    // This should trigger GM006 (invalid property type for relationship) 
    // but not GM004 (which only applies to INode)
    public ContactInfo Details { get; set; }
}

public class ContactInfo
{
    public string Email { get; set; }
    public MyNode Person { get; set; }
}

public class MyNode : INode
{
    public string Id { get; set; }
}";

        // Should not trigger GM004, but will trigger GM006 instead
        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task SimplePropertyTypes_DoNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System.Collections.Generic;

public class MyNode : INode
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }
    public List<string> Tags { get; set; }
    public double[] Scores { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }
}