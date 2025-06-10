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
/// Tests for the GM007 diagnostic rule: Complex properties can only contain simple properties.
/// </summary>
public class GM007_ComplexPropertyOnlySimplePropertiesTests
{
    [Fact]
    public async Task ComplexPropertyWithOnlySimpleProperties_DoesNotReportError()
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
    public async Task ComplexPropertyWithComplexProperty_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public Person PersonInfo { get; set; }
}

public class Person
{
    public string Name { get; set; }
    public Address Address { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
}";

        var expected = Verify.Diagnostic("GM007")
            .WithSpan(7, 19, 7, 25)
            .WithArguments("PersonInfo", "Person");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexPropertyWithCollectionOfComplexProperties_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System.Collections.Generic;

public class MyNode : INode
{
    public string Id { get; set; }
    public Company CompanyInfo { get; set; }
}

public class Company
{
    public string Name { get; set; }
    public List<Address> Offices { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
}";

        var expected = Verify.Diagnostic("GM007")
            .WithSpan(8, 20, 8, 26)
            .WithArguments("CompanyInfo", "Company");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexPropertyWithArrayOfComplexProperties_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public Department DepartmentInfo { get; set; }
}

public class Department
{
    public string Name { get; set; }
    public Employee[] Employees { get; set; }
}

public class Employee
{
    public string Name { get; set; }
    public int Id { get; set; }
}";

        var expected = Verify.Diagnostic("GM007")
            .WithSpan(7, 23, 7, 29)
            .WithArguments("DepartmentInfo", "Department");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ComplexPropertyWithCollectionOfSimpleProperties_DoesNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System.Collections.Generic;

public class MyNode : INode
{
    public string Id { get; set; }
    public ContactInfo Contact { get; set; }
}

public class ContactInfo
{
    public string Email { get; set; }
    public List<string> PhoneNumbers { get; set; }
    public List<int> ContactIds { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task IRelationshipWithComplexProperty_DoesNotTriggerGM007()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyRelationship : IRelationship
{
    public string Id { get; set; }
    public Address Address { get; set; } // This will trigger GM006 but not GM007
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
}";

        var expected = Verify.Diagnostic("GM006")
            .WithSpan(7, 19, 7, 26)
            .WithArguments("Address", "Address");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task NestedComplexProperties_ReportsErrorOnDirectParent()
    {
        var test = @"
using Cvoya.Graph.Model;

public class MyNode : INode
{
    public string Id { get; set; }
    public CompanyInfo Company { get; set; }
}

public class CompanyInfo
{
    public string Name { get; set; }
    public Department MainDepartment { get; set; }
}

public class Department
{
    public string Name { get; set; }
    public Employee Manager { get; set; }
}

public class Employee
{
    public string Name { get; set; }
    public int Id { get; set; }
}";

        var expected = Verify.Diagnostic("GM007")
            .WithSpan(7, 24, 7, 30)
            .WithArguments("Company", "CompanyInfo");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }
}