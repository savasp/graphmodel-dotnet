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

namespace Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Core;

using System.Collections.Concurrent;
using System.Reflection;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Builders;
using Cvoya.Graph.Model.Neo4j.Querying.Cypher.Visitors.Expressions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Default implementation of visitor factory.
/// </summary>
internal class VisitorFactory
{
    private readonly CypherQueryContext _context;
    private readonly ConcurrentDictionary<Type, Func<object>> _factoryMethods = new();

    public VisitorFactory(CypherQueryContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        RegisterDefaultVisitors();
    }

    public TVisitor Create<TVisitor>() where TVisitor : class
    {
        var visitor = Create(typeof(TVisitor));
        return (TVisitor)visitor;
    }

    public object Create(Type visitorType)
    {
        if (_factoryMethods.TryGetValue(visitorType, out var factory))
        {
            return factory();
        }

        // Try to create using reflection as fallback
        var constructor = FindBestConstructor(visitorType);
        if (constructor != null)
        {
            var parameters = CreateConstructorParameters(constructor);
            var factoryMethod = () => Activator.CreateInstance(visitorType, parameters)!;

            // Cache for next time
            _factoryMethods[visitorType] = factoryMethod;

            return factoryMethod();
        }

        throw new GraphException(
            $"Cannot create visitor of type {visitorType.Name}. " +
            "No suitable constructor found.");
    }

    /// <summary>
    /// Registers a custom factory method for a visitor type.
    /// </summary>
    public void Register<TVisitor>(Func<TVisitor> factory) where TVisitor : class
    {
        _factoryMethods[typeof(TVisitor)] = () => factory();
    }

    private void RegisterDefaultVisitors()
    {
        // Register clause visitors
        Register(() => new DistinctVisitor(_context));
        Register(() => new GroupByVisitor(_context));
        Register(() => new OrderByVisitor(_context));
        Register(() => new SelectVisitor(_context));
        Register(() => new WhereVisitor(_context));

        // Register Execution visitors
        Register(() => new FirstVisitor(_context));
        Register(() => new SelectManyVisitor(_context));

        // Register expression visitors
        Register(() => new ExpressionVisitorChain(_context));
    }

    private ConstructorInfo? FindBestConstructor(Type visitorType)
    {
        var constructors = visitorType.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .ToList();

        foreach (var constructor in constructors)
        {
            if (CanCreateWithConstructor(constructor))
            {
                return constructor;
            }
        }

        return null;
    }

    private bool CanCreateWithConstructor(ConstructorInfo constructor)
    {
        foreach (var parameter in constructor.GetParameters())
        {
            if (!CanProvideParameter(parameter.ParameterType))
            {
                return false;
            }
        }
        return true;
    }

    private bool CanProvideParameter(Type parameterType)
    {
        return parameterType == typeof(CypherQueryScope) ||
               parameterType == typeof(CypherQueryBuilder) ||
               parameterType == typeof(ILoggerFactory) ||
               parameterType == typeof(ICypherExpressionVisitor);
    }

    private object?[] CreateConstructorParameters(ConstructorInfo constructor)
    {
        var parameters = constructor.GetParameters();
        var values = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            var paramType = parameters[i].ParameterType;

            if (paramType == typeof(CypherQueryScope))
                values[i] = _context.Scope;
            else if (paramType == typeof(CypherQueryBuilder))
                values[i] = _context.Builder;
            else if (paramType == typeof(ICypherExpressionVisitor) || paramType == typeof(ICypherExpressionVisitor))
                values[i] = null; // For chain of responsibility pattern
            else
                throw new GraphException($"Cannot provide parameter of type {paramType.Name} for visitor constructor");
        }

        return values;
    }
}