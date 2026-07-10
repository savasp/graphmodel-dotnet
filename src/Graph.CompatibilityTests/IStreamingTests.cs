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

namespace Cvoya.Graph.CompatibilityTests;

public interface IStreamingTests : IGraphModelTest
{
    [Fact]
    public async Task StreamingQuery_DisposingEnumeratorBeforeExhaustion_CleansUpAutoTransaction()
    {
        var marker = $"AbandonedStream-{Guid.NewGuid():N}";

        for (var i = 0; i < 5; i++)
        {
            await Graph.CreateNodeAsync(
                new Person { FirstName = marker, LastName = i.ToString() },
                null,
                TestContext.Current.CancellationToken);
        }

        var filteredQuery = Graph.Nodes<Person>()
            .Where(p => p.FirstName == marker);

        var query = filteredQuery
            .OrderBy(p => p.LastName);

        var enumerator = query.GetAsyncEnumerator(TestContext.Current.CancellationToken);
        try
        {
            Assert.True(await enumerator.MoveNextAsync());
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        var count = await filteredQuery.CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task StreamingQuery_CancellationAfterFirstItem_ThrowsAndCleansUpAutoTransaction()
    {
        var marker = $"CancelledStream-{Guid.NewGuid():N}";

        for (var i = 0; i < 5; i++)
        {
            await Graph.CreateNodeAsync(
                new Person { FirstName = marker, LastName = i.ToString() },
                null,
                TestContext.Current.CancellationToken);
        }

        var filteredQuery = Graph.Nodes<Person>()
            .Where(p => p.FirstName == marker);

        var query = filteredQuery
            .OrderBy(p => p.LastName);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var enumerator = query.GetAsyncEnumerator(cts.Token);
        try
        {
            Assert.True(await enumerator.MoveNextAsync());

            await cts.CancelAsync();

            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await enumerator.MoveNextAsync());
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        var count = await filteredQuery.CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task StreamingQuery_CancelledMidEnumeration_ThrowsOperationCanceledException()
    {
        var marker = $"CancelledMidEnumeration-{Guid.NewGuid():N}";
        var dateOfBirth = new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < 3; i++)
        {
            await Graph.CreateNodeAsync(
                new Person
                {
                    FirstName = marker,
                    LastName = $"Person-{i}",
                    DateOfBirth = dateOfBirth,
                },
                null,
                TestContext.Current.CancellationToken);
        }

        var filteredQuery = Graph.Nodes<Person>()
            .Where(p => p.FirstName == marker);

        var query = filteredQuery
            .OrderBy(p => p.LastName);

        var seen = 0;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in query.WithCancellation(cts.Token))
            {
                seen++;
                Assert.Equal(1, seen);
                cts.Cancel();
            }
        });

        Assert.Equal(1, seen);

        var count = await filteredQuery.CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task StreamingQuery_ComplexPropertyEntities_MatchesBufferedResults()
    {
        var marker = $"StreamingComplex-{Guid.NewGuid():N}";

        for (var i = 0; i < 3; i++)
        {
            await Graph.CreateNodeAsync(
                new Class1
                {
                    Property1 = marker,
                    Property2 = $"Item-{i}",
                    A = new ComplexClassA
                    {
                        Property1 = $"A-{i}",
                        Property2 = $"A2-{i}",
                        B = new ComplexClassB { Property1 = $"A-B-{i}" },
                        C = new ComplexClassC
                        {
                            Property1 = $"A-C-{i}",
                            B = new ComplexClassB { Property1 = $"A-C-B-{i}" },
                        },
                    },
                    B = new ComplexClassB { Property1 = $"Root-B-{i}" },
                },
                null,
                TestContext.Current.CancellationToken);
        }

        var query = Graph.Nodes<Class1>()
            .Where(c => c.Property1 == marker)
            .OrderBy(c => c.Property2);

        var streamed = new List<Class1>();
        await foreach (var entity in query.WithCancellation(TestContext.Current.CancellationToken))
        {
            streamed.Add(entity);
        }

        var buffered = await query.ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(buffered.Count, streamed.Count);
        Assert.Equal(3, streamed.Count);

        for (var i = 0; i < buffered.Count; i++)
        {
            AssertComplexEntity(buffered[i], streamed[i]);
        }

        static void AssertComplexEntity(Class1 expected, Class1 actual)
        {
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.Property1, actual.Property1);
            Assert.Equal(expected.Property2, actual.Property2);

            Assert.NotNull(expected.A);
            Assert.NotNull(actual.A);
            Assert.Equal(expected.A.Property1, actual.A.Property1);
            Assert.Equal(expected.A.Property2, actual.A.Property2);

            Assert.NotNull(expected.A.B);
            Assert.NotNull(actual.A.B);
            Assert.Equal(expected.A.B.Property1, actual.A.B.Property1);

            Assert.NotNull(expected.A.C);
            Assert.NotNull(actual.A.C);
            Assert.Equal(expected.A.C.Property1, actual.A.C.Property1);

            Assert.NotNull(expected.A.C.B);
            Assert.NotNull(actual.A.C.B);
            Assert.Equal(expected.A.C.B.Property1, actual.A.C.B.Property1);

            Assert.NotNull(expected.B);
            Assert.NotNull(actual.B);
            Assert.Equal(expected.B.Property1, actual.B.Property1);
        }
    }
}
