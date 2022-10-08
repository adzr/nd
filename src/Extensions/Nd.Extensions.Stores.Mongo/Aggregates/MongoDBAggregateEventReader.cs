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
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Nd.Aggregates.Events;
using Nd.Aggregates.Extensions;
using Nd.Aggregates.Identities;
using Nd.Aggregates.Persistence;
using Nd.Core.Extensions;
using Nd.Extensions.Stores.Mongo.Common;
using Nd.Extensions.Stores.Mongo.Exceptions;
using Nd.Identities;
using Nd.Identities.Extensions;

namespace Nd.Extensions.Stores.Mongo.Aggregates
{
    public class MongoDBAggregateEventReader : MongoAccessor, IAggregateEventReader
    {
        private readonly ILogger<MongoDBAggregateEventReader>? _logger;
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
        }

        public MongoDBAggregateEventReader(
            MongoClient client,
            string databaseName,
            ILogger<MongoDBAggregateEventReader>? logger,
            ActivitySource? activitySource) :
            base(client, databaseName)
        {
            _logger = logger;
            _activitySource = activitySource ?? new ActivitySource(GetType().Name);
        }

        public async IAsyncEnumerable<ICommittedEvent<TIdentity>> ReadAsync<TIdentity>(
            TIdentity aggregateId,
            ICorrelationIdentity correlationId,
            uint versionStart,
            uint versionEnd,
            [EnumeratorCancellation] CancellationToken cancellation = default)
            where TIdentity : notnull, IAggregateIdentity
        {
            if (aggregateId is null)
            {
                throw new ArgumentNullException(nameof(aggregateId));
            }

            using var activity = _activitySource.StartActivity(nameof(ReadAsync));

            _ = activity?.AddDomainAggregatesTag(new[] { aggregateId });

            cancellation.ThrowIfCancellationRequested();

            await foreach (var @event in FindEvents<TIdentity>(
                aggregateId,
                correlationId,
#pragma warning disable CA1308 // Normalize strings to uppercase
                GetCollection<MongoAggregateDocument<TIdentity>>($"{aggregateId.TypeName}-aggregates".ToSnakeCase().ToLowerInvariant()),
#pragma warning restore CA1308 // Normalize strings to uppercase
                _logger,
                cancellation))
            {
                yield return @event;
            }
        }

        private static async IAsyncEnumerable<ICommittedEvent<TIdentity>> FindEvents<TIdentity>(
            IAggregateIdentity aggregateId,
            ICorrelationIdentity correlationId,
            IMongoCollection<MongoAggregateDocument<TIdentity>> collection,
            ILogger? logger,
            [EnumeratorCancellation] CancellationToken cancellation)
            where TIdentity : notnull, IAggregateIdentity
        {
            using var cursor = await collection
                .FindAsync<MongoAggregateDocument<TIdentity>>(
                Builders<MongoAggregateDocument<TIdentity>>.Filter.Eq(d => d.Id, aggregateId),
                cancellationToken: cancellation).ConfigureAwait(false);

            var documemt = await cursor.SingleOrDefaultAsync(cancellation).ConfigureAwait(false);

            using var scope = logger
                .BeginScope()
                .WithCorrelationId(correlationId)
                .WithAggregateId(aggregateId)
                .Build();

            if (documemt?.Events is null)
            {
                if (logger is not null)
                {
                    s_mongoResultMissing(logger, default);
                }

                yield break;
            }

            if (logger is not null)
            {
                s_mongoResultReceived(logger, documemt.ToJson(), default);
            }

            foreach (var e in documemt.Events)
            {
                cancellation.ThrowIfCancellationRequested();

                yield return e.Content is null
                    ? throw new MongoReaderEventDefinitionException("Null event content found, cannot resolve type and version")
                    : MapEvent(e, documemt);
            }
        }

        private static CommittedEvent<TIdentity> MapEvent<TIdentity>(
            MongoAggregateEventDocument e,
            MongoAggregateDocument<TIdentity> documemt)
            where TIdentity : notnull, IAggregateIdentity =>
            new(e.Content!, new AggregateEventMetadata<TIdentity>(
                        new IdempotencyIdentity(e.IdempotencyIdentity),
                        new CorrelationIdentity(e.CorrelationIdentity),
                        documemt.Id!,
                        new AggregateEventIdentity(e.Id),
                        e.Content!.TypeName,
                        e.Content.TypeVersion,
                        e.AggregateVersion,
                        e.Timestamp));
    }

    internal sealed record class CommittedEvent<TIdentity>(
        IAggregateEvent AggregateEvent,
        IAggregateEventMetadata<TIdentity> Metadata
        ) : ICommittedEvent<TIdentity>
        where TIdentity : notnull, IAggregateIdentity;
}
