// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.CompatibilityTests;

/// <summary>
/// Thrown by an <see cref="IGraphProviderTestHarness"/> when the backing infrastructure it needs
/// (for example, a Docker-hosted database container) cannot be started or reached.
/// </summary>
/// <remarks>
/// <see cref="CompatibilityTest"/> renders this as a skip locally. When
/// <c>GRAPHMODEL_COMPLIANCE_STRICT=1</c> is set, it fails the run instead, so CI cannot silently
/// pass a compliance lane whose infrastructure never actually came up.
/// </remarks>
/// <param name="message">A description of why the infrastructure is unavailable.</param>
/// <param name="inner">The underlying exception, if any.</param>
public class GraphProviderUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);
