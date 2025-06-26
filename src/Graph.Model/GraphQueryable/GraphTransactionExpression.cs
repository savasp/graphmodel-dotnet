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

namespace Cvoya.Graph.Model;

using System.Linq.Expressions;


/// <summary>
/// Expression that wraps another expression with transaction context.
/// </summary>
public sealed class GraphTransactionExpression : Expression
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GraphTransactionExpression"/> class.
    /// </summary>
    /// <param name="innerExpression">The inner expression to wrap.</param>
    /// <param name="transaction">The transaction to associate with this expression.</param>
    public GraphTransactionExpression(Expression innerExpression, IGraphTransaction transaction)
    {
        InnerExpression = innerExpression ?? throw new ArgumentNullException(nameof(innerExpression));
        Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
    }

    /// <summary>
    /// Gets the inner expression that this transaction expression wraps.
    /// </summary>
    public Expression InnerExpression { get; }

    /// <summary>
    /// Gets the transaction associated with this expression.
    /// </summary>
    public IGraphTransaction Transaction { get; }

    /// <inheritdoc />
    public override ExpressionType NodeType => ExpressionType.Extension;
    /// <inheritdoc />
    public override Type Type => InnerExpression.Type;
    /// <inheritdoc />
    public override bool CanReduce => true;

    /// <inheritdoc />
    public override Expression Reduce() => InnerExpression;

    /// <inheritdoc />
    protected override Expression Accept(ExpressionVisitor visitor)
    {
        // This is the key - we need to let the visitor visit our inner expression
        // and create a new instance if it changed
        var visitedInner = visitor.Visit(InnerExpression);

        if (visitedInner != InnerExpression)
        {
            return new GraphTransactionExpression(visitedInner, Transaction);
        }

        return this;
    }

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        // Same as Accept - visit the inner expression
        var newInner = visitor.Visit(InnerExpression);
        return newInner != InnerExpression ? new GraphTransactionExpression(newInner, Transaction) : this;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        // Don't call InnerExpression.ToString() directly to avoid recursion
        // Just return a simple representation
        return $"WithTransaction({InnerExpression.NodeType})";
    }
}