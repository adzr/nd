/*
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

using Nd.Core.Extensions;
using Nd.Core.Types.Names;
using Nd.Core.Types.Versions;
using System.Collections.ObjectModel;

namespace Nd.Core.Types
{
    public static class Definitions
    {
        public static readonly IReadOnlyDictionary<string, Type> NamedTypes;

        public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<uint, Type>> VersionedTypes;

        public static Type[] GetAllImplementations<T>() => AppDomain
                .CurrentDomain
                .GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(T).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract && t.IsPublic)
                .ToArray();

        static Definitions()
        {
            var types = GetAllImplementations<INamedType>();

            NamedTypes = types.Where(t => !typeof(IVersionedType).IsAssignableFrom(t))
                           .GroupBy(t => t.GetName())
                           .Select(ValidateNameDuplicates)
                           .ToDictionary(g => g.Key, nameGroup => nameGroup.Single());

            VersionedTypes = types.Where(t => typeof(IVersionedType).IsAssignableFrom(t))
                       .Select(ResolveTypesNamesAndVersions)
                       .GroupBy(t => t.Name)
                       .Select(ValidateVersionConflictsInGroup)
                       .ToDictionary(g => g.Key, nameGroup => (IReadOnlyDictionary<uint, Type>)new ReadOnlyDictionary<uint, Type>(nameGroup
                           .GroupBy(g => g.Version)
                           .ToDictionary(g => g.Key, versionGroup => versionGroup.Single().Type)));
        }

        private static IGrouping<string, Type> ValidateNameDuplicates(IGrouping<string, Type> type)
        {
            if (type.Count() > 1)
            {
                throw new ApplicationException($"Multiple definitions of unversioned type name \"{type.Key}\"");
            }

            return type;
        }

        private static IGrouping<string, (string Name, uint Version, Type Type)> ValidateVersionConflictsInGroup(IGrouping<string, (string Name, uint Version, Type Type)> types)
        {
            if (types.Any(t => t.Version == 0u))
            {
                throw new ApplicationException($"Definition of type name \"{types.Key}\" has a version number of value equals 0");
            }

            if (types.Count() > types.Select(t => t.Version).Distinct().Count())
            {
                throw new ApplicationException($"Multiple definitions of type name \"{types.Key}\" with similar version numbers");
            }

            return types;
        }

        private static (string Name, uint Version, Type Type) ResolveTypesNamesAndVersions(Type type)
        {
            var (name, version) = type.GetNameAndVersion();

            return (name, version, type);
        }
    }
}