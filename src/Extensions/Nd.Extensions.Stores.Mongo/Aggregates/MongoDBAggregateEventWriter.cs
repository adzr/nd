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
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
using Nd.Identities.Extensions;

namespace Nd.Extensions.Stores.Mongo.Aggregates
{
    public class MongoDBAggregateEventWriter : MongoAccessor, IAggregateEventWriter
    {
        private static readonly Action<ILogger, string, Exception?> s_mongoResultReceived =
            LoggerMessage.Define<string>(LogLevel.Trace, new EventId(
                                MongoLoggingEventsConstants.MongoResultReceived,
                                nameof(MongoLoggingEventsConstants.MongoResultReceived)),
                                "Mongo result received {Result}");

        private static readonly Action<ILogger, Exception?> s_mongoResultMissing =
            LoggerMessage.Define(LogLevel.Error, new EventId(
                                MongoLoggingEventsConstants.MongoResultMissing,
                                nameof(MongoLoggingEventsConstants.MongoResultMissing)),
                                "Mongo result missing");

        private static readonly Action<ILogger, Exception?> s_mongoNotSupportingTransactions =
            LoggerMessage.Define(LogLevel.Warning, new EventId(
                                MongoLoggingEventsConstants.MongoNotSupportingTransactions,
                                nameof(MongoLoggingEventsConstants.MongoNotSupportingTransactions)),
                                "Mongo transactions are not supported");


        private readonly ILogger<MongoDBAggregateEventWriter>? _logger;
        private readonly ActivitySource _activitySource;

        static MongoDBAggregateEventWriter()
        {
            BsonDefaultsInitializer.Initialize();
        }

        public MongoDBAggregateEventWriter(
            MongoClient client,
            string databaseName,
            ILogger<MongoDBAggregateEventWriter>? logger,
            ActivitySource? activitySource) :
            base(client, databaseName)
        {
            _logger = logger;
            _activitySource = activitySource ?? new ActivitySource(GetType().Name);
        }

