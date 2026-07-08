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

namespace Cvoya.Graph.Model.CompatibilityTests;

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
