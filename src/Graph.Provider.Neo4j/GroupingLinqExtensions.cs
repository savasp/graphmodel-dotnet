using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Cvoya.Graph.Provider.Model;

namespace Cvoya.Graph.Client.Neo4j
{
    public static class GroupingLinqExtensions
    {
        // GroupBy extension for Cypher translation
        public static async Task<List<(TKey Key, long Count)>> GroupByCypher<TSource, TKey>(this IGraphProvider client, Expression<Func<TSource, bool>> filter, Expression<Func<TSource, TKey>> keySelector)
        {
            if (keySelector.Body is not MemberExpression keyMember)
                throw new NotSupportedException("Only grouping by a single property is supported.");
            var label = typeof(TSource).FullName ?? typeof(TSource).Name;
            var keyProp = keyMember.Member.Name;
            var filterCypher = CypherExpressionTranslator.ParseWhere(filter.Body);
            var cypher = $"MATCH (n:`{label}`) WHERE {filterCypher} WITH n.{keyProp} AS key, count(n) AS count RETURN key, count";
            var results = await client.ExecuteCypher(cypher);
            var list = new List<(TKey, long)>();
            foreach (var row in results)
            {
                var dict = (IDictionary<string, object>)row;
                var key = (TKey)Convert.ChangeType(dict["key"], typeof(TKey));
                var count = Convert.ToInt64(dict["count"]);
                list.Add((key, count));
            }
            return list;
        }

        // GroupBy with multi-key and aggregation projection
        public static async Task<List<TResult>> GroupByCypher<TSource, TKey, TResult>(
            this IGraphProvider client,
            Expression<Func<TSource, bool>> filter,
            Expression<Func<TSource, TKey>> keySelector,
            Expression<Func<IGrouping<TKey, TSource>, TResult>> resultSelector)
        {
            // Support grouping by multiple properties (anonymous type or tuple)
            var label = typeof(TSource).FullName ?? typeof(TSource).Name;
            var filterCypher = CypherExpressionTranslator.ParseWhere(filter.Body);
            string keyCypher;
            string keyReturn;
            if (keySelector.Body is NewExpression newExpr)
            {
                // Anonymous type: new { n.Prop1, n.Prop2 }
                var fields = newExpr.Arguments
                    .Select((arg, i) =>
                    {
                        if (arg is MemberExpression mem)
                            return (mem.Member.Name, $"n.{mem.Member.Name}");
                        return ($"key{i}", arg.ToString());
                    }).ToList();
                keyCypher = string.Join(", ", fields.Select(f => $"{f.Item2} AS {f.Item1}"));
                keyReturn = string.Join(", ", fields.Select(f => f.Item1));
            }
            else if (keySelector.Body is MemberExpression keyMember)
            {
                keyCypher = $"n.{keyMember.Member.Name} AS {keyMember.Member.Name}";
                keyReturn = keyMember.Member.Name;
            }
            else
            {
                throw new NotSupportedException("Only grouping by properties or anonymous types is supported.");
            }

            // Parse resultSelector for supported aggregations
            // Only support: Count(), Sum(x => x.Prop), Avg(x => x.Prop), Min, Max
            var aggCypher = new List<string>();
            if (resultSelector.Body is NewExpression resNewExpr)
            {
                foreach (var arg in resNewExpr.Arguments)
                {
                    if (arg is MethodCallExpression mce)
                    {
                        if (mce.Method.Name == "Count")
                            aggCypher.Add("count(n) AS Count");
                        else if (mce.Method.Name == "Sum")
                        {
                            var prop = ((LambdaExpression)mce.Arguments[0]).Body as MemberExpression;
                            aggCypher.Add($"sum(n.{prop?.Member.Name}) AS Sum_{prop?.Member.Name}");
                        }
                        else if (mce.Method.Name == "Average" || mce.Method.Name == "Avg")
                        {
                            var prop = ((LambdaExpression)mce.Arguments[0]).Body as MemberExpression;
                            aggCypher.Add($"avg(n.{prop?.Member.Name}) AS Avg_{prop?.Member.Name}");
                        }
                        else if (mce.Method.Name == "Min")
                        {
                            var prop = ((LambdaExpression)mce.Arguments[0]).Body as MemberExpression;
                            aggCypher.Add($"min(n.{prop?.Member.Name}) AS Min_{prop?.Member.Name}");
                        }
                        else if (mce.Method.Name == "Max")
                        {
                            var prop = ((LambdaExpression)mce.Arguments[0]).Body as MemberExpression;
                            aggCypher.Add($"max(n.{prop?.Member.Name}) AS Max_{prop?.Member.Name}");
                        }
                        else
                            throw new NotSupportedException($"Aggregation {mce.Method.Name} not supported.");
                    }
                    else if (arg is MemberExpression mem)
                    {
                        aggCypher.Add($"n.{mem.Member.Name} AS {mem.Member.Name}");
                    }
                }
            }
            else
            {
                throw new NotSupportedException("Only anonymous type projections are supported for aggregation.");
            }
            var cypher = $"MATCH (n:`{label}`) WHERE {filterCypher} WITH {keyCypher}, n {string.Join(", ", aggCypher)} RETURN {keyReturn}, {string.Join(", ", aggCypher)}";
            var results = await client.ExecuteCypher(cypher);
            var list = new List<TResult>();
            foreach (var row in results)
            {
                var dict = (IDictionary<string, object>)row;
                // Use reflection to construct TResult (anonymous type or tuple)
                var ctor = typeof(TResult).GetConstructors().First();
                var ctorParams = ctor.GetParameters();
                var args = ctorParams.Select(p => dict[p.Name!]).ToArray();
                var result = (TResult)ctor.Invoke(args);
                list.Add(result);
            }
            return list;
        }
    }
}
