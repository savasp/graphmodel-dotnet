// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying.Commands;

/// <summary>Internal endpoint intent consumed by provider relationship-create commands.</summary>
internal abstract record GraphEndpointIntent;

/// <summary>An endpoint already frozen to one provider-native node identity.</summary>
internal sealed record SelectedGraphEndpoint(SelectedGraphElement Element) : GraphEndpointIntent;

/// <summary>A new endpoint to create in the command transaction.</summary>
internal sealed record NewGraphEndpoint(INode Node) : GraphEndpointIntent;
