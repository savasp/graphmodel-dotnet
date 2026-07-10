// Copyright CVOYA. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.
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

namespace Cvoya.Graph.Neo4j.Querying.Cypher;

using System.Globalization;
using System.Text;
using Cvoya.Graph.Cypher.Ast;
using Cvoya.Graph.Cypher.Ast.Expressions;
using Cvoya.Graph.Cypher.Validation;
using Cvoya.Graph.Neo4j.Entities;

internal sealed class Neo4jCypherRenderer
{
    public CypherQuery Render(CypherStatement statement)
    {
        ArgumentNullException.ThrowIfNull(statement);

#if DEBUG
        new CypherAstValidator().Run(statement);
#endif

        var builder = new StringBuilder();
        foreach (var clause in statement.Clauses)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            RenderClause(builder, clause);
        }

        (Type Source, Type Relationship, Type Target)? pathTypes = statement.PathTypes is null
            ? null
            : (statement.PathTypes.Source, statement.PathTypes.Relationship, statement.PathTypes.Target);
        return new CypherQuery(builder.ToString().Trim(), statement.Parameters, pathTypes);
    }

    private static void RenderClause(StringBuilder builder, ICypherClause clause)
    {
        switch (clause)
        {
            case MatchClause match:
                builder.Append(match.Optional ? "OPTIONAL MATCH " : "MATCH ");
                builder.Append(string.Join(", ", match.Patterns.Select(RenderPattern)));
                break;

            case WhereClause where:
                builder.Append("WHERE ").Append(RenderExpression(where.Predicate));
                break;

            case WithClause with:
                builder.Append("WITH ");
                if (with.Distinct)
                {
                    builder.Append("DISTINCT ");
                }

                builder.Append(with.Wildcard ? "*" : string.Join(", ", with.Items.Select(RenderReturnItem)));
                break;

            case ReturnClause @return:
                builder.Append("RETURN ");
                if (@return.Distinct)
                {
                    builder.Append("DISTINCT ");
                }

                builder.Append(string.Join(", ", @return.Items.Select(RenderReturnItem)));
                break;

            case CallClause call:
                if (call.Procedure == "search.entities")
                {
                    RenderEntitySearch(builder, call);
                    break;
                }

                builder.Append("CALL ")
                    .Append(RenderProcedureName(call.Procedure))
                    .Append('(')
                    .Append(string.Join(", ", call.Arguments.Select(argument => RenderExpression(argument))))
                    .Append(')');
                if (call.Yields.Count > 0)
                {
                    builder.Append(" YIELD ")
                        .Append(string.Join(", ", call.Yields.Select(yield =>
                            yield.Alias is null ? yield.Name : $"{yield.Name} AS {yield.Alias}")));
                }

                break;

            case UnwindClause unwind:
                builder.Append("UNWIND ")
                    .Append(RenderExpression(unwind.Source))
                    .Append(" AS ")
                    .Append(unwind.Alias);
                break;

            case OrderByClause orderBy:
                builder.Append("ORDER BY ")
                    .Append(string.Join(", ", orderBy.Items.Select(item =>
                        RenderExpression(item.Expression) + (item.Descending ? " DESC" : string.Empty))));
                break;

            case SkipClause skip:
                builder.Append("SKIP ").Append(RenderExpression(skip.Count));
                break;

            case LimitClause limit:
                builder.Append("LIMIT ").Append(RenderExpression(limit.Count));
                break;

            case EntityProjectionClause projection:
                RenderEntityProjection(builder, projection);
                break;

            default:
                throw new GraphException($"Unsupported Cypher clause '{clause.GetType().Name}'.");
        }
    }

    private static string RenderPattern(PathPattern pattern)
    {
        var builder = new StringBuilder();
        if (pattern.Alias is not null)
        {
            builder.Append(pattern.Alias).Append(" = ");
        }

        for (var index = 0; index < pattern.Elements.Count; index++)
        {
            switch (pattern.Elements[index])
            {
                case NodePattern node:
                    builder.Append('(');
                    if (node.Alias is not null)
                    {
                        builder.Append(node.Alias);
                    }

                    if (node.Labels.Count > 0)
                    {
                        builder.Append(':').Append(string.Join('|', node.Labels));
                    }

                    builder.Append(')');
                    break;

                case RelationshipPattern relationship:
                    RenderRelationship(builder, relationship);
                    break;
            }
        }

        return builder.ToString();
    }

    private static void RenderRelationship(StringBuilder builder, RelationshipPattern relationship)
    {
        if (relationship.Direction == CypherDirection.Incoming)
        {
            builder.Append("<-");
        }
        else
        {
            builder.Append('-');
        }

        builder.Append('[');
        if (relationship.Alias is not null)
        {
            builder.Append(relationship.Alias);
        }

        if (relationship.Type is not null)
        {
            builder.Append(':').Append(relationship.Type);
        }

        if (relationship.Depth is not null)
        {
            builder.Append('*')
                .Append(relationship.Depth.Min)
                .Append("..")
                .Append(relationship.Depth.Max);
        }

        builder.Append(']');
        if (relationship.Direction == CypherDirection.Outgoing)
        {
            builder.Append("->");
        }
        else
        {
            builder.Append('-');
        }
    }

    private static string RenderReturnItem(ReturnItem item)
    {
        var expression = RenderExpression(item.Expression);
        return item.Alias is null ? expression : $"{expression} AS {item.Alias}";
    }

    private static string RenderExpression(CypherExpression expression, CypherBinaryOperator? parentOperator = null)
    {
        return expression switch
        {
            VariableRef variable => variable.Alias,
            PropertyAccess property => $"{RenderExpression(property.Target)}.{property.Property}",
            EscapedPropertyAccess property =>
                $"{RenderExpression(property.Target)}.{CypherIdentifier.Escape(property.Property, "property name")}",
            QueryParameter parameter => $"${parameter.Name}",
            Literal literal => RenderLiteral(literal.Value),
            FunctionCall function => RenderFunction(function),
            BinaryExpression binary => RenderBinary(binary, parentOperator),
            UnaryExpression unary => RenderUnary(unary),
            LabelTest label => RenderLabelTest(label),
            ListExpression list => $"[{string.Join(", ", list.Items.Select(item => RenderExpression(item)))}]",
            MapExpression map => $"{{ {string.Join(", ", map.Entries.Select(entry => $"{entry.Key}: {RenderExpression(entry.Value)}"))} }}",
            EntityProjectionExpression entity => RenderEntityProjectionExpression(entity),
            IndexExpression index => $"{RenderExpression(index.Target)}[{RenderExpression(index.Index)}]",
            CaseExpression @case => RenderCase(@case),
            ConjunctionExpression conjunction => string.Join(" AND ", conjunction.Predicates.Select(item => RenderExpression(item))),
            PatternSubqueryExpression subquery => RenderPatternSubquery(subquery),
            PatternComprehensionExpression comprehension => RenderPatternComprehension(comprehension),
            _ => throw new GraphException($"Unsupported Cypher expression '{expression.GetType().Name}'."),
        };
    }

    private static string RenderEntityProjectionExpression(EntityProjectionExpression expression)
    {
        if (!expression.LoadComplexProperties)
        {
            return $"{{ Node: {expression.Alias}, ComplexProperties: [] }}";
        }

        var alias = expression.Alias;
        return $@"{{ Node: {alias}, ComplexProperties: reduce(flat = [], propertyPath IN [
            ({alias})-[propertyRelationships*1..{GraphDataModel.DefaultDepthAllowed}]->(propertyNode)
            WHERE ALL(propertyRelationship IN propertyRelationships WHERE propertyRelationship.{ComplexPropertyStorage.RelationshipMarkerProperty} = true) |
            [i IN range(0, size(propertyRelationships) - 1) | {{
                ParentNode: startNode(propertyRelationships[i]),
                Relationship: propertyRelationships[i],
                SequenceNumber: propertyRelationships[i].SequenceNumber,
                Property: endNode(propertyRelationships[i])
            }}]
        ] | flat + propertyPath) }}";
    }

    private static string RenderFunction(FunctionCall function)
    {
        if (function.Name == "math.power" && function.Arguments.Count == 2)
        {
            return $"({RenderExpression(function.Arguments[0])} ^ {RenderExpression(function.Arguments[1])})";
        }

        var name = function.Name switch
        {
            "temporal.datetime" => "datetime",
            "temporal.localDateTime" => "localdatetime",
            "temporal.date" => "date",
            "temporal.time" => "time",
            "temporal.duration" => "duration",
            "string.join" => "apoc.text.join",
            "string.indexOf" => "apoc.text.indexOf",
            "string.lastIndexOf" => "apoc.text.lastIndexOf",
            "string.padLeft" => "apoc.text.lpad",
            "string.padRight" => "apoc.text.rpad",
            "string.compareTo" => "apoc.text.compareTo",
            _ => function.Name,
        };

        return $"{name}({string.Join(", ", function.Arguments.Select(argument => RenderExpression(argument)))})";
    }

    private static string RenderBinary(BinaryExpression binary, CypherBinaryOperator? parentOperator)
    {
        var op = binary.Op switch
        {
            CypherBinaryOperator.And => "AND",
            CypherBinaryOperator.Or => "OR",
            CypherBinaryOperator.Equal => "=",
            CypherBinaryOperator.NotEqual => "<>",
            CypherBinaryOperator.LessThan => "<",
            CypherBinaryOperator.LessThanOrEqual => "<=",
            CypherBinaryOperator.GreaterThan => ">",
            CypherBinaryOperator.GreaterThanOrEqual => ">=",
            CypherBinaryOperator.Add => "+",
            CypherBinaryOperator.Subtract => "-",
            CypherBinaryOperator.Multiply => "*",
            CypherBinaryOperator.Divide => "/",
            CypherBinaryOperator.Modulo => "%",
            CypherBinaryOperator.StartsWith => "STARTS WITH",
            CypherBinaryOperator.EndsWith => "ENDS WITH",
            CypherBinaryOperator.Contains => "CONTAINS",
            CypherBinaryOperator.In => "IN",
            CypherBinaryOperator.Matches => "=~",
            _ => throw new GraphException($"Unsupported Cypher binary operator '{binary.Op}'."),
        };

        var text = $"{RenderExpression(binary.Left, binary.Op)} {op} {RenderExpression(binary.Right, binary.Op)}";
        if (binary.Op is CypherBinaryOperator.And or CypherBinaryOperator.Or)
        {
            return $"({text})";
        }

        if (parentOperator is CypherBinaryOperator.Equal or CypherBinaryOperator.NotEqual or
            CypherBinaryOperator.LessThan or CypherBinaryOperator.LessThanOrEqual or
            CypherBinaryOperator.GreaterThan or CypherBinaryOperator.GreaterThanOrEqual &&
            binary.Op is CypherBinaryOperator.Add or CypherBinaryOperator.Subtract or
                CypherBinaryOperator.Multiply or CypherBinaryOperator.Divide or CypherBinaryOperator.Modulo)
        {
            return $"({text})";
        }

        return text;
    }

    private static string RenderUnary(UnaryExpression unary)
    {
        var operand = RenderExpression(unary.Operand);
        return unary.Op switch
        {
            CypherUnaryOperator.Not => $"NOT ({operand})",
            CypherUnaryOperator.IsNull => $"{operand} IS NULL",
            CypherUnaryOperator.IsNotNull => $"{operand} IS NOT NULL",
            // Parenthesized: -(a + b) must not render as -a + b.
            CypherUnaryOperator.Negate => $"-({operand})",
            _ => throw new GraphException($"Unsupported Cypher unary operator '{unary.Op}'."),
        };
    }

    private static string RenderLabelTest(LabelTest label)
    {
        var target = RenderExpression(label.Target);
        var conditions = label.Labels.Select(item => $"{RenderLiteral(item)} IN labels({target})").ToArray();
        return conditions.Length == 1 ? conditions[0] : $"({string.Join(" OR ", conditions)})";
    }

    private static string RenderCase(CaseExpression expression)
    {
        var builder = new StringBuilder()
            .Append("CASE WHEN ")
            .Append(RenderExpression(expression.Condition))
            .Append(" THEN ")
            .Append(RenderExpression(expression.WhenTrue));
        if (expression.WhenFalse is not null)
        {
            builder.Append(" ELSE ").Append(RenderExpression(expression.WhenFalse));
        }

        return builder.Append(" END").ToString();
    }

    private static string RenderPatternSubquery(PatternSubqueryExpression expression)
    {
        var keyword = expression.Kind == PatternSubqueryKind.Exists ? "EXISTS" : "COUNT";
        var predicate = expression.Predicate is null ? string.Empty : $" WHERE {RenderExpression(expression.Predicate)}";
        return $"{keyword} {{ MATCH {RenderPattern(expression.Pattern)}{predicate} }}";
    }

    private static string RenderPatternComprehension(PatternComprehensionExpression expression)
    {
        var predicate = expression.Predicate is null ? string.Empty : $" WHERE {RenderExpression(expression.Predicate)}";
        return $"[{RenderPattern(expression.Pattern)}{predicate} | {RenderExpression(expression.Projection)}]";
    }

    private static string RenderLiteral(object? value)
    {
        return value switch
        {
            null => "null",
            string text => RenderStringLiteral(text),
            char character => RenderStringLiteral(character.ToString()),
            bool boolean => boolean ? "true" : "false",
            float number => RenderFloating(number),
            double number => RenderFloating(number),
            decimal number => RenderFloating(number),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => throw new GraphException(
                $"Cannot render a literal of type '{value.GetType().Name}' into Cypher text; " +
                "pass the value as a query parameter instead."),
        };
    }

    private static string RenderStringLiteral(string text)
    {
        // Backslashes first: escaping quotes introduces backslashes that must not be re-escaped.
        var escaped = text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);
        return $"'{escaped}'";
    }

    private static string RenderFloating<T>(T number)
        where T : IFormattable
    {
        var text = number.ToString(null, CultureInfo.InvariantCulture);

        // Exponent notation is already a valid Cypher float literal; appending ".0" would corrupt it.
        if (text.Contains('.', StringComparison.Ordinal) || text.Contains('E', StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        var digits = text.StartsWith('-') ? text.AsSpan(1) : text.AsSpan();
        if (digits.Length > 0 && !digits.ContainsAnyExceptInRange('0', '9'))
        {
            return text + ".0";
        }

        // NaN and Infinity have no Cypher literal form.
        throw new GraphException($"Cannot render '{text}' as a Cypher float literal.");
    }

    private static string RenderProcedureName(string procedure) => procedure switch
    {
        "search.nodes" => "db.index.fulltext.queryNodes",
        "search.relationships" => "db.index.fulltext.queryRelationships",
        _ => procedure,
    };

    private static void RenderEntitySearch(StringBuilder builder, CallClause call)
    {
        builder.Append("CALL {\n")
            .Append("    CALL db.index.fulltext.queryNodes(")
            .Append(RenderExpression(call.Arguments[0])).Append(", ")
            .Append(RenderExpression(call.Arguments[2])).Append(") YIELD node\n")
            .Append("    RETURN node AS entity\n")
            .Append("    UNION ALL\n")
            .Append("    CALL db.index.fulltext.queryRelationships(")
            .Append(RenderExpression(call.Arguments[1])).Append(", ")
            .Append(RenderExpression(call.Arguments[2])).Append(") YIELD relationship\n")
            .Append("    MATCH (src)-[relationship]->(tgt)\n")
            .Append("    RETURN { StartNode: { Node: src, ComplexProperties: [] }, ")
            .Append("Relationship: relationship, EndNode: { Node: tgt, ComplexProperties: [] } } AS entity\n")
            .Append('}');
    }

    private static void RenderEntityProjection(StringBuilder builder, EntityProjectionClause projection)
    {
        if (projection.Shape == EntityProjectionShape.Node)
        {
            if (!projection.LoadSourceProperties)
            {
                RenderSimpleNodeProjection(builder, projection.SourceAlias);
                return;
            }

            RenderNodePropertyLoad(builder, projection.SourceAlias);
            return;
        }

        if (projection.IncludePathCoordinates)
        {
            builder.Append("RETURN pathIndex, hopIndex, { StartNode: { Node: ")
                .Append(projection.SourceAlias)
                .Append(", ComplexProperties: [] }, Relationship: ")
                .Append(projection.RelationshipAlias)
                .Append(", EndNode: { Node: ")
                .Append(projection.TargetAlias)
                .Append(", ComplexProperties: [] } } AS PathSegment");
            return;
        }

        if (!projection.LoadSourceProperties && !projection.LoadTargetProperties)
        {
            builder.Append("RETURN {\n")
                .Append("    StartNode: { Node: ").Append(projection.SourceAlias).Append(", ComplexProperties: [] },\n")
                .Append("    Relationship: ").Append(projection.RelationshipAlias).Append(",\n")
                .Append("    EndNode: { Node: ").Append(projection.TargetAlias).Append(", ComplexProperties: [] }\n")
                .Append("} AS PathSegment");
            return;
        }

        RenderPathSegmentPropertyLoad(builder, projection);
    }

    private static void RenderSimpleNodeProjection(StringBuilder builder, string alias)
    {
        builder.Append("RETURN {\n")
            .Append("                Node: ").Append(alias).Append(",\n")
            .Append("                ComplexProperties: []\n")
            .Append("            } AS Node");
    }

    private static void RenderNodePropertyLoad(StringBuilder builder, string alias)
    {
        builder.Append("OPTIONAL MATCH src_path = (").Append(alias)
            .Append(")-[rels*1..").Append(GraphDataModel.DefaultDepthAllowed).Append("]->(prop)\n")
            .Append("WHERE ALL(rel IN rels WHERE rel.").Append(ComplexPropertyStorage.RelationshipMarkerProperty).Append(" = true)\n")
            .Append("WITH ").Append(alias).Append(",\n")
            .Append("    CASE WHEN src_path IS NULL THEN [] ELSE [i IN range(0, size(rels) - 1) | {\n")
            .Append("        ParentNode: CASE WHEN i = 0 THEN ").Append(alias).Append(" ELSE nodes(src_path)[i] END,\n")
            .Append("        Relationship: rels[i],\n")
            .Append("        SequenceNumber: rels[i].SequenceNumber,\n")
            .Append("        Property: nodes(src_path)[i + 1]\n")
            .Append("    }] END AS src_property_path\n")
            .Append("WITH ").Append(alias)
            .Append(", reduce(flat = [], path IN collect(src_property_path) | flat + path) AS src_properties\n")
            .Append("RETURN {\n")
            .Append("    Node: ").Append(alias).Append(",\n")
            .Append("    ComplexProperties: src_properties\n")
            .Append("} AS Node");
    }

    private static void RenderPathSegmentPropertyLoad(StringBuilder builder, EntityProjectionClause projection)
    {
        var sourceAlias = projection.SourceAlias;
        var relationshipAlias = projection.RelationshipAlias!;
        var targetAlias = projection.TargetAlias!;

        if (projection.LoadSourceProperties)
        {
            AppendPathPropertyLoad(builder, sourceAlias, relationshipAlias, targetAlias, sourceAlias, "src", "rels", "prop");
            builder.AppendLine();
        }
        else
        {
            builder.Append("WITH ").Append(sourceAlias).Append(", ").Append(relationshipAlias).Append(", ")
                .Append(targetAlias).Append(", [] AS src_properties\n");
        }

        if (projection.LoadTargetProperties)
        {
            AppendPathPropertyLoad(builder, sourceAlias, relationshipAlias, targetAlias, targetAlias, "tgt", "trels", "tprop");
            builder.AppendLine();
        }
        else
        {
            builder.Append("WITH ").Append(sourceAlias).Append(", ").Append(relationshipAlias).Append(", ")
                .Append(targetAlias).Append(", src_properties, [] AS tgt_properties\n");
        }

        builder.Append("RETURN {\n")
            .Append("    StartNode: { Node: ").Append(sourceAlias).Append(", ComplexProperties: src_properties },\n")
            .Append("    Relationship: ").Append(relationshipAlias).Append(",\n")
            .Append("    EndNode: { Node: ").Append(targetAlias).Append(", ComplexProperties: tgt_properties }\n")
            .Append("} AS PathSegment");
    }

    private static void AppendPathPropertyLoad(
        StringBuilder builder,
        string sourceAlias,
        string relationshipAlias,
        string targetAlias,
        string ownerAlias,
        string prefix,
        string relationshipsAlias,
        string propertyAlias)
    {
        builder.Append("OPTIONAL MATCH ").Append(prefix).Append("_path = (").Append(ownerAlias)
            .Append(")-[").Append(relationshipsAlias).Append("*1..").Append(GraphDataModel.DefaultDepthAllowed)
            .Append("]->(").Append(propertyAlias).Append(")\n")
            .Append("WHERE ALL(rel IN ").Append(relationshipsAlias).Append(" WHERE rel.")
            .Append(ComplexPropertyStorage.RelationshipMarkerProperty).Append(" = true)\n")
            .Append("WITH ").Append(sourceAlias).Append(", ").Append(relationshipAlias).Append(", ")
            .Append(targetAlias);
        if (prefix == "tgt")
        {
            builder.Append(", src_properties");
        }

        builder.Append(", CASE WHEN ").Append(prefix).Append("_path IS NULL THEN [] ELSE [i IN range(0, size(")
            .Append(relationshipsAlias).Append(") - 1) | {\n")
            .Append("    ParentNode: CASE WHEN i = 0 THEN ").Append(ownerAlias).Append(" ELSE nodes(")
            .Append(prefix).Append("_path)[i] END,\n")
            .Append("    Relationship: ").Append(relationshipsAlias).Append("[i],\n")
            .Append("    SequenceNumber: ").Append(relationshipsAlias).Append("[i].SequenceNumber,\n")
            .Append("    Property: nodes(").Append(prefix).Append("_path)[i + 1]\n")
            .Append("}] END AS ").Append(prefix).Append("_property_path\n")
            .Append("WITH ").Append(sourceAlias).Append(", ").Append(relationshipAlias).Append(", ")
            .Append(targetAlias);
        if (prefix == "tgt")
        {
            builder.Append(", src_properties");
        }

        builder.Append(", reduce(flat = [], path IN collect(").Append(prefix)
            .Append("_property_path) | flat + path) AS ").Append(prefix).Append("_properties\n");
    }
}
