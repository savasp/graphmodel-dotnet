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

using System.Linq.Expressions;

namespace Cvoya.Graph.Model;

/// <summary>
/// Represents a transaction attachment in the expression tree.
/// </summary>
public sealed class GraphTransactionExpression : Expression
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphTransactionExpression"/> class.
    /// </summary>
    /// <param name="source">The source expression to which the transaction is attached.</param>
    /// <param name="transaction">The transaction to associate with the source expression.</param>
    public GraphTransactionExpression(Expression source, IGraphTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(transaction);

        Source = source;
        Transaction = transaction;
    }

    /// <summary>
    /// Gets the source expression that this transaction is attached to.
    /// </summary>
    public Expression Source { get; }

    /// <summary>
    /// Gets the transaction associated with this expression.
    /// </summary>
    public IGraphTransaction Transaction { get; }

    /// <summary>
    /// Gets the node type as Extension since this is a custom expression type.
    /// </summary>
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <summary>
    /// Gets the type of the expression, which matches the source expression's type.
    /// </summary>
    public override Type Type => Source.Type;

    /// <summary>
    /// Accepts a visitor for this expression. Since this is a custom expression type,
    /// most visitors won't know how to handle it and will just return this expression unchanged.
    /// Providers that understand this expression type can check for it explicitly.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <returns>This expression, potentially modified by the visitor.</returns>
    protected override Expression Accept(ExpressionVisitor visitor)
    {
        // Let the visitor decide what to do with this expression
        // Most visitors will just return it unchanged
        return visitor.Visit(this);
    }

    /// <summary>
    /// Visits the children of this expression, allowing for modifications to the source expression.
    /// If the source expression is modified, a new <see cref="GraphTransactionExpression"/> is returned with the updated source.
    /// If no modifications are made, the current instance is returned.
    /// </summary>
    /// <param name="visitor">The visitor to use for visiting child expressions.</param>
    /// <returns>
    /// A new <see cref="GraphTransactionExpression"/> with the updated source if modifications were made,
    /// or the current instance if no modifications were necessary.
    /// </returns>
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        var newSource = visitor.Visit(Source);
        if (newSource != Source)
        {
            return new GraphTransactionExpression(newSource, Transaction);
        }

        return this;
    }
}