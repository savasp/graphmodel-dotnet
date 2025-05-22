using System;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Cvoya.Graph.Provider.Model;
using Cvoya.Graph.Provider.Neo4j;
using Neo4j.Driver;
using Neo4jDriver = Neo4j.Driver;

namespace Cvoya.Graph.Provider.Neo4j.Linq
{
    public class Neo4jExpressionVisitor : ExpressionVisitor
    {
        private readonly Neo4jGraphProvider _provider;
        private readonly Type _elementType;
        private readonly IGraphTransaction? _transaction;

        public Neo4jExpressionVisitor(Neo4jGraphProvider provider, Type elementType, IGraphTransaction? transaction)
        {
            _provider = provider;
            _elementType = elementType;
            _transaction = transaction;
        }

        public string Translate(Expression expression)
        {
            // Cypher query builder with support for Where, Select, OrderBy, Take, navigation, and Neo4j functions
            var label = GetLabel(_elementType);
            var varName = "n";
            string? whereClause = null;
            string? orderByClause = null;
            string? returnClause = $"RETURN {varName}";
            string? limitClause = null;

            Expression current = expression;
            LambdaExpression? selectLambda = null;
            int? takeCount = null;

            // Walk the expression tree for supported LINQ methods
            while (current is MethodCallExpression mce)
            {
                var method = mce.Method.Name;
                if (method == "Where")
                {
                    if (mce.Arguments[1] is UnaryExpression ue && ue.Operand is LambdaExpression lambda)
                    {
                        if (lambda.Body is BinaryExpression be)
                        {
                            // Only support ==, !=, >, <, >=, <= for now
                            string op = be.NodeType switch
                            {
                                ExpressionType.Equal => "=",
                                ExpressionType.NotEqual => "!=",
                                ExpressionType.GreaterThan => ">",
                                ExpressionType.LessThan => "<",
                                ExpressionType.GreaterThanOrEqual => ">=",
                                ExpressionType.LessThanOrEqual => "<=",
                                _ => throw new NotSupportedException($"Operator {be.NodeType} not supported")
                            };
                            if (be.Left is MemberExpression me && be.Right is ConstantExpression ce)
                            {
                                var propName = me.Member.Name;
                                var value = ce.Value;
                                whereClause = $"WHERE {varName}.{propName} {op} '{value}'";
                            }
                            else if (be.Right is MemberExpression me2 && be.Left is ConstantExpression ce2)
                            {
                                var propName = me2.Member.Name;
                                var value = ce2.Value;
                                whereClause = $"WHERE {varName}.{propName} {op} '{value}'";
                            }
                        }
                        // TODO: Add support for logical AND/OR, method calls (e.g. string functions)
                    }
                    current = mce.Arguments[0];
                }
                else if (method == "OrderBy" || method == "OrderByDescending")
                {
                    if (mce.Arguments[1] is UnaryExpression ue && ue.Operand is LambdaExpression lambda && lambda.Body is MemberExpression me)
                    {
                        var propName = me.Member.Name;
                        var dir = method == "OrderBy" ? "ASC" : "DESC";
                        orderByClause = $"ORDER BY {varName}.{propName} {dir}";
                    }
                    current = mce.Arguments[0];
                }
                else if (method == "Take")
                {
                    if (mce.Arguments[1] is ConstantExpression ce && ce.Value is int count)
                    {
                        takeCount = count;
                        limitClause = $"LIMIT {count}";
                    }
                    current = mce.Arguments[0];
                }
                else if (method == "Select")
                {
                    if (mce.Arguments[1] is UnaryExpression ue && ue.Operand is LambdaExpression lambda)
                    {
                        selectLambda = lambda;
                        // For now, only support simple projections: n.Prop or new { n.Prop1, n.Prop2 }
                        if (lambda.Body is MemberExpression me)
                        {
                            var propName = me.Member.Name;
                            returnClause = $"RETURN {varName}.{propName}";
                        }
                        else if (lambda.Body is NewExpression ne)
                        {
                            var members = ne.Members != null ? ne.Members : new System.Collections.ObjectModel.ReadOnlyCollection<System.Reflection.MemberInfo>(new System.Reflection.MemberInfo[0]);
                            var props = string.Join(", ", members.Select(m => $"{varName}.{m.Name}"));
                            returnClause = $"RETURN {props}";
                        }
                    }
                    current = mce.Arguments[0];
                }
                else
                {
                    // Not supported, break
                    break;
                }
            }

            var cypher = $"MATCH ({varName}:{label}) ";
            if (whereClause != null) cypher += whereClause + " ";
            if (orderByClause != null) cypher += orderByClause + " ";
            if (limitClause != null) cypher += limitClause + " ";
            cypher += returnClause;
            return cypher;
        }

        public object ExecuteQuery(string cypher, Type elementType)
        {
            // Block execution!
            return this.ExecuteQueryAsync(cypher, elementType).Result;
        }

        private async Task<object> ExecuteQueryAsync(string cypher, Type elementType)
        {
            var results = await _provider.ExecuteCypher(cypher, null, _transaction);

            var convertToGraphEntityMethodName = nameof(SerializationExtensions.ConvertToGraphEntity);
            var method = typeof(SerializationExtensions).GetMethod(
                    convertToGraphEntityMethodName,
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new GraphProviderException($"{convertToGraphEntityMethodName} method not found");
            var convertToGraphEntity = method.MakeGenericMethod(elementType);

            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
            foreach (var record in results.Select(r => r as IRecord).Where(r => r != null))
            {
                if (record!["n"] is Neo4jDriver.INode node)
                {
                    var entity = convertToGraphEntity.Invoke(null, [node]);
                    list.Add(entity);
                }
                else if (record["r"] is Neo4jDriver.IRelationship rel)
                {
                    var entity = convertToGraphEntity.Invoke(null, [rel]);
                    list.Add(entity);
                }
            }
            return list;
        }

        private static string GetLabel(Type type)
        {
            var getLabelMethodName = nameof(Neo4jGraphProvider.GetLabel);
            var method = typeof(Neo4jGraphProvider).GetMethod(
                    getLabelMethodName,
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                ?? throw new InvalidOperationException($"{getLabelMethodName} method not found");
            return (string)method.Invoke(null, [type])!;
        }
    }
}
