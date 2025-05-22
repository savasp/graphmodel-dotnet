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
        private readonly Type _rootType;
        private readonly Type _elementType;
        private readonly IGraphTransaction? _transaction;

        public Neo4jExpressionVisitor(Neo4jGraphProvider provider, Type rootType, Type elementType, IGraphTransaction? transaction)
        {
            _provider = provider;
            _rootType = rootType;
            _elementType = elementType;
            _transaction = transaction;
        }

        public string Translate(Expression expression)
        {
            // Cypher query builder with support for Where, Select, OrderBy, Take, navigation, and Neo4j functions
            var label = GetLabel(_rootType);
            var varName = "n";
            string? whereClause = null;
            string? orderByClause = null;
            string? returnClause = $"RETURN {varName}";
            string? limitClause = null;
            string? matchClause = null;

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
                        // Support simple and anonymous projections, and navigation
                        if (lambda.Body is MemberExpression me)
                        {
                            // Simple property projection: n.Prop AS Prop
                            var propName = me.Member.Name;
                            returnClause = $"RETURN {varName}.{propName} AS {propName}";
                        }
                        else if (lambda.Body is NewExpression ne)
                        {
                            // Anonymous type projection: new { FullName = p.FirstName, ... }
                            var members = ne.Members ?? new System.Collections.ObjectModel.ReadOnlyCollection<System.Reflection.MemberInfo>(new System.Reflection.MemberInfo[0]);
                            var props = new List<string>();
                            for (int i = 0; i < ne.Arguments.Count; i++)
                            {
                                var arg = ne.Arguments[i];
                                var member = members[i];
                                if (arg is MemberExpression argMe)
                                {
                                    props.Add($"{varName}.{argMe.Member.Name} AS {member.Name}");
                                }
                                else if (arg is MethodCallExpression concatMce && concatMce.Method.Name == "Concat")
                                {
                                    // TODO: Support for string concatenation, e.g., new { FullName = p.FirstName + " " + p.LastName }
                                    // For now, fallback to empty string
                                    props.Add($"'' AS {member.Name}");
                                }
                                else
                                {
                                    // Fallback: try to ToString the argument
                                    props.Add($"{varName}.{member.Name} AS {member.Name}");
                                }
                            }
                            returnClause = $"RETURN {string.Join(", ", props)}";
                        }
                        else if (lambda.Body is MemberInitExpression mie)
                        {
                            // Support for new { ... } with initializers
                            var bindings = mie.Bindings.OfType<MemberAssignment>();
                            var props = string.Join(", ", bindings.Select(b => $"{varName}.{b.Member.Name} AS {b.Member.Name}"));
                            returnClause = $"RETURN {props}";
                        }
                        // TODO: Navigation/deep traversal: detect navigation property and generate MATCH/OPTIONAL MATCH for relationships
                    }
                    current = mce.Arguments[0];
                }
                else if (method == "SelectMany")
                {
                    // TODO: Support for navigation collections (deep traversal)
                    // This will require generating additional MATCH clauses and handling collections in hydration
                    // For now, break
                    break;
                }
                else
                {
                    // Not supported, break
                    break;
                }
            }

            var cypher = $"MATCH ({varName}:{label}) ";
            if (matchClause != null) cypher += matchClause + " ";
            if (whereClause != null) cypher += whereClause + " ";
            if (orderByClause != null) cypher += orderByClause + " ";
            if (limitClause != null) cypher += limitClause + " ";
            cypher += returnClause;
            return cypher;
        }

        public object ExecuteQuery(string cypher, Type elementType)
        {
            Console.WriteLine($"Executing Cypher: {cypher}");
            // Block execution!
            return this.ExecuteQueryAsync(cypher, elementType).Result;
        }

        private async Task<object> ExecuteQueryAsync(string cypher, Type elementType)
        {
            var results = await _provider.ExecuteCypher(cypher, null, _transaction);

            // Helper: is primitive or string
            static bool IsSimpleType(Type t) => t.IsPrimitive || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(Guid);

            // If elementType is a simple type, just extract the value from the record
            if (IsSimpleType(elementType))
            {
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
                foreach (var record in results.Select(r => r as IRecord).Where(r => r != null))
                {
                    foreach (var val in record!.Values.Values)
                    {
                        if (val is string s)
                            list.Add(s);
                        else if (val != null && val.GetType() == elementType)
                            list.Add(val);
                        else if (val is IList valueList && valueList.Count > 0 && valueList[0]?.GetType() == elementType)
                            list.Add(valueList[0]);
                    }
                }
                return list;
            }

            // If elementType is an anonymous type (has CompilerGeneratedAttribute and is not a node/relationship)
            if (elementType.IsClass && elementType.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false) && !typeof(Neo4jDriver.INode).IsAssignableFrom(elementType) && !typeof(Neo4jDriver.IRelationship).IsAssignableFrom(elementType))
            {
                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
                var ctor = elementType.GetConstructors().FirstOrDefault();
                var ctorParams = ctor?.GetParameters();
                foreach (var record in results.Select(r => r as IRecord).Where(r => r != null))
                {
                    var args = new object?[ctorParams!.Length];
                    for (int i = 0; i < ctorParams.Length; i++)
                    {
                        var param = ctorParams[i];
                        // Try to match by name (case-insensitive)
                        var kvp = record!.Values.FirstOrDefault(kv => string.Equals(kv.Key, param.Name, StringComparison.OrdinalIgnoreCase));
                        object? value = kvp.Value;
                        if (value == null)
                        {
                            args[i] = param.ParameterType.IsValueType && Nullable.GetUnderlyingType(param.ParameterType) == null
                                ? Activator.CreateInstance(param.ParameterType)
                                : null;
                        }
                        else if (param.ParameterType.IsClass && value is Neo4jDriver.INode nodeVal)
                        {
                            // Navigation: hydrate related node as the parameter type
                            var navConvertToGraphEntityMethodName = nameof(SerializationExtensions.ConvertToGraphEntity);
                            var navMethod = typeof(SerializationExtensions).GetMethod(
                                    navConvertToGraphEntityMethodName,
                                    BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
                                ?? throw new GraphProviderException($"{navConvertToGraphEntityMethodName} method not found");
                            var navConvertToGraphEntity = navMethod.MakeGenericMethod(param.ParameterType);
                            args[i] = navConvertToGraphEntity.Invoke(null, new object[] { nodeVal });
                        }
                        else
                        {
                            args[i] = Convert.ChangeType(value, param.ParameterType);
                        }
                    }
                    var anon = ctor!.Invoke(args);
                    list.Add(anon);
                }
                return list;
            }

            // Default: hydrate as node/relationship
            var convertToGraphEntityMethodName = nameof(SerializationExtensions.ConvertToGraphEntity);
            var method = typeof(SerializationExtensions).GetMethod(
                    convertToGraphEntityMethodName,
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new GraphProviderException($"{convertToGraphEntityMethodName} method not found");
            var convertToGraphEntity = method.MakeGenericMethod(elementType);

            var entityList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
            foreach (var record in results.Select(r => r as IRecord).Where(r => r != null))
            {
                if (record!.TryGetValue("n", out var nodeValue) && nodeValue is Neo4jDriver.INode node)
                {
                    var entity = convertToGraphEntity.Invoke(null, new object[] { node });
                    entityList.Add(entity);
                }
                else if (record.TryGetValue("r", out var relValue) && relValue is Neo4jDriver.IRelationship rel)
                {
                    var entity = convertToGraphEntity.Invoke(null, new object[] { rel });
                    entityList.Add(entity);
                }
            }
            return entityList;
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
