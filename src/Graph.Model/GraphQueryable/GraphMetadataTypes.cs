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

namespace Cvoya.Graph.Model;

/// <summary>
/// Flags enum for specifying what metadata to include in query results
/// </summary>
[Flags]
public enum GraphMetadataTypes
{
    /// <summary>No additional metadata</summary>
    None = 0,

    /// <summary>Include entity IDs</summary>
    Ids = 1,

    /// <summary>Include entity labels/types</summary>
    Labels = 2,

    /// <summary>Include relationship directions</summary>
    Directions = 4,

    /// <summary>Include creation timestamps</summary>
    CreatedAt = 8,

    /// <summary>Include last modified timestamps</summary>
    ModifiedAt = 16,

    /// <summary>Include query execution statistics</summary>
    Statistics = 32,

    /// <summary>Include all available metadata</summary>
    All = Ids | Labels | Directions | CreatedAt | ModifiedAt | Statistics
}

