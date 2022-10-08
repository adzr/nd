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
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Nd.Aggregates.Identities;
using static Nd.Core.Extensions.TypeExtensions;

namespace Nd.Extensions.Stores.Mongo
{
    public class AggregateIdentitySerializer : SerializerBase<IAggregateIdentity?>
    {
        public override IAggregateIdentity? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args) =>
            throw new NotSupportedException($"Deserialization of abstract type {nameof(IAggregateIdentity)} is not supported");

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, IAggregateIdentity? value)
        {
            if (value is null)
            {
                context?.Writer.WriteNull();
                return;
            }

            BsonSerializer.LookupSerializer(value.Value.GetType()).Serialize(context, args, value.Value);
        }
    }

    public class AggregateIdentitySerializer<T> : SerializerBase<T?>
        where T : IAggregateIdentity
    {
        private readonly IBsonSerializer _valueSerializer;
        private readonly Constructor<T> _constructor;

        public AggregateIdentitySerializer(IBsonSerializer valueSerializer, Constructor<T> constructor)
        {
            _valueSerializer = valueSerializer ?? throw new ArgumentNullException(nameof(valueSerializer));
            _constructor = constructor ?? throw new ArgumentNullException(nameof(constructor));
        }

        public override T? Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args) =>
            _constructor(_valueSerializer.Deserialize(context, args));

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, T? value)
        {
            if (value is null)
            {
                context?.Writer.WriteNull();
                return;
            }

            _valueSerializer.Serialize(context, args, value.Value);
        }
    }
}
