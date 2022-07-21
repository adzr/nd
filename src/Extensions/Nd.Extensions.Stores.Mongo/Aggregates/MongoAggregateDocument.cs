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
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using Nd.Aggregates.Events;

namespace Nd.Extensions.Stores.Mongo.Aggregates
{
    public class MongoAggregateDocument<T> where T : notnull
    {
        [BsonId(IdGenerator = typeof(NullIdChecker))]
        public T? Id { get; set; }

        [BsonElement("Name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("Version")]
        public uint Version { get; set; }

        [BsonElement("Events")]
        [BsonIgnoreIfDefault]
        public IEnumerable<MongoAggregateEventDocument> Events { get; set; } = Array.Empty<MongoAggregateEventDocument>();
    }

    public class MongoAggregateEventDocument
    {
        [BsonElement("Id")]
        public Guid Id { get; set; }

        [BsonElement("Timestamp")]
        public DateTimeOffset Timestamp { get; set; }

        [BsonElement("AggregateVersion")]
        public uint AggregateVersion { get; set; }

        [BsonElement("CorrelationId")]
        public Guid CorrelationIdentity { get; set; }

        [BsonElement("IdempotencyId")]
        public Guid IdempotencyIdentity { get; set; }

        [BsonElement("Content")]
        public IAggregateEvent? Content { get; set; }
    }
}