        public async Task WriteAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellation = default)
            where TEvent : notnull, IPendingEvent
        {
            if (events is null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            var eventList = events.ToList();

            if (!eventList.Any())
            {
                return;
            }

            using var activity = _activitySource.StartActivity(nameof(WriteAsync));

            var first = eventList.First(e => e.Metadata.CorrelationIdentity is not null);

            using var scope = _logger
                .BeginScope()
                .WithCorrelationId(first.Metadata.CorrelationIdentity)
                .WithAggregateId(first.Metadata.AggregateIdentity)
                .Build();

            using var session = await Client
                .StartSessionAsync(cancellationToken: cancellation)
                .ConfigureAwait(false);

            if (session is null)
            {
                throw new MongoSessionException("Failed to start mongo session");
            }

            await WriteInternalAsync(session, activity, eventList, cancellation).ConfigureAwait(false);
        }

        private async Task WriteInternalAsync<TEvent>(IClientSessionHandle session, Activity? activity, IList<TEvent> eventList, CancellationToken cancellation)
            where TEvent : notnull, IPendingEvent
        {
            var aggregates = eventList
                .GroupBy(e => e.Metadata.AggregateIdentity)
                .Select(g => (Identity: g.Key, Events: g.OrderBy(e => e.Metadata.AggregateVersion).ToImmutableArray()))
                .ToImmutableArray();

            foreach (var (identity, events) in aggregates)
            {
                cancellation.ThrowIfCancellationRequested();

                ValidatePendingEventSequence(identity, events);
            }

            try
            {
                StartTransaction(session, activity, eventList);
            }
            catch (NotSupportedException e)
            {
                if (_logger is not null)
                {
                    s_mongoNotSupportingTransactions(_logger, e);
                }
            }

            try
            {
                foreach (var (identity, events) in aggregates)
                {
                    cancellation.ThrowIfCancellationRequested();

                    var (startVersion, endVersion) =
                        (events.First().Metadata.AggregateVersion,
                        events.Last().Metadata.AggregateVersion);

                    var aggregateId = identity;
#pragma warning disable CA1308 // Normalize strings to uppercase
                    var mongoResult = await GetCollection<MongoAggregateDocument<IAggregateIdentity>>($"{identity.TypeName}-aggregates".ToSnakeCase().ToLowerInvariant())
#pragma warning disable CA1308 // Normalize strings to uppercase
                        .UpdateOneAsync(session, CreateAggregateFilteringCriteria(aggregateId, startVersion),
                        CreateAggregateUpdateSettings(aggregateId, endVersion, events),
                        new UpdateOptions
                        {
                            IsUpsert = true
                        },
                        cancellation)
                    .ConfigureAwait(false);

                    LogMongoResult(mongoResult);
                }
            }
            catch
            {
                await AbortTransaction(session, activity, eventList, cancellation).ConfigureAwait(false);
                throw;
            }

            await CommitTranaction(session, activity, eventList, cancellation).ConfigureAwait(false);
        }

        private static void ValidatePendingEventSequence<TEvent>(IAggregateIdentity identity, ImmutableArray<TEvent> events) where TEvent : notnull, IPendingEvent
        {
            var versions = events
                .Select(e => e.Metadata.AggregateVersion)
                .ToImmutableArray();

            if (!versions.Any())
            {
                throw new InvalidEventSequenceException($"Aggregate {identity} has no pending event sequence");
            }

            var expectedVersion = versions.First();

            foreach (var version in versions)
            {
                if (expectedVersion < 1)
                {
                    throw new InvalidEventSequenceException($"Aggregate {identity} cannot have event version less than 0, found {version}");
                }

                if (expectedVersion != version)
                {
                    throw new InvalidEventSequenceException($"Aggregate {identity} has unexpected pending event version sequence, expected {expectedVersion} found {version}");
                }

                expectedVersion++;
            }
        }

        private void LogMongoResult(UpdateResult? mongoResult)
        {
            if (_logger is not null)
            {
                if (mongoResult is not null)
                {
                    s_mongoResultReceived(_logger, mongoResult.ToJson(), default);
                }
                else
                {
                    s_mongoResultMissing(_logger, default);
                }
            }
        }

        private static UpdateDefinition<MongoAggregateDocument<IAggregateIdentity>> CreateAggregateUpdateSettings<TEvent>(
            IAggregateIdentity aggregateId,
            uint endVersion,
            IEnumerable<TEvent> events)
            where TEvent : IPendingEvent =>
            Builders<MongoAggregateDocument<IAggregateIdentity>>.Update
            .SetOnInsert(d => d.Id, aggregateId)
            .Set(d => d.Version, endVersion)
            .AddToSetEach(d => d.Events, events
                .OrderBy(e => e.Metadata.AggregateVersion)
                .Select(e => new MongoAggregateEventDocument
                {
                    Id = e.Metadata.EventIdentity.Value,
                    Timestamp = e.Metadata.Timestamp,
                    AggregateVersion = e.Metadata.AggregateVersion,
                    CorrelationIdentity = e.Metadata.CorrelationIdentity.Value,
                    IdempotencyIdentity = e.Metadata.IdempotencyIdentity.Value,
                    Content = e.AggregateEvent
                }));

        private static FilterDefinition<MongoAggregateDocument<IAggregateIdentity>> CreateAggregateFilteringCriteria(IAggregateIdentity aggregateId, uint startVersion) =>
            Builders<MongoAggregateDocument<IAggregateIdentity>>.Filter.And(
                Builders<MongoAggregateDocument<IAggregateIdentity>>.Filter.Eq(d => d.Id, aggregateId),
                Builders<MongoAggregateDocument<IAggregateIdentity>>.Filter.Eq(d => d.Version, startVersion - 1));

        private static async Task CommitTranaction<TEvent>(IClientSessionHandle session, Activity? activity, IList<TEvent> eventList, CancellationToken cancellation)
            where TEvent : notnull, IPendingEvent
        {
            if (session.IsInTransaction)
            {
                await session.CommitTransactionAsync(cancellation).ConfigureAwait(false);
                AddActivityTags(activity, eventList);
                _ = activity?.AddTag(MongoActivityConstants.MongoSuccessfulResultTag, true);
            }
        }

        private static async Task AbortTransaction<TEvent>(IClientSessionHandle session, Activity? activity, IList<TEvent> eventList, CancellationToken cancellation)
            where TEvent : notnull, IPendingEvent
        {
            if (session.IsInTransaction)
            {
                await session.AbortTransactionAsync(cancellation).ConfigureAwait(false);
                AddActivityTags(activity, eventList);
                _ = activity?.AddTag(MongoActivityConstants.MongoSuccessfulResultTag, false);
            }
        }

        private static void StartTransaction<TEvent>(IClientSessionHandle session, Activity? activity, IList<TEvent> eventList)
            where TEvent : notnull, IPendingEvent
        {
            session.StartTransaction();
            AddActivityTags(activity, eventList);
        }

        private static void AddActivityTags<TEvent>(Activity? activity, IList<TEvent> eventList)
            where TEvent : notnull, IPendingEvent =>
            _ = activity?
                .AddCorrelationsTag(eventList
                    .GroupBy(e => e.Metadata.CorrelationIdentity)
                    .Select(e => e.Key.Value)
                    .ToArray())
                .AddDomainAggregatesTag(eventList
                    .GroupBy(e => e.Metadata.AggregateIdentity)
                    .Select(e => e.Key)
                    .ToArray());
    }
}
