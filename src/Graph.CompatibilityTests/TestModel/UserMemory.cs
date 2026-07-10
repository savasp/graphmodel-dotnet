// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

using Cvoya.Graph;

[Relationship("MEMORY")]
public record UserMemory : Relationship
{
    public UserMemory() : base(string.Empty, string.Empty) { }

    public UserMemory(string userId, string memoryId) : base(userId, memoryId) { }

    public UserMemory(User user, Memory memory) : base(user.Id, memory.Id) { }
}
