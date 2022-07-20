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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Nd.Aggregates.Events;
using Nd.Aggregates.Extensions;
using Nd.Aggregates.Identities;
using Nd.Aggregates.Persistence;
using Nd.Extensions.Stores.Mongo.Exceptions;
using Nd.Identities;

namespace Nd.Extensions.Stores.Mongo.Aggregates
{
    public abstract class MongoDBAggregateEventReader<TIdentity, TValue> : MongoAccessor, IAggregateEventReader<TIdentity>
        where TIdentity : notnull, IAggregateIdentity
        where TValue : notnull
    {
        private readonly ILogger? _logger;
        private readonly ActivitySource _activitySource;

        private static readonly Action<ILogger, string, Exception?> s_mongoResultReceived =
            LoggerMessage.Define<string>(LogLevel.Trace, new EventId(
                                MongoLoggingEventsConstants.MongoResultReceived,
                                nameof(MongoLoggingEventsConstants.MongoResultReceived)),
                                "Mongo document received {Result}");

        private static readonly Action<ILogger, Exception?> s_mongoResultMissing =
            LoggerMessage.Define(LogLevel.Trace, new EventId(
                                MongoLoggingEventsConstants.MongoResultMissing,
                                nameof(MongoLoggingEventsConstants.MongoResultMissing)),
                                "Mongo document not found");

        static MongoDBAggregateEventReader()
        {
            BsonDefaultsInitializer.Initialize();
            BsonDefaultsInitializer.RegisterMongoAggregateDocument<TValue>();
        }

        protected MongoDBAggregateEventReader(
            MongoClient client,
            string databaseName,
            string collectionName,
            ILoggerFactory? loggerFactory,
            ActivitySource? activitySource) :
            base(client, databaseName, collectionName)
        {
            _logger = loggerFactory?.CreateLogger(GetType());
            _activitySource = activitySource ?? new ActivitySource(GetType().Name);
        }

        public async Task<IEnumerable<TEvent>> ReadAsync<TEvent>(TIdentity aggregateId, ICorrelationIdentity correlationId, uint versionStart, uint versionEnd, CancellationToken cancellation = default)
            where TEvent : ICommittedEvent<TIdentity>
        {
            if (aggregateId is null)
            {
                throw new ArgumentNullException(nameof(aggregateId));
            }

            using var activity = _activitySource.StartActivity();

            _ = activity?.AddDomainAggregatesTag(new[] { aggregateId });

            cancellation.ThrowIfCancellationRequested();

            return await FindEvents<TEvent>(aggregateId, correlationId, CreateIdentity, GetCollection<MongoAggregateDocument<TValue>>(), _logger, cancellation).ConfigureAwait(false);
        }

        private static async Task<IEnumerable<TEvent>> FindEvents<TEvent>(TIdentity aggregateId, ICorrelationIdentity correlationId, Func<object, TIdentity> createIdentity, IMongoCollection<MongoAggregateDocument<TValue>> collection, ILogger? logger, CancellationToken cancellation)
            where TEvent : ICommittedEvent<TIdentity>
        {
            var events = new List<CommittedEvent<TIdentity>>();

            using var cursor = await collection
                .FindAsync<MongoAggregateDocument<TValue>>(
                Builders<MongoAggregateDocument<TValue>>.Filter.Eq(d => d.Id, aggregateId.Value),
                cancellationToken: cancellation).ConfigureAwait(false);

            var documemt = await cursor.SingleOrDefaultAsync(cancellation).ConfigureAwait(false);

            using var correlationIdScope = logger.WithCorrelationId(correlationId);
            using var aggregateIdScope = logger.WithAggregateId(aggregateId);

            if (documemt is not null)
            {
                if (logger is not null)
                {
                    s_mongoResultReceived(logger, documemt.ToJson(), default);
                }

                if (documemt.Events is not null)
                {
                    foreach (var e in documemt.Events)
                    {
                        if (e.Content is null)
                        {
                            throw new MongoReaderEventDefinitionException("Null event content found, cannot resolve type and version");
                        }

                        events.Add(MapEvent(e, documemt, createIdentity));
                    }
                }
            }
            else if (logger is not null)
            {
                s_mongoResultMissing(logger, default);
            }

            return (IEnumerable<TEvent>)events;
        }

        private static CommittedEvent<TIdentity> MapEvent(
            MongoAggregateEventDocument e,
            MongoAggregateDocument<TValue> documemt,
            Func<object, TIdentity> createIdentity) =>
            new(e.Content!, new AggregateEventMetadata<TIdentity>(
                        new IdempotencyIdentity(e.IdempotencyIdentity),
                        new CorrelationIdentity(e.CorrelationIdentity),
                        new AggregateEventIdentity(e.Id),
                        e.Content!.TypeName,
                        e.Content.TypeVersion,
                        createIdentity(documemt.Id!),
                        documemt.Name,
                        e.AggregateVersion,
                        e.Timestamp));

        protected abstract TIdentity CreateIdentity(object value);
    }


    internal class CommittedEvent<TIdentity> : ICommittedEvent<TIdentity>
        where TIdentity : IAggregateIdentity
    {
        public CommittedEvent(IAggregateEvent aggregateEvent, IAggregateEventMetadata<TIdentity> metadata)
        {
            AggregateEvent = aggregateEvent;
            Metadata = metadata;
        }

        public IAggregateEvent AggregateEvent { get; }

        public IAggregateEventMetadata<TIdentity> Metadata { get; }
    }
}
