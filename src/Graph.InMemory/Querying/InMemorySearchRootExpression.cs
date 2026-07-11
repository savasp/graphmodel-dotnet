// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.InMemory.Querying;

using System.Linq.Expressions;
using Cvoya.Graph.Querying;

/// <summary>
/// The root expression behind <see cref="IGraph.Search"/>-style entry points. The shared query
/// model builder recognizes it through <see cref="IGraphSearchRootExpression"/> and produces a
/// <c>SearchRoot</c>; execution then fails with a clear <see cref="GraphException"/> because the
/// in-memory provider does not declare the <see cref="GraphCapability.FullTextSearch"/>
/// capability. Building the queryable itself performs no work, matching the public contract.
/// </summary>
internal sealed class InMemorySearchRootExpression(
    string searchQuery,
    Type entityType,
    SearchRootTarget target) : Expression, IGraphSearchRootExpression
{
    public string SearchQuery { get; } = searchQuery;

    public Type EntityType { get; } = entityType;

    public SearchRootTarget Target { get; } = target;

    public override ExpressionType NodeType => ExpressionType.Extension;

    public override Type Type => typeof(IGraphQueryable<>).MakeGenericType(EntityType);
}
