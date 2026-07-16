// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

internal enum LinqOperator
{
    Where,
    Select,
    OrderBy,
    OrderByDescending,
    ThenBy,
    ThenByDescending,
    Take,
    Skip,
    Distinct,
    ToListOrArray,
    First,
    Single,
    Last,
    Any,
    All,
    Count,
    Sum,
    Average,
    Min,
    Max,
    Contains,
    ElementAt,
    ElementAtOrDefault,
    SelectMany,
    GroupBy,
    Join,
    Union,
    PathSegments,
    TraversePaths,
    ShortestPath,
    AllShortestPaths,
    Direction,
    WithDepth,
    Search,
    RelationshipPredicate,
    WhereHasRelationship,
}
