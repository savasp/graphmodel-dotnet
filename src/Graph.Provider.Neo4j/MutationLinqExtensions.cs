using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Cvoya.Graph.Provider.Model;

namespace Cvoya.Graph.Client.Neo4j
{
    public static class MutationLinqExtensions
    {
        // Create node
        public static async Task Create<T>(this IGraphProvider client, Expression<Func<T>> nodeFactory) where T : class
        {
            if (nodeFactory.Body is not MemberInitExpression init)
                throw new NotSupportedException("Only object initializers are supported in Create.");
            var label = typeof(T).FullName ?? typeof(T).Name;
            var props = init.Bindings
                .OfType<MemberAssignment>()
                .ToDictionary(b => b.Member.Name, b => Expression.Lambda(b.Expression).Compile().DynamicInvoke());
            var propCypher = string.Join(", ", props.Select(kv => $"{kv.Key}: ${kv.Key}"));
            var cypher = $"CREATE (n:`{label}` {{ {propCypher} }})";
            await client.ExecuteCypher(cypher, props);
        }

        // Update node
        public static async Task Update<T>(this IGraphProvider client, Expression<Func<T, bool>> filter, Expression<Func<T, T>> update) where T : class
        {
            if (update.Body is not MemberInitExpression init)
                throw new NotSupportedException("Only object initializers are supported in Update.");
            var label = typeof(T).FullName ?? typeof(T).Name;
            var setProps = init.Bindings
                .OfType<MemberAssignment>()
                .ToDictionary(b => b.Member.Name, b => Expression.Lambda(b.Expression, update.Parameters).Compile().DynamicInvoke(null));
            var setCypher = string.Join(", ", setProps.Select(kv => $"n.{kv.Key} = ${kv.Key}"));
            var filterCypher = CypherExpressionTranslator.ParseWhere(filter.Body);
            var cypher = $"MATCH (n:`{label}`) WHERE {filterCypher} SET {setCypher}";
            await client.ExecuteCypher(cypher, setProps);
        }

        // Delete node
        public static async Task Delete<T>(this IGraphProvider client, Expression<Func<T, bool>> filter) where T : class
        {
            var label = typeof(T).FullName ?? typeof(T).Name;
            var filterCypher = CypherExpressionTranslator.ParseWhere(filter.Body);
            var cypher = $"MATCH (n:`{label}`) WHERE {filterCypher} DETACH DELETE n";
            await client.ExecuteCypher(cypher);
        }

