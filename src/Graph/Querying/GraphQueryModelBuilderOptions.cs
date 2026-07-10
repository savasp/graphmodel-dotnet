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

namespace Cvoya.Graph.Querying;

internal sealed record GraphQueryModelBuilderOptions
{
    public const int DefaultMaxNodeCount = 10_000;
    public const int DefaultMaxDepth = 100;

    public GraphQueryModelBuilderOptions(
        int maxNodeCount = DefaultMaxNodeCount,
        int maxDepth = DefaultMaxDepth)
    {
        if (maxNodeCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxNodeCount), "The maximum expression node count must be positive.");
        }

        if (maxDepth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "The maximum expression depth must be positive.");
        }

        MaxNodeCount = maxNodeCount;
        MaxDepth = maxDepth;
    }

    public int MaxNodeCount { get; }

    public int MaxDepth { get; }
}
