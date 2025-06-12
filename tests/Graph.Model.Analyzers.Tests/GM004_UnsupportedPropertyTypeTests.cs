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
/// Tests for the GM004 diagnostic rule: Unsupported property type.
/// </summary>
public class GM004_UnsupportedPropertyTypeTests
{
    [Fact]
    public async Task SupportedPrimitiveTypes_DoNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System;

public class MyNode : INode
{
    public string Id { get; set; }
    public int Count { get; set; }
    public bool IsActive { get; set; }
    public double Value { get; set; }
    public decimal Price { get; set; }
    public float Height { get; set; }
    public long LongValue { get; set; }
    public byte ByteValue { get; set; }
    public char CharValue { get; set; }
    public short ShortValue { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task SupportedDateTimeTypes_DoNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System;

public class MyNode : INode
{
    public string Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTimeOffset ModifiedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task SupportedOtherTypes_DoNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System;

public enum Priority { Low, Medium, High }

public class MyNode : INode
{
    public string Id { get; set; }
    public Guid UniqueId { get; set; }
    public Priority Priority { get; set; }
    public Point Location { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task SupportedCollectionTypes_DoNotReportError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System;
using System.Collections.Generic;

public class MyNode : INode
{
    public string Id { get; set; }
    public List<string> Tags { get; set; }
    public IList<int> Numbers { get; set; }
    public ICollection<bool> Flags { get; set; }
    public IEnumerable<DateTime> Dates { get; set; }
    public string[] StringArray { get; set; }
    public int[] IntArray { get; set; }
}";

        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task SupportedCustomClass_DoesNotReportError()  // Changed name and expectation
    {
        var test = @"
using Cvoya.Graph.Model;

public class CustomClass
{
    public string Value { get; set; }
}

public class MyNode : INode
{
    public string Id { get; set; }
    public CustomClass Custom { get; set; }
}";

        // No expected diagnostics - this should be valid
        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task UnsupportedInterface_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System.Collections;

public class MyNode : INode
{
    public string Id { get; set; }
    public IEnumerable Items { get; set; }
}";

        var expected = Verify.Diagnostic("GM004")
            .WithSpan(8, 12, 8, 23)
            .WithArguments("Items", "System.Collections.IEnumerable");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task UnsupportedDelegate_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System;

public class MyNode : INode
{
    public string Id { get; set; }
    public Action Callback { get; set; }
}";

        var expected = Verify.Diagnostic("GM004")
            .WithSpan(8, 12, 8, 18) // "Action" is 6 characters
            .WithArguments("Callback", "System.Action");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task UnsupportedGenericType_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System;

public class MyNode : INode
{
    public string Id { get; set; }
    public Tuple<string, int> TupleValue { get; set; }
}";

        var expected = Verify.Diagnostic("GM004")
            .WithSpan(8, 12, 8, 30) // "Tuple<string, int>" is 18 characters
            .WithArguments("TupleValue", "System.Tuple<string, int>");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task SupportedCollectionOfCustomClass_DoesNotReportError()  // Changed name and expectation
    {
        var test = @"
using Cvoya.Graph.Model;
using System;
using System.Collections.Generic;

public class CustomClass
{
    public string Value { get; set; }
}

public class MyNode : INode
{
    public string Id { get; set; }
    public List<CustomClass> CustomList { get; set; }
}";

        // No expected diagnostics - this should be valid since CustomClass is a valid complex type
        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task UnsupportedNullableTypes_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System;

public struct CustomStruct
{
    public int Value { get; set; }
}

public class MyNode : INode
{
    public string Id { get; set; }
    public CustomStruct? NullableCustom { get; set; }
}";

        var expected = Verify.Diagnostic("GM004")
            .WithSpan(13, 12, 13, 25) // "CustomStruct?" is 13 characters
            .WithArguments("NullableCustom", "CustomStruct?");

        await Verify.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task RelationshipWithUnsupportedProperty_ReportsError()
    {
        var test = @"
using Cvoya.Graph.Model;
using System;

public class MyRelationship : IRelationship
{
    public string Id { get; set; }
    public string StartNodeId { get; set; }
    public string EndNodeId { get; set; }
    public bool IsBidirectional { get; set; }
    public object Metadata { get; set; }
}";

        var expected = Verify.Diagnostic("GM004")
            .WithSpan(11, 12, 11, 18) // "object" is 6 characters
            .WithArguments("Metadata", "object");

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
    public int[,] Matrix { get; set; }
}";

        var expected = Verify.Diagnostic("GM004")
            .WithSpan(7, 12, 7, 18) // Just the type "int[,]" - 6 characters
            .WithArguments("Matrix", "int[*,*]"); // Roslyn represents it as int[*,*]

        await Verify.VerifyAnalyzerAsync(test, expected);
    }
}
