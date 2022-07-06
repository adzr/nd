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

namespace Nd.Extensions.Stores.MongoDB.Aggregates
{
    public class MongoAggregateDocument
    {
        [BsonConstructor("_id", "Name", "Version", "Events")]
        public MongoAggregateDocument(object identity, string name, uint version, IEnumerable<MongoAggregateEventDocument> events)
        {
            Identity = identity;
            Name = name;
            Version = version;
            Events = events;
        }

        [BsonId]
        public object Identity { get; }

        [BsonElement("Name")]
        public string Name { get; }

        [BsonElement("Version")]
        public uint Version { get; }

        [BsonElement("Events")]
        [BsonIgnoreIfDefault]
        public IEnumerable<MongoAggregateEventDocument> Events { get; }
    }

    public class MongoAggregateEventDocument
    {
        [BsonConstructor(
            "Id",
            "Timestamp",
            "Version",
            "CorrelationId",
            "IdempotencyId",
            "TypeName",
            "TypeVersion",
            "Content")]
        public MongoAggregateEventDocument(
            Guid identity,
            DateTimeOffset timestamp,
            uint aggregateVersion,
            Guid correlationIdentity,
            Guid idempotencyIdentity,
            string typeName,
            uint typeVersion,
            BsonDocument content)
        {
            Identity = identity;
            Timestamp = timestamp;
            AggregateVersion = aggregateVersion;
            CorrelationIdentity = correlationIdentity;
            IdempotencyIdentity = idempotencyIdentity;
            TypeName = typeName;
            TypeVersion = typeVersion;
            Content = content;
        }

        [BsonElement("Id")]
        public Guid Identity { get; }

        [BsonElement("Timestamp")]
        public DateTimeOffset Timestamp { get; }

        [BsonElement("Version")]
        public uint AggregateVersion { get; }

        [BsonElement("CorrelationId")]
        public Guid CorrelationIdentity { get; }

        [BsonElement("IdempotencyId")]
        public Guid IdempotencyIdentity { get; }

        [BsonElement("TypeName")]
        public string TypeName { get; }

        [BsonElement("TypeVersion")]
        public uint TypeVersion { get; }

        [BsonElement("Content")]
        public BsonDocument Content { get; }
    }
}
