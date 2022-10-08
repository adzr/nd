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

using System;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using Nd.Aggregates.Events;
using Nd.Aggregates.Identities;
using Nd.Commands;
using Nd.Commands.Results;
using Nd.Core.Extensions;
using Nd.Core.Types;
using Nd.Identities;

namespace Nd.Extensions.Stores.Mongo
{
    public static class BsonDefaultsInitializer
    {
        private static bool s_initialized;
        private static readonly object s_lock = new();

        public static void Initialize()
        {
            lock (s_lock)
            {
                if (!s_initialized)
                {
                    s_initialized = true;

                    BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
                    BsonSerializer.RegisterSerializer(new DateTimeOffsetSerializer());
                    RegisterAggregateIdentitySerializers();
                    RegisterNamedTypesDiscriminatorConvention();

#pragma warning disable CS0618 // Type or member is obsolete
                    BsonDefaults.GuidRepresentationMode = GuidRepresentationMode.V3;
#pragma warning restore CS0618 // Type or member is obsolete
                }
            }
        }

        private static void RegisterNamedTypesDiscriminatorConvention() =>
            RegisterDiscriminatorConvention(
                new NamedTypeDiscriminatorConvention(),
                typeof(IAggregateEvent),
                typeof(ICommand),
                typeof(IExecutionResult),
                typeof(IAggregateIdentity),
                typeof(IIdempotencyIdentity),
                typeof(ICorrelationIdentity));

        private static void RegisterAggregateIdentitySerializers()
        {
            BsonSerializer.RegisterSerializer(new AggregateIdentitySerializer());

            foreach (var type in TypeDefinitions.GetAllImplementations<IAggregateIdentity>())
            {
                var valueType = type.GetProperty("Value")?.PropertyType ??
                    throw new TypeAccessException($"Failed to resolve type of 'Value' property of type {type.ResolveName()}");

                BsonSerializer.RegisterSerializer(type, new AggregateIdentitySerializer<IAggregateIdentity>(BsonSerializer.LookupSerializer(valueType),
                    type.GetConstructor(new[] { valueType })?.CompileConstructor<IAggregateIdentity>() ??
                    throw new TypeAccessException($"Failed to find a public constructor for identity of type {type.ResolveName()} that accepts an argument of type {valueType.ResolveName()}")));
            }
        }

        private static void RegisterDiscriminatorConvention(IDiscriminatorConvention convention, params Type[] types)
        {
            var filter = (Type[])types.Clone();

            types = types
                .Union(TypeDefinitions
                        .Catalog
                        .Select(r => r.Type)
                        .Where(t => filter.Any(f => f.IsAssignableFrom(t))))
                .ToArray();

            var pack = new ConventionPack();

            pack.AddClassMapConvention("AlwaysApplyDiscriminatorToNamedTypes", m => m.SetDiscriminatorIsRequired(true));

            ConventionRegistry.Register("NamedTypesConventionPack", pack, t => types.Contains(t));

            foreach (var type in types)
            {
                BsonSerializer.RegisterDiscriminatorConvention(type, convention);
            }
        }
    }
}
