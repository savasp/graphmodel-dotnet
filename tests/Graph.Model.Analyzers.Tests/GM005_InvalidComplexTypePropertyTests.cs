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
/// Tests for the GM005 diagnostic rule: Invalid complex type property.
/// </summary>
public class GM005_InvalidComplexTypePropertyTests
{
    [Fact]
    public async Task ValidComplexTypeProperty_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System;

public class Address
{
    public Address() { }
    public string Street { get; set; }
    public string City { get; set; }
    public string ZipCode { get; set; }
}

public class MyNode : INode
{
    public string Id { get; set; }
    public Address Address { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ValidComplexTypeCollectionProperty_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System;
using System.Collections.Generic;

public class Tag
{
    public Tag() { }
    public string Name { get; set; }
    public string Value { get; set; }
}

public class MyNode : INode
{
    public string Id { get; set; }
    public List<Tag> Tags { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ComplexTypeWithoutParameterlessConstructor_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class Address
{
    public Address(string street) 
    {
        Street = street;
    }
    
    public string Street { get; set; }
    public string City { get; set; }
}

public class MyNode : INode
{
    public string Id { get; set; }
    public Address Address { get; set; }
}";

        var expected = Verify.Diagnostic("GM005")
            .WithSpan(18, 12, 18, 19)
            .WithArguments("Address", "Address");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexTypeWithPrivateConstructor_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class Address
{
    private Address() { }
    
    public string Street { get; set; }
    public string City { get; set; }
}

public class MyNode : INode
{
    public string Id { get; set; }
    public Address Address { get; set; }
}";

        var expected = Verify.Diagnostic("GM005")
            .WithSpan(15, 12, 15, 19)
            .WithArguments("Address", "Address");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexTypeWithNonPublicProperty_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class Address
{
    private string Street { get; set; }  // Non-public property
    public string City { get; set; }
}

public class MyNode : INode
{
    public string Id { get; set; }
    public Address Address { get; set; }
}";

        var expected = Verify.Diagnostic("GM005")
            .WithSpan(13, 12, 13, 19)
            .WithArguments("Address", "Address");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexTypeWithGetterOnlyProperty_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class Address
{
    public string Street { get; }
    public string City { get; set; }
}

public class MyNode : INode
{
    public string Id { get; set; }
    public Address Address { get; set; }
}";

        var expected = Verify.Diagnostic("GM005")
            .WithSpan(13, 12, 13, 19)
            .WithArguments("Address", "Address");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexTypeInRelationship_DoesNotReportGM005()
    {
        // GM005 only applies to INode implementations, not IRelationship
        var test = @"
using Cvoya.Graph.Model;

public class Metadata
{
    public Metadata(string key) 
    {
        Key = key;
    }
    
    public string Key { get; set; }
    public string Value { get; set; }
}

public class MyRelationship : IRelationship
{
    public string Id { get; set; }
    public string StartNodeId { get; set; }
    public string EndNodeId { get; set; }
    public bool IsBidirectional { get; set; }
    public Metadata Metadata { get; set; }
}";

        // Should report GM004 for unsupported type, not GM005
        var expected = Verify.Diagnostic("GM004")
            .WithSpan(21, 12, 21, 20) // Just the type "Metadata"
            .WithArguments("Metadata", "Metadata");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexTypeWithValidNestedComplexType_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class Coordinates
{
    public Coordinates() { }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class Address
{
    public Address() { }
    
    public string Street { get; set; }
    public string City { get; set; }
    public Coordinates Location { get; set; }
}

public class MyNode : INode
{
    public string Id { get; set; }
    public Address Address { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task ComplexTypeArrayProperty_ReportsErrorForInvalidElement()
    {
        var test = @"
using Cvoya.Graph.Model;

public class Address
{
    public Address(string street) 
    {
        Street = street;
    }
    
    public string Street { get; set; }
}

public class MyNode : INode
{
    public string Id { get; set; }
    public Address[] Addresses { get; set; }
}";

        var expected = Verify.Diagnostic("GM005")
            .WithSpan(17, 12, 17, 21)
            .WithArguments("Addresses", "Address[]");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task StructComplexType_ReportsGM004NotGM005()
    {
        var test = @"
using Cvoya.Graph.Model;

public struct AddressStruct
{
    public string Street { get; set; }
    public string City { get; set; }
}

public class MyNode : INode
{
    public string Id { get; set; }
    public AddressStruct Address { get; set; }
}";

        // Structs should be caught by GM004 (unsupported type), not GM005
        var expected = Verify.Diagnostic("GM004")
            .WithSpan(13, 12, 13, 25) // "AddressStruct" is 13 characters, ends at column 25
            .WithArguments("Address", "AddressStruct");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexTypeInIRelationship_ShouldNotReportGM005()
    {
        // GM005 should only apply to INode implementations, not IRelationship
        var test = """
        using Cvoya.Graph.Model;

        public class ComplexType
        {
            public ComplexType(string value) { Value = value; }
            public string Value { get; set; }
        }

        public class MyRelationship : IRelationship
        {
            public string Id { get; set; }
            public string StartNodeId { get; set; }
            public string EndNodeId { get; set; }
            public bool IsBidirectional { get; set; }
            public ComplexType Complex { get; set; } // Should report GM004, not GM005
        }
        """;

        var expected = Verify.Diagnostic("GM004")
            .WithSpan(15, 12, 15, 23)
            .WithArguments("Complex", "ComplexType");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexTypeInINode_WithoutParameterlessConstructor_ShouldReportGM005()
    {
        // Complex types in INode must have parameterless constructors
        var test = """
    using Cvoya.Graph.Model;

    public class ComplexType
    {
        public ComplexType(string value) { Value = value; }
        public string Value { get; set; }
    }

    public class MyNode : INode
    {
        public string Id { get; set; }
        public ComplexType Complex { get; set; } // Should report GM005
    }
    """;

        var expected = Verify.Diagnostic("GM005")
            .WithSpan(12, 12, 12, 23)
            .WithArguments("Complex", "ComplexType");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }
}