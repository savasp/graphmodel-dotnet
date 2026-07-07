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

namespace Cvoya.Graph.Model.Neo4j.Translation.Tests;

using System.Linq.Expressions;
using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Neo4j.Translation.Tests.Harness;
using Cvoya.Graph.Model.Neo4j.Translation.Tests.Model;

/// <summary>
/// Covers the "marker method" terminal LINQ operations (First/Single/Last/Any/All/Count/
/// LongCount/Sum/Average/Min/Max/Contains/ElementAt). These are represented in the expression
/// tree via internal marker methods (e.g. <c>FirstAsyncMarker</c>) that
/// <c>CypherQueryVisitor.HandleLinqMethod</c> dispatches on - see
/// <see cref="MarkerExpressions"/> for why they're built directly rather than via the public
/// async extension methods (which execute eagerly).
/// </summary>
public class TerminalOperationsTranslationTests : TranslationTestBase
{
    [Fact]
    public Task ToListAsync_MaterializesResults()
    {
        var source = Root.Nodes<Person>();
        var expr = MarkerExpressions.Call<Person>("ToListAsyncMarker", source.Expression);
        return VerifyTranslation(typeof(Person), expr);
    }

    [Fact]
    public Task ToArrayAsync_MaterializesResults()
    {
        var source = Root.Nodes<Person>();
        var expr = MarkerExpressions.Call<Person>("ToArrayAsyncMarker", source.Expression);
        return VerifyTranslation(typeof(Person), expr);
    }

    [Fact]
    public Task First_NoPredicate()
    {
        var source = Root.Nodes<Person>();
        var expr = MarkerExpressions.Call<Person>("FirstAsyncMarker", source.Expression);
        return VerifyTranslation(typeof(Person), expr);
    }

    [Fact]
    public Task FirstOrDefault_NoPredicate()
    {
        var source = Root.Nodes<Person>();
        var expr = MarkerExpressions.Call<Person>("FirstOrDefaultAsyncMarker", source.Expression);
        return VerifyTranslation(typeof(Person), expr);
    }

    [Fact]
    public Task Single_NoPredicate()
    {
        var source = Root.Nodes<Person>();
        var expr = MarkerExpressions.Call<Person>("SingleAsyncMarker", source.Expression);
        return VerifyTranslation(typeof(Person), expr);
    }

    [Fact]
    public Task SingleOrDefault_NoPredicate()
    {
        var source = Root.Nodes<Person>();
        var expr = MarkerExpressions.Call<Person>("SingleOrDefaultAsyncMarker", source.Expression);
        return VerifyTranslation(typeof(Person), expr);
    }

    [Fact]
    public Task Last_NoPredicate()
    {
        var source = Root.Nodes<Person>().OrderBy(p => p.LastName);
        var expr = MarkerExpressions.Call<Person>("LastAsyncMarker", source.Expression);
        return VerifyTranslation(typeof(Person), expr);
    }

    [Fact]
    public Task LastOrDefault_NoPredicate()
    {
        var source = Root.Nodes<Person>().OrderBy(p => p.LastName);
        var expr = MarkerExpressions.Call<Person>("LastOrDefaultAsyncMarker", source.Expression);
        return VerifyTranslation(typeof(Person), expr);
    }

    [Theory]
    [InlineData("LastAsyncMarker")]
    [InlineData("LastOrDefaultAsyncMarker")]
    public void LastTerminals_WithoutOrderBy_ThrowGraphException(string markerName)
    {
        var source = Root.Nodes<Person>();
        var expr = MarkerExpressions.Call<Person>(markerName, source.Expression);

        var exception = Assert.Throws<GraphException>(() => CypherTranslator.Translate(typeof(Person), expr));

        Assert.Contains("OrderBy", exception.Message);
    }

    [Fact]
    public Task Any_NoPredicate()
    {
        var source = Root.Nodes<Person>();
        var expr = MarkerExpressions.Call<Person>("AnyAsyncMarker", source.Expression);
        return VerifyTranslation(typeof(Person), expr);
    }

    [Fact]
    public Task All_WithPredicate()
    {
        var source = Root.Nodes<Person>();
        Expression<Func<Person, bool>> predicate = p => p.Age >= 18;
        var expr = MarkerExpressions.Call<Person>("AllAsyncMarker", source.Expression, predicate);
        return VerifyTranslation(typeof(Person), expr);
    }

    [Fact]
    public Task Count_NoPredicate()
    {
        var source = Root.Nodes<Person>();
        var expr = MarkerExpressions.Call<Person>("CountAsyncMarker", source.Expression);
        return VerifyTranslation(typeof(Person), expr);
    }

