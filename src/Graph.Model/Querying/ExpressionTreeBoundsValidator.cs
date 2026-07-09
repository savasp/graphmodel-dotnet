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

namespace Cvoya.Graph.Model.Querying;

using System.Linq.Expressions;

internal sealed class ExpressionTreeBoundsValidator : ExpressionVisitor
{
    private readonly int _maxNodeCount;
    private readonly int _maxDepth;
    private int _nodeCount;
    private int _depth;

    private ExpressionTreeBoundsValidator(GraphQueryModelBuilderOptions options)
    {
        _maxNodeCount = options.MaxNodeCount;
        _maxDepth = options.MaxDepth;
    }

    public static void Validate(Expression expression, GraphQueryModelBuilderOptions options)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(options);

        new ExpressionTreeBoundsValidator(options).Visit(expression);
    }

    public override Expression? Visit(Expression? node)
    {
        if (node is null)
        {
            return null;
        }

        _nodeCount++;
        if (_nodeCount > _maxNodeCount)
        {
            throw new GraphQueryTranslationException(
                $"The graph query expression contains more than the configured maximum of {_maxNodeCount} nodes. " +
                "Simplify the query or increase GraphQueryModelBuilderOptions.MaxNodeCount.");
        }

        _depth++;
        if (_depth > _maxDepth)
        {
            throw new GraphQueryTranslationException(
                $"The graph query expression is deeper than the configured maximum depth of {_maxDepth}. " +
                "Simplify the query or increase GraphQueryModelBuilderOptions.MaxDepth.");
        }

        try
        {
            return base.Visit(node);
        }
        finally
        {
            _depth--;
        }
    }

    protected override Expression VisitExtension(Expression node)
    {
        return node.CanReduce ? base.VisitExtension(node) : node;
    }
}
