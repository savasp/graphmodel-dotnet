// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

internal interface IGraphSearchRootExpression
{
    string SearchQuery { get; }

    Type EntityType { get; }

    SearchRootTarget Target { get; }
}
