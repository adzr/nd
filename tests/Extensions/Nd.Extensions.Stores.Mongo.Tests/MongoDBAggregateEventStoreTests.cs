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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Nd.Aggregates.Events;
using Nd.Aggregates.Identities;
using Nd.Aggregates.Persistence;
using Nd.Containers;
using Nd.Core.Factories;
using Nd.Core.Types;
using Nd.Extensions.Stores.Mongo.Aggregates;
using Nd.Identities;
using Xunit;
using Xunit.Categories;

namespace Nd.Extensions.Stores.Mongo.Tests
{
    [IntegrationTest]
    public class MongoDBAggregateEventStoreTests : IDisposable
    {
        #region Test types definitions

        [Identity("AggregatesTestIdentity")]
        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be serialized.")]
        public sealed record class TestIdentity : GuidAggregateIdentity
        {
            public TestIdentity(Guid value) : base(value) { }

            public TestIdentity(IGuidFactory factory) : base(factory) { }
        }

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be serialized.")]
        public record class TestAggregateState : AggregateState<TestAggregateState>,
            ICanHandleAggregateEvent<TestEventCountV1>,
            ICanHandleAggregateEvent<TestEventCountV2>
        {
            private readonly ConcurrentQueue<IAggregateEvent<TestAggregateState>> _events = new();

            public IReadOnlyCollection<IAggregateEvent<TestAggregateState>> Events => _events;

            public uint Counter { get; private set; }

            public override TestAggregateState State => this;
            public void Handle(TestEventCountV1 aggregateEvent) => Counter++;

            public void Handle(TestEventCountV2 aggregateEvent) => Counter += aggregateEvent?.Amount ?? 0;
        }

        [Event("TestEventCount", 1)]
        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be serialized.")]
        public sealed record class TestEventCountV1 : AggregateEvent<TestAggregateState>;

        [Event("TestEventCount", 2)]
        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be serialized.")]
        public sealed record class TestEventCountV2(uint Amount) : AggregateEvent<TestAggregateState>;

        internal class TestReader : MongoDBAggregateEventReader<TestIdentity, Guid>
        {
            public TestReader(
                MongoClient client,
                string databaseName,
                string collectionName,
                ILogger<TestReader>? logger,
                ActivitySource? activitySource) :
                base(
                    client,
                    databaseName,
                    collectionName,
                    logger,
                    activitySource)
            { }

            protected override TestIdentity CreateIdentity(object value) => new(Guid.Parse($"{value}"));
        }

        internal class TestWriter : MongoDBAggregateEventWriter<TestIdentity, Guid>
        {
            public TestWriter(
                MongoClient client,
                string databaseName,
                string collectionName,
                ILogger<TestWriter>? logger,
                ActivitySource? activitySource) :
                base(
                    client,
                    databaseName,
                    collectionName,
                    logger,
                    activitySource)
            { }
        }

        internal sealed record class UncommittedEvent(
            IAggregateEvent AggregateEvent,
            IAggregateEventMetadata<TestIdentity> Metadata
        ) : IUncommittedEvent<TestIdentity>;

        #endregion

        private const string DatabaseName = "test_db";
        private const string CollectionName = "events";

        private MongoContainer _mongoContainer;
        private readonly MongoClient _mongoClient;
        private readonly TestWriter _mongoWriter;
        private readonly TestReader _mongoReader;

        public MongoDBAggregateEventStoreTests()
        {
            using var tokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(1));

            LocalPortManager.AcquireRandomPortAsync(async (port, cancellation) =>
            {
                _mongoContainer = new MongoContainer(port: $"{port}", password: Helpers.GetRandomSecureHex(16));
                await _mongoContainer.StartAsync(cancellation).ConfigureAwait(false);
            }, cancellation: tokenSource.Token).GetAwaiter().GetResult();

            var mongoSettings = MongoClientSettings.FromConnectionString(_mongoContainer!.ConnectionString);

            _mongoClient = new MongoClient(mongoSettings);

            _mongoWriter = new TestWriter(_mongoClient, DatabaseName, CollectionName, default, default);

            _mongoReader = new TestReader(_mongoClient, DatabaseName, CollectionName, default, default);
        }

        [Fact]
        public async Task CanStoreEventsToMongoAsync()
        {
            var correlationId = new CorrelationIdentity(Guid.NewGuid());
            var _ = new TestAggregateState();
            var identity = new TestIdentity(CombGuidFactory.Instance.Create());
            var aggregateName = "TestAggregateRoot";

            var v1NameAndVersion = TypeDefinitions.ResolveNameAndVersion(typeof(TestEventCountV1));
            var v2NameAndVersion = TypeDefinitions.ResolveNameAndVersion(typeof(TestEventCountV2));
            var timestamp = DateTime.UtcNow;

            var expectedEvents = new[] {
                new UncommittedEvent(
                    Metadata: new AggregateEventMetadata<TestIdentity>(
                        IdempotencyIdentity: new IdempotencyIdentity(Guid.NewGuid()),
                        CorrelationIdentity: new CorrelationIdentity(Guid.NewGuid()),
                        EventIdentity: new AggregateEventIdentity(Guid.NewGuid()),
                        TypeName: v1NameAndVersion.Name,
                        TypeVersion: v1NameAndVersion.Version,
                        AggregateIdentity: identity,
                        AggregateName: aggregateName,
                        AggregateVersion: 1,
                        Timestamp: timestamp),
                    AggregateEvent: new TestEventCountV1()),
                new UncommittedEvent(
                    Metadata: new AggregateEventMetadata<TestIdentity>(
                        IdempotencyIdentity: new IdempotencyIdentity(Guid.NewGuid()),
                        CorrelationIdentity: new CorrelationIdentity(Guid.NewGuid()),
                        EventIdentity: new AggregateEventIdentity(Guid.NewGuid()),
                        TypeName: v2NameAndVersion.Name,
                        TypeVersion: v2NameAndVersion.Version,
                        AggregateIdentity: identity,
                        AggregateName: aggregateName,
                        AggregateVersion: 2,
                        Timestamp: timestamp),
                    AggregateEvent: new TestEventCountV2(100)),
            };

            await _mongoWriter.WriteAsync(expectedEvents, default)
                .ConfigureAwait(false);

            var events = new List<ICommittedEvent<TestIdentity>>();

            await foreach (var @event in _mongoReader
                .ReadAsync(identity, correlationId, uint.MinValue, uint.MaxValue, default))
            {
                events.Add(@event);
            }

            Assert.Equal(
                expectedEvents
                .Select(e => (e.AggregateEvent, e.Metadata))
                .ToArray(),
                events
                .Select(e => (e.AggregateEvent, e.Metadata))
                .ToArray());
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MongoDBAggregateEventStoreTests() => Dispose(false);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _mongoContainer.Dispose();
            }
        }
    }
}
