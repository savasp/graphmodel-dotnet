// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying.Commands;

/// <summary>One opaque provider-native graph element reference scoped to an active command transaction.</summary>
internal sealed record SelectedGraphElement(GraphElementKind Kind, object NativeIdentity);
