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

using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Nd.Aggregates.Events;
using Nd.Core.Types;

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
                    BsonSerializer.RegisterDiscriminatorConvention(typeof(IAggregateEvent), new NamedTypeDiscriminatorConvention());

                    var eventTypes = TypeDefinitions
                        .Catalog
                        .Select((v) => v.Type)
                        .Where(t => typeof(IAggregateEvent).IsAssignableFrom(t));

                    foreach (var type in eventTypes)
                    {
                        BsonSerializer.RegisterDiscriminatorConvention(type, new NamedTypeDiscriminatorConvention());
                    }

#pragma warning disable CS0618 // Type or member is obsolete
                    BsonDefaults.GuidRepresentationMode = GuidRepresentationMode.V3;
#pragma warning restore CS0618 // Type or member is obsolete
                }
            }
        }
    }
}
