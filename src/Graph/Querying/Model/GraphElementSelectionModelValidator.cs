// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Cvoya.Graph.Querying;

using System.Linq.Expressions;

/// <summary>Validates the deliberately narrow query grammar accepted by graph commands.</summary>
public static class GraphElementSelectionModelValidator
{
    /// <summary>Validates a graph-element selection model.</summary>
    /// <param name="selection">The selection model.</param>
    public static void Validate(GraphElementSelectionModel selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var model = selection.Query;
        GraphQueryModelValidator.Validate(model);

        if (model.Root is SearchRoot { Target: SearchRootTarget.Entities })
        {
            throw Unsupported("mixed node-and-relationship search roots are not valid command targets");
        }

        if (model.Projection is not null)
        {
            throw Unsupported("Select and other projections are not entity-preserving command selections");
        }

        if (model.Traversal.Count > 0 || model.PathShape is not null)
        {
            throw Unsupported("traversal and path queries are not valid command selections");
        }

        if (model.Join is not null || model.GroupBy is not null || model.SelectMany is not null || model.Union is not null)
        {
            throw Unsupported("join, grouping, flattening, and set operators are not valid command selections");
        }

        if (model.Distinct)
        {
            throw Unsupported("Distinct is not valid in a command selection because native targets are deduplicated after the selection window");
        }

        if (model.PostPaging is not null)
        {
            throw Unsupported("operators after Skip or Take are not valid command selections");
        }

        if (model.Terminal != TerminalOperation.ToListOrArray ||
            model.TerminalOperand is not null || model.TerminalPredicate is not null)
        {
            throw Unsupported("aggregate, element, and predicate terminal operations are not valid command selections");
        }

        for (var index = 0; index < model.Ordering.Count; index++)
        {
            var selector = model.Ordering[index].KeySelector;
            var body = StripConvert(selector.Body);
            if (body is ParameterExpression || !GraphDataModel.IsSimple(body.Type))
            {
                throw Unsupported(
                    $"ordering key {index} must produce a scalar/property value rather than a whole entity or collection");
            }
        }
    }

    private static Expression StripConvert(Expression expression)
    {
        while (expression is UnaryExpression
            { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
        {
            expression = unary.Operand;
        }

        return expression;
    }

    private static GraphQueryTranslationException Unsupported(string reason) =>
        new($"Cannot use the query as a graph command selection: {reason}.");
}