        // Create relationship
        public static async Task CreateRelationship<TSource, TTarget>(
            this IGraphProvider client,
            Expression<Func<TSource>> sourceFactory,
            Expression<Func<TTarget>> targetFactory,
            string relationshipType,
            object? relationshipProperties = null)
            where TSource : class
            where TTarget : class
        {
            if (sourceFactory.Body is not MemberInitExpression sourceInit)
                throw new NotSupportedException("Only object initializers are supported in CreateRelationship for source.");
            if (targetFactory.Body is not MemberInitExpression targetInit)
                throw new NotSupportedException("Only object initializers are supported in CreateRelationship for target.");
            var sourceLabel = typeof(TSource).FullName ?? typeof(TSource).Name;
            var targetLabel = typeof(TTarget).FullName ?? typeof(TTarget).Name;
            var sourceProps = sourceInit.Bindings
                .OfType<MemberAssignment>()
                .ToDictionary(b => b.Member.Name, b => Expression.Lambda(b.Expression).Compile().DynamicInvoke());
            var targetProps = targetInit.Bindings
                .OfType<MemberAssignment>()
                .ToDictionary(b => b.Member.Name, b => Expression.Lambda(b.Expression).Compile().DynamicInvoke());
            var sourceMatch = string.Join(" AND ", sourceProps.Select(kv => $"s.{kv.Key} = $source_{kv.Key}"));
            var targetMatch = string.Join(" AND ", targetProps.Select(kv => $"t.{kv.Key} = $target_{kv.Key}"));
            var parameters = sourceProps.ToDictionary(kv => $"source_{kv.Key}", kv => kv.Value)
                .Concat(targetProps.ToDictionary(kv => $"target_{kv.Key}", kv => kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            string relPropsCypher = string.Empty;
            if (relationshipProperties != null)
            {
                var relProps = relationshipProperties.GetType().GetProperties()
                    .ToDictionary(p => p.Name, p => p.GetValue(relationshipProperties));
                relPropsCypher = " { " + string.Join(", ", relProps.Select(kv => $"{kv.Key}: $rel_{kv.Key}")) + " }";
                foreach (var kv in relProps)
                    parameters[$"rel_{kv.Key}"] = kv.Value;
            }
            var cypher = $"MATCH (s:`{sourceLabel}`), (t:`{targetLabel}`) WHERE {sourceMatch} AND {targetMatch} CREATE (s)-[r:`{relationshipType}`{relPropsCypher}]->(t)";
            await client.ExecuteCypher(cypher, parameters);
        }

        // Update relationship
        public static async Task UpdateRelationship<TSource, TTarget>(
            this IGraphProvider client,
            Expression<Func<TSource, bool>> sourceFilter,
            Expression<Func<TTarget, bool>> targetFilter,
            string relationshipType,
            object updatedProperties)
            where TSource : class
            where TTarget : class
        {
            var sourceLabel = typeof(TSource).FullName ?? typeof(TSource).Name;
            var targetLabel = typeof(TTarget).FullName ?? typeof(TTarget).Name;
            var sourceFilterCypher = CypherExpressionTranslator.ParseWhere(sourceFilter.Body);
            var targetFilterCypher = CypherExpressionTranslator.ParseWhere(targetFilter.Body);
            var relProps = updatedProperties.GetType().GetProperties()
                .ToDictionary(p => p.Name, p => p.GetValue(updatedProperties));
            var setCypher = string.Join(", ", relProps.Select(kv => $"r.{kv.Key} = $rel_{kv.Key}"));
            var parameters = relProps.ToDictionary(kv => $"rel_{kv.Key}", kv => kv.Value);
            var cypher = $"MATCH (s:`{sourceLabel}`)-[r:`{relationshipType}`]->(t:`{targetLabel}`) WHERE {sourceFilterCypher} AND {targetFilterCypher} SET {setCypher}";
            await client.ExecuteCypher(cypher, parameters);
        }

        // Delete relationship
        public static async Task DeleteRelationship<TSource, TTarget>(
            this IGraphProvider client,
            Expression<Func<TSource, bool>> sourceFilter,
            Expression<Func<TTarget, bool>> targetFilter,
            string relationshipType)
            where TSource : class
            where TTarget : class
        {
            var sourceLabel = typeof(TSource).FullName ?? typeof(TSource).Name;
            var targetLabel = typeof(TTarget).FullName ?? typeof(TTarget).Name;
            var sourceFilterCypher = CypherExpressionTranslator.ParseWhere(sourceFilter.Body);
            var targetFilterCypher = CypherExpressionTranslator.ParseWhere(targetFilter.Body);
            var cypher = $"MATCH (s:`{sourceLabel}`)-[r:`{relationshipType}`]->(t:`{targetLabel}`) WHERE {sourceFilterCypher} AND {targetFilterCypher} DELETE r";
            await client.ExecuteCypher(cypher);
        }

        // Advanced: Create relationship with nested/deep relationship filtering
        public static async Task CreateRelationshipWithFilter<TSource, TTarget>(
            this IGraphProvider client,
            Expression<Func<TSource, bool>> sourceFilter,
            Expression<Func<TTarget, bool>> targetFilter,
            string relationshipType,
            object? relationshipProperties = null)
            where TSource : class
            where TTarget : class
        {
            var sourceLabel = typeof(TSource).FullName ?? typeof(TSource).Name;
            var targetLabel = typeof(TTarget).FullName ?? typeof(TTarget).Name;
            var sourceFilterCypher = CypherExpressionTranslator.ParseWhere(sourceFilter.Body);
            var targetFilterCypher = CypherExpressionTranslator.ParseWhere(targetFilter.Body);
            var parameters = new System.Collections.Generic.Dictionary<string, object?>();
            string relPropsCypher = string.Empty;
            if (relationshipProperties != null)
            {
                var relProps = relationshipProperties.GetType().GetProperties()
                    .ToDictionary(p => p.Name, p => p.GetValue(relationshipProperties));
                relPropsCypher = " { " + string.Join(", ", relProps.Select(kv => $"{kv.Key}: $rel_{kv.Key}")) + " }";
                foreach (var kv in relProps)
                    parameters[$"rel_{kv.Key}"] = kv.Value;
            }
            var cypher = $"MATCH (s:`{sourceLabel}`), (t:`{targetLabel}`) WHERE {sourceFilterCypher} AND {targetFilterCypher} CREATE (s)-[r:`{relationshipType}`{relPropsCypher}]->(t)";
            await client.ExecuteCypher(cypher, parameters);
        }

        // Advanced: Delete relationships with nested/deep relationship filtering
        public static async Task DeleteRelationshipsWithDeepFilter<TSource, TTarget>(
            this IGraphProvider client,
            Expression<Func<TSource, bool>> sourceFilter,
            Expression<Func<TTarget, bool>> targetFilter,
            string relationshipType,
            Expression<Func<TSource, TTarget, bool>>? relationshipFilter = null)
            where TSource : class
            where TTarget : class
        {
            var sourceLabel = typeof(TSource).FullName ?? typeof(TSource).Name;
            var targetLabel = typeof(TTarget).FullName ?? typeof(TTarget).Name;
            var sourceFilterCypher = CypherExpressionTranslator.ParseWhere(sourceFilter.Body);
            var targetFilterCypher = CypherExpressionTranslator.ParseWhere(targetFilter.Body);
            string relFilterCypher = string.Empty;
            if (relationshipFilter != null)
            {
                relFilterCypher = CypherExpressionTranslator.ParseWhere(relationshipFilter.Body);
            }
            var whereClause = $"{sourceFilterCypher} AND {targetFilterCypher}" + (string.IsNullOrWhiteSpace(relFilterCypher) ? "" : $" AND {relFilterCypher}");
            var cypher = $"MATCH (s:`{sourceLabel}`)-[r:`{relationshipType}`]->(t:`{targetLabel}`) WHERE {whereClause} DELETE r";
            await client.ExecuteCypher(cypher);
        }
    }
}
