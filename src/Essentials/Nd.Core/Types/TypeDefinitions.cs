﻿/*
 * Copyright © 2022 Ahmed Zaher
 * https://github.com/adzr/Nd
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

using System;
using System.Collections.Generic;
using System.Linq;
using Nd.Core.Exceptions;
using Nd.Core.Extensions;
using Nd.Core.Types.Names;

namespace Nd.Core.Types
{
    public static class TypeDefinitions
    {
        public static readonly (string Name, uint Version, Type Type)[] Catalog =
            GetAllImplementations<INamedType>()
            .Select(ResolveTypesNamesAndVersions)
            .GroupBy(g => g.Name)
            .Select(ValidateUniqueVersionSequences)
            .SelectMany(g => g.ToList())
            .Select(r => (r.Name, r.Version, r.Type))
            .ToArray();

        public static readonly IDictionary<Type, (string Name, uint Version)> TypesNamesAndVersions = Catalog
            .ToDictionary(r => r.Type, r => (r.Name, r.Version));

        public static readonly IDictionary<(string name, uint Version), Type> NamesAndVersionsTypes = Catalog
            .ToDictionary(r => (r.Name, r.Version), r => r.Type);

        public static (string Name, uint Version) ResolveNameAndVersion(Type type) =>
            TypesNamesAndVersions.TryGetValue(type, out (string TypeName, uint TypeVersion) entry) ?
            entry : throw new TypeDefinitionNotFouNdCoreException($"Definition of type has no Name or Version defined: {type.ToPrettyString()}");

        public static Type[] GetAllImplementations<T>() => AppDomain
            .CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(T).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract && !t.IsGenericType)
            .ToArray();

        private static (string Name, uint Version, Type Type) ResolveTypesNamesAndVersions(Type type)
        {
            var (name, version) = type.ResolveNameAndVersion();
            return (name, version, type);
        }

        private static IGrouping<string, (string Name, uint Version, Type Type)> ValidateUniqueVersionSequences(IGrouping<string, (string Name, uint Version, Type Type)> types)
        {
            var duplicates = types
                .GroupBy(g => g.Version)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();

            return duplicates.Any()
                ? throw new TypeDefinitionConflictException($"Multiple definitions of type name \"{types.Key}\" with similar version numbers {{{string.Join(", ", duplicates)}}}")
                : types.Any(t => t.Version == 0) && types.Count() > 1
                ? throw new TypeDefinitionConflictException($"Multiple definitions of type name \"{types.Key}\" with some of them missing version numbers")
                : types;
        }
    }
}
