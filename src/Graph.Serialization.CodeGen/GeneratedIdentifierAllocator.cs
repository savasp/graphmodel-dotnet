// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Serialization.CodeGen;


/// <summary>
/// Allocates deterministic generated local names without colliding with names already emitted in
/// the same C# scope. Preferred names remain unchanged unless a collision requires a numeric suffix.
/// </summary>
internal sealed class GeneratedIdentifierAllocator
{
    private readonly HashSet<string> allocatedNames;

    public GeneratedIdentifierAllocator(params string[] reservedNames)
    {
        allocatedNames = new HashSet<string>(reservedNames, StringComparer.Ordinal);
    }

    public string Allocate(string preferredName)
    {
        var candidate = preferredName;
        var suffix = 1;

        while (!allocatedNames.Add(candidate))
        {
            candidate = $"{preferredName}{suffix}";
            suffix++;
        }

        return Utils.EscapeIdentifier(candidate);
    }
}
