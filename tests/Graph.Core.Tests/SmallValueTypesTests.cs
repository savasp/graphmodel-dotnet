// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.
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

namespace Cvoya.Graph.Core.Tests;

using Cvoya.Graph.Serialization;


[Trait("Area", "SmallValueTypes")]
public class SmallValueTypesTests
{
    public static TheoryData<RelationshipDirection, int, string> RelationshipDirectionCases => new()
    {
        { RelationshipDirection.Outgoing, 0, nameof(RelationshipDirection.Outgoing) },
        { RelationshipDirection.Incoming, 1, nameof(RelationshipDirection.Incoming) },
    };

    public static TheoryData<Point, Point, bool> PointEqualityCases => new()
    {
        { Point.Origin, new Point(), true },
        { new Point { Longitude = 1, Latitude = 2, Height = 3 }, new Point { Longitude = 1, Latitude = 2, Height = 3 }, true },
        { new Point { Longitude = 1, Latitude = 2, Height = 3 }, new Point { Longitude = 9, Latitude = 2, Height = 3 }, false },
        { new Point { Longitude = 1, Latitude = 2, Height = 3 }, new Point { Longitude = 1, Latitude = 9, Height = 3 }, false },
        { new Point { Longitude = 1, Latitude = 2, Height = 3 }, new Point { Longitude = 1, Latitude = 2, Height = 9 }, false },
    };

    [Theory]
    [MemberData(nameof(RelationshipDirectionCases))]
    public void RelationshipDirection_ValuesAreStable(RelationshipDirection direction, int expectedValue, string expectedName)
    {
        Assert.Equal(expectedValue, (int)direction);
        Assert.Equal(expectedName, direction.ToString());
    }

    [Fact]
    public void Point_OriginAndDefaultUseZeroCoordinates()
    {
        Assert.Equal(new Point { Longitude = 0, Latitude = 0, Height = 0 }, Point.Origin);
        Assert.Equal(Point.Origin, new Point());
    }

    [Theory]
    [MemberData(nameof(PointEqualityCases))]
    public void Point_ValueEqualityUsesAllCoordinates(Point left, Point right, bool expected)
    {
        Assert.Equal(expected, left == right);
        Assert.Equal(expected, left.Equals(right));
    }

    [Fact]
    public void PropertyValidation_DefaultConstructorHasNoRules()
    {
        var validation = new PropertyValidation();

        Assert.Null(validation.MinLength);
        Assert.Null(validation.MaxLength);
        Assert.Null(validation.MinValue);
        Assert.Null(validation.MaxValue);
        Assert.Null(validation.Pattern);
    }

    [Fact]
    public void PropertyValidation_ValueEqualityUsesAllRules()
    {
        var first = new PropertyValidation(
            minLength: 2,
            maxLength: 20,
            minValue: 1,
            maxValue: 10,
            pattern: "^[a-z]+$");
        var second = new PropertyValidation(
            minLength: 2,
            maxLength: 20,
            minValue: 1,
            maxValue: 10,
            pattern: "^[a-z]+$");
        var different = second with { Pattern = "^[0-9]+$" };

        Assert.Equal(first, second);
        Assert.NotEqual(first, different);
    }

    [Fact]
    public void SimpleValue_CanCarryPointAsGraphSimpleValue()
    {
        var point = new Point { Longitude = 1.5, Latitude = 2.5, Height = 3.5 };
        var simple = new SimpleValue(point, typeof(Point));

        Assert.Equal(typeof(Point), simple.Type);
        Assert.Equal(point, simple.Object);
        Assert.True(GraphDataModel.IsSimple(simple.Type));
    }

    [Fact]
    public void SimpleCollection_CarriesElementTypeAndValues()
    {
        var collection = new SimpleCollection(
            new[]
            {
                new SimpleValue(new DateOnly(2026, 1, 2), typeof(DateOnly)),
                new SimpleValue(new DateOnly(2026, 1, 3), typeof(DateOnly)),
            },
            typeof(DateOnly));

        Assert.Equal(typeof(DateOnly), collection.ElementType);
        Assert.Equal(
            new[] { new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 3) },
            collection.Values.Select(value => Assert.IsType<DateOnly>(value.Object)));
    }
}
