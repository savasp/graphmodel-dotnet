// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Neo4j.Translation.Tests;

using System.Runtime.CompilerServices;
using VerifyTests;
using VerifyXunit;

/// <summary>
/// One-time setup for Verify snapshot testing.
/// </summary>
public static class VerifyModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifierSettings.UseUtf8NoBom();
        Verifier.DerivePathInfo(
            (sourceFile, projectDirectory, type, method) => new PathInfo(
                directory: Path.Join(projectDirectory, "Snapshots"),
                typeName: type.Name,
                methodName: method.Name));
    }
}
