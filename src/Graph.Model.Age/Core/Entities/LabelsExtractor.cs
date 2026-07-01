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

namespace Cvoya.Graph.Model.Age.Core.Entities;

using System;
using System.Collections.Generic;
using System.Linq;
using Cvoya.Graph.Model;
using Npgsql.Age.Types;

/// <summary>
/// Extracts labels from AGE vertices and edges, handling inheritance_labels
/// for class hierarchy support.
/// </summary>
internal static class LabelsExtractor
{
    public static IReadOnlyList<string> ExtractLabels(Vertex vertex)
        => ExtractLabels(vertex.Properties, vertex.Label);

    public static IReadOnlyList<string> ExtractLabels(Edge edge)
        => ExtractLabels(edge.Properties, edge.Label);

    private static IReadOnlyList<string> ExtractLabels(
        IReadOnlyDictionary<string, object> properties,
        string? label)
    {
        if (properties.TryGetValue("inheritance_labels", out var inheritanceValue))
        {
            return inheritanceValue switch
            {
                string[] stringArray => stringArray.ToList(),
                IList<object?> list => list.Select(v => v?.ToString() ?? string.Empty).Where(static v => !string.IsNullOrWhiteSpace(v)).ToList(),
                IEnumerable<string> stringList => stringList.ToList(),
                _ => []
            };
        }

        if (properties.TryGetValue(nameof(INode.Labels), out var value))
        {
            return value switch
            {
                IList<object?> list => list.Select(v => v?.ToString() ?? string.Empty).Where(static v => !string.IsNullOrWhiteSpace(v)).ToList(),
                IEnumerable<string> stringList => stringList.ToList(),
                _ => []
            };
        }

        if (!string.IsNullOrEmpty(label))
        {
            return label.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return [];
    }
}
