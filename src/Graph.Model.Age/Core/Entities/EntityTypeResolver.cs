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

/// <summary>
/// Resolves .NET types from AGE vertex/edge labels for class hierarchy support.
/// </summary>
internal static class EntityTypeResolver
{
    /// <summary>
    /// Resolves the most derived type from the available labels.
    /// When querying by base type (e.g., Person), if the stored label corresponds to
    /// a derived type (e.g., Manager), we need to deserialize as the derived type.
    /// First tries from inheritance_labels (most derived label in the hierarchy),
    /// then falls back to the vertex/edge label.
    /// </summary>
    public static Type ResolveType(Type targetType, string label, IReadOnlyList<string> inheritanceLabels)
    {
        var typeResolutionLabel = inheritanceLabels.FirstOrDefault() ?? label;
        if (string.IsNullOrWhiteSpace(typeResolutionLabel))
            return targetType;

        try
        {
            Type? mostDerived;

            // If the target type is an interface (e.g., INode), resolve the concrete
            // type directly from the label.
            if (targetType.IsInterface)
            {
                try
                {
                    mostDerived = Labels.GetTypeFromLabel(typeResolutionLabel);
                }
                catch
                {
                    mostDerived = null;
                }
            }
            else
            {
                mostDerived = Labels.GetMostDerivedType(targetType, typeResolutionLabel);
            }

            if (mostDerived != null)
                return mostDerived;
        }
        catch
        {
            // If resolution fails, fall back to the original target type
        }

        return targetType;
    }
}
