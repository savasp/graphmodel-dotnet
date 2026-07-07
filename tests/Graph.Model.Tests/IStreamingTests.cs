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

namespace Cvoya.Graph.Model.Tests;

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

        var query = Graph.Nodes<Person>()
            .Where(p => p.FirstName == marker)
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

        var count = await query.CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(5, count);
    }
}
