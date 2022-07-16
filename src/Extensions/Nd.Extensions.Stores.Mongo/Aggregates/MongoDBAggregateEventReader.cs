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
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Nd.Aggregates.Events;
using Nd.Aggregates.Extensions;
using Nd.Aggregates.Identities;
using Nd.Aggregates.Persistence;
using Nd.Core.Types;
using Nd.Extensions.Stores.Mongo.Exceptions;
using Nd.Identities;

namespace Nd.Extensions.Stores.Mongo.Aggregates
{
    public abstract class MongoDBAggregateEventReader<TIdentity, TState> : MongoAccessor, IAggregateEventReader<TIdentity, TState>
        where TIdentity : notnull, IAggregateIdentity
        where TState : notnull
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
            where TEvent : ICommittedEvent<TIdentity, TState>
        {
            if (aggregateId is null)
            {
                throw new ArgumentNullException(nameof(aggregateId));
            }

            using var activity = _activitySource.StartActivity();

            _ = activity?.AddDomainAggregatesTag(new[] { aggregateId });

            cancellation.ThrowIfCancellationRequested();

            return await FindEvents<TEvent>(aggregateId, correlationId, CreateIdentity, GetCollection<MongoAggregateDocument>(), _logger, cancellation).ConfigureAwait(false);
        }

        private static async Task<IEnumerable<TEvent>> FindEvents<TEvent>(TIdentity aggregateId, ICorrelationIdentity correlationId, Func<object, TIdentity> createIdentity, IMongoCollection<MongoAggregateDocument> collection, ILogger? logger, CancellationToken cancellation)
            where TEvent : ICommittedEvent<TIdentity, TState>
        {
            var events = new List<CommittedEvent<TIdentity, TState>>();

            using var cursor = await collection
                .FindAsync<MongoAggregateDocument>(
                Builders<MongoAggregateDocument>.Filter.Eq(MongoConstants.AggregateIdKey, aggregateId.Value),
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

                foreach (var e in documemt.Events)
                {
                    if (!Definitions.NamesAndVersionsTypes.TryGetValue((e.TypeName, e.TypeVersion), out var type))
                    {
                        throw new MongoReaderEventDefinitionException(e.TypeName, e.TypeVersion);
                    }

                    events.Add(MapEvent(e, documemt, createIdentity, type));
                }
            }
            else if (logger is not null)
            {
                s_mongoResultMissing(logger, default);
            }

            return (IEnumerable<TEvent>)events;
        }

        private static CommittedEvent<TIdentity, TState> MapEvent(
            MongoAggregateEventDocument e,
            MongoAggregateDocument documemt,
            Func<object, TIdentity> createIdentity,
            Type type) =>
            new((IAggregateEvent<TState>)(BsonSerializer.Deserialize(e.Content, type) ??
                throw new MongoReaderEventDeserializationException(type)),
                    new AggregateEventMetadata<TIdentity>(
                        new IdempotencyIdentity(e.IdempotencyIdentity),
                        new CorrelationIdentity(e.CorrelationIdentity),
                        new AggregateEventIdentity(e.Identity),
                        e.TypeName,
                        e.TypeVersion,
                        createIdentity(documemt.Id),
                        documemt.Name,
                        e.AggregateVersion,
                        e.Timestamp));

        protected abstract TIdentity CreateIdentity(object value);
    }


    internal class CommittedEvent<TIdentity, TState> : ICommittedEvent<TIdentity, TState>
        where TIdentity : IAggregateIdentity
        where TState : notnull
    {
        public CommittedEvent(IAggregateEvent<TState> aggregateEvent, IAggregateEventMetadata<TIdentity> metadata)
        {
            AggregateEvent = aggregateEvent;
            Metadata = metadata;
        }

        public IAggregateEvent<TState> AggregateEvent { get; }

        public IAggregateEventMetadata<TIdentity> Metadata { get; }
    }
}