    [Fact]
    public Task OrderByThenCount_DropsOrderingFromAggregate()
    {
        var source = Root.Nodes<Person>().OrderBy(p => p.LastName);
        var expr = MarkerExpressions.Call<Person>("CountAsyncMarker", source.Expression);
        return VerifyTranslation(typeof(Person), expr);
    }

    [Fact]
    public Task OrderByTakeThenCount_PreservesOrderingInPipedWith()
    {
        var source = Root.Nodes<Person>().OrderBy(p => p.LastName).Take(5);
        var expr = MarkerExpressions.Call<Person>("CountAsyncMarker", source.Expression);
        return VerifyTranslation(typeof(Person), expr);
    }

    [Fact]
    public Task Count_WithPredicate()
    {
        var source = Root.Nodes<Person>();
        Expression<Func<Person, bool>> predicate = p => p.Age > 18;
        var expr = MarkerExpressions.Call<Person>("CountAsyncMarker", source.Expression, predicate);
        return VerifyTranslation(typeof(Person), expr);
    }

    [Fact]
    public Task LongCount_NoPredicate()
    {
        var source = Root.Nodes<Person>();
        var expr = MarkerExpressions.Call<Person>("LongCountAsyncMarker", source.Expression);
        return VerifyTranslation(typeof(Person), expr);
    }

    [Fact]
    public Task Sum_WithSelector()
    {
        var source = Root.Nodes<Person>();
        Expression<Func<Person, int>> selector = p => p.Age;
        var expr = MarkerExpressions.Call<Person>("SumAsyncMarker", source.Expression, selector);
        return VerifyTranslation(typeof(Person), expr);
    }

    [Fact]
    public Task Average_WithSelector()
    {
        var source = Root.Nodes<Person>();
        Expression<Func<Person, int>> selector = p => p.Age;
        var expr = MarkerExpressions.Call<Person>("AverageAsyncMarker", source.Expression, selector);
        return VerifyTranslation(typeof(Person), expr);
    }

    [Fact]
    public Task Min_WithSelector()
    {
        var source = Root.Nodes<Person>();
        Expression<Func<Person, int>> selector = p => p.Age;
        var expr = MarkerExpressions.Call<Person>("MinAsyncMarker", source.Expression, selector);
        return VerifyTranslation(typeof(Person), expr);
    }

    [Fact]
    public Task Max_WithSelector()
    {
        var source = Root.Nodes<Person>();
        Expression<Func<Person, int>> selector = p => p.Age;
        var expr = MarkerExpressions.Call<Person>("MaxAsyncMarker", source.Expression, selector);
        return VerifyTranslation(typeof(Person), expr);
    }

    [Fact]
    public Task OrderByThenMax_DropsOrderingFromAggregate()
    {
        var source = Root.Nodes<Person>().OrderBy(p => p.LastName);
        Expression<Func<Person, int>> selector = p => p.Age;
        var expr = MarkerExpressions.Call<Person>("MaxAsyncMarker", source.Expression, selector);
        return VerifyTranslation(typeof(Person), expr);
    }

    [Fact]
    public Task Contains_OnScalarProjection()
    {
        var source = Root.Nodes<Person>().Select(p => p.FirstName);
        var expr = MarkerExpressions.Call<string>("ContainsAsyncMarker", source.Expression, Expression.Constant("Alice"));
        return VerifyTranslation(typeof(string), expr);
    }

    [Fact]
    public Task Contains_OnScalarProjection_NullItem()
    {
        var source = Root.Nodes<Person>().Select(p => p.FirstName);
        var expr = MarkerExpressions.Call<string>("ContainsAsyncMarker", source.Expression, Expression.Constant(null, typeof(string)));
        return VerifyTranslation(typeof(string), expr);
    }

    [Fact]
    public Task Contains_OnConcreteNodeQueryableConstant()
    {
        var source = Root.ConcreteNodeQueryableConstant<Person>().Select(p => p.FirstName);
        var expr = MarkerExpressions.Call<string>("ContainsAsyncMarker", source.Expression, Expression.Constant("Alice"));
        return VerifyTranslation(typeof(string), expr);
    }

    [Fact]
    public Task ElementAt_ByIndex()
    {
        var source = Root.Nodes<Person>().OrderBy(p => p.LastName);
        var expr = MarkerExpressions.Call<Person>("ElementAtAsyncMarker", source.Expression, Expression.Constant(3));
        return VerifyTranslation(typeof(Person), expr);
    }

    [Fact]
    public Task ElementAtOrDefault_ByIndex()
    {
        var source = Root.Nodes<Person>().OrderBy(p => p.LastName);
        var expr = MarkerExpressions.Call<Person>("ElementAtOrDefaultAsyncMarker", source.Expression, Expression.Constant(3));
        return VerifyTranslation(typeof(Person), expr);
    }
}
