/*
 * Copyright © 2015 - 2021 Rasmus Mikkelsen
 * Copyright © 2015 - 2021 eBay Software Foundation
 * Modified from original source https://github.com/eventflow/EventFlow
 * 
 * Copyright © 2022 Ahmed Zaher
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy 
 * of this software and associated documentation files (the "Software"), to deal 
 * in the Software without restriction, including without limitation the rights 
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
 * copies of the Software, and to permit persons to whom the Software is 
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all 
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE 
 * SOFTWARE.
 */

using Nd.Core.NamedTypes;
using Nd.Core.VersionedTypes;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Nd.Core.Extensions
{
    public static class TypeExtensions
    {
        private const int PrettyStringMaxDepth = 8;

        private static readonly ConcurrentDictionary<Type, string> ToPrettyStringCache = new();

        public static string ToPrettyString(this Type type, int maxDepth = PrettyStringMaxDepth) =>
            ToPrettyStringCache.GetOrAdd(type, t => ToPrettyStringRecursive(t, 0, maxDepth));

        private static string ToPrettyStringRecursive(Type type, int depth, int maxDepth)
        {
            if (depth > maxDepth)
            {
                return type.Name;
            }

            var typeNameFragments = GetTypeNameFragments(type);

            if (typeNameFragments.Length == 1)
            {
                return typeNameFragments[0];
            }

            Type[]? genericArguments;

            try
            {
                genericArguments = type.GetTypeInfo().GetGenericArguments();

                var isConstructedGenericType = type.IsConstructedGenericType;

                return @$"{typeNameFragments[0]}<{string.Join(",", genericArguments.Select(t =>
                !isConstructedGenericType ? string.Empty : ToPrettyStringRecursive(t, depth + 1, maxDepth)))}>";
            }
            catch
            {
                return type.Name;
            }
        }

        private static string[] GetTypeNameFragments(Type type) => type.Name.Split('`');

        public static Type[] GetInterfacesOfType<T>(this Type type) => type
            .GetTypeInfo()
            .GetInterfaces()
            .Where(t => typeof(T).IsAssignableFrom(t))
            .ToArray();

        public static Type[] GetGenericTypeArgumentsOfType<T>(this Type type) => type
            .GetTypeInfo()
            .GenericTypeArguments
            .Where(t => typeof(T).IsAssignableFrom(t))
            .ToArray();

        public static MethodInfo GetMethodWithSingleParameterOfType(this Type type, string name, Type param) => type
            .GetTypeInfo()
            .GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { param }) ?? throw new NotSupportedException(
                    $"Failed to find method with name \"{name}\" in type \"{type.ToPrettyString()}\" that takes a single parameter of type \"{param.ToPrettyString()}\"");

        public static bool HasMethodWithSingleParameterOfType(this Type type, string name, Type param) => type
            .GetTypeInfo()
            .GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { param }) is not null;

        public static string GetName(this Type type) =>
            type?
            .GetTypeInfo()
            .GetCustomAttributes<NamedTypeAttribute>()
            .FirstOrDefault()?.TypeName ??
            type?.AssemblyQualifiedName ??
            throw new ArgumentNullException(nameof(type));

        public static uint GetVersion(this Type type) =>
            type?
            .GetTypeInfo()
            .GetCustomAttributes<VersionedTypeAttribute>()
            .FirstOrDefault()?.TypeVersion ??
            throw new ArgumentNullException(nameof(type));


        public static T CompileMethodInvocation<T>(this MethodInfo methodInfo)
        {
            var genericArguments = typeof(T).GetTypeInfo().GetGenericArguments();
            var methodArgumentList = methodInfo.GetParameters().Select(p => p.ParameterType).ToList();
            var funcArgumentList = genericArguments.Skip(1).Take(methodArgumentList.Count).ToList();

            if (funcArgumentList.Count != methodArgumentList.Count)
            {
                throw new ArgumentException("Incorrect number of arguments");
            }

            var instanceArgument = Expression.Parameter(genericArguments.First());

            var argumentPairs = funcArgumentList.Zip(methodArgumentList, (s, d) => new { Source = s, Destination = d }).ToList();

            if (argumentPairs.All(a => a.Source == a.Destination))
            {
                // No need to do anything fancy, the types are the same.
                var parameters = funcArgumentList.Select(Expression.Parameter).ToList();
                return Expression.Lambda<T>(
                    Expression.Call(instanceArgument, methodInfo, parameters),
                    new[] { instanceArgument }.Concat(parameters)
                ).Compile();
            }

            var lambdaArgument = new List<ParameterExpression> { instanceArgument };

            var type = methodInfo.DeclaringType ?? throw new NullReferenceException($"Method info \"{methodInfo.Name}\" missing declaring type");

            var instanceVariable = Expression.Variable(type);

            var blockVariables = new List<ParameterExpression> { instanceVariable };

            var blockExpressions = new List<Expression> { Expression.Assign(instanceVariable, Expression.ConvertChecked(instanceArgument, type)) };

            var callArguments = new List<ParameterExpression>();

            foreach (var a in argumentPairs)
            {
                if (a.Source == a.Destination)
                {
                    var sourceParameter = Expression.Parameter(a.Source);
                    lambdaArgument.Add(sourceParameter);
                    callArguments.Add(sourceParameter);
                }
                else
                {
                    var sourceParameter = Expression.Parameter(a.Source);
                    var destinationVariable = Expression.Variable(a.Destination);
                    var assignToDestination = Expression.Assign(destinationVariable, Expression.Convert(sourceParameter, a.Destination));

                    lambdaArgument.Add(sourceParameter);
                    callArguments.Add(destinationVariable);
                    blockVariables.Add(destinationVariable);
                    blockExpressions.Add(assignToDestination);
                }
            }

            var callExpression = Expression.Call(instanceVariable, methodInfo, callArguments);

            blockExpressions.Add(callExpression);

            var block = Expression.Block(blockVariables, blockExpressions);

            var lambdaExpression = Expression.Lambda<T>(block, lambdaArgument);

            return lambdaExpression.Compile();
        }
    }
}
