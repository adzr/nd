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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using Nd.Aggregates;
using Nd.Aggregates.Events;
using Nd.Aggregates.Exceptions;
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

        [Identity("MongoDbAggregateEventTest")]
        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        public sealed record class TestIdentity : GuidAggregateIdentity
        {
            public TestIdentity(Guid value) : base(value) { }

            public TestIdentity(IGuidFactory factory) : base(factory) { }

            public override IAggregateRootFactory CreateAggregateFactory() => new TestAggregateRootFactory().ConfigureIdentity(this);
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

        internal sealed record class PendingEvent(
            IAggregateEvent AggregateEvent,
            IAggregateEventMetadata<TestIdentity> Metadata
        ) : IPendingEvent<TestIdentity>;

        internal class TestAggregateRootFactory : AggregateRootFactory<TestIdentity, TestAggregateState>
        {
            public override IAggregateRoot<TestIdentity, TestAggregateState> Build(ISession session) => new TestAggregateRoot(Identity!, State, session);

            protected override IAggregateState<TestAggregateState> CreateState() => new TestAggregateState();
        }

        internal class TestAggregateRoot : AggregateRoot<TestIdentity, TestAggregateState>
        {
            public TestAggregateRoot(TestIdentity identity, IAggregateState<TestAggregateState> state, ISession session) : base(identity, state, session) { }
        }

        #endregion

        private const string DatabaseName = "test_db";

        private MongoContainer _mongoContainer;
        private readonly MongoClient _mongoClient;
        private readonly MongoDBAggregateEventWriter _mongoWriter;
        private readonly MongoDBAggregateEventReader _mongoReader;

        public MongoDBAggregateEventStoreTests()
        {
            using var tokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            LocalPortManager.AcquireRandomPortAsync(async (port, cancellation) =>
            {
                _mongoContainer = new MongoContainer(port: $"{port}", password: Helpers.GetRandomSecureHex(16));
                await _mongoContainer.StartAsync(cancellation).ConfigureAwait(false);
            }, cancellation: tokenSource.Token).GetAwaiter().GetResult();

            var mongoSettings = MongoClientSettings.FromConnectionString(_mongoContainer!.ConnectionString);

            _mongoClient = new MongoClient(mongoSettings);

            _mongoWriter = new MongoDBAggregateEventWriter(_mongoClient, DatabaseName, default, default);

            _mongoReader = new MongoDBAggregateEventReader(_mongoClient, DatabaseName, default, default);
        }

        [Fact]
        public async Task CanStoreEventsToMongoAsync()
        {
            var correlationId = new CorrelationIdentity(Guid.NewGuid());
            var identity = new TestIdentity(CombGuidFactory.Instance.Create());

            var v1NameAndVersion = TypeDefinitions.ResolveNameAndVersion(typeof(TestEventCountV1));
            var v2NameAndVersion = TypeDefinitions.ResolveNameAndVersion(typeof(TestEventCountV2));
            var timestamp = DateTime.UtcNow;

            var expectedEvents = new[] {
                new PendingEvent(
                    Metadata: new AggregateEventMetadata<TestIdentity>(
                        IdempotencyIdentity: new IdempotencyIdentity(Guid.NewGuid()),
                        CorrelationIdentity: correlationId,
                        EventIdentity: new AggregateEventIdentity(Guid.NewGuid()),
                        TypeName: v1NameAndVersion.Name,
                        TypeVersion: v1NameAndVersion.Version,
                        AggregateIdentity: identity,
                        AggregateVersion: 1,
                        Timestamp: timestamp),
                    AggregateEvent: new TestEventCountV1()),
                new PendingEvent(
                    Metadata: new AggregateEventMetadata<TestIdentity>(
                        IdempotencyIdentity: new IdempotencyIdentity(Guid.NewGuid()),
                        CorrelationIdentity: correlationId,
                        EventIdentity: new AggregateEventIdentity(Guid.NewGuid()),
                        TypeName: v2NameAndVersion.Name,
                        TypeVersion: v2NameAndVersion.Version,
                        AggregateIdentity: identity,
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

        [Fact]
        public async Task CanFailOnLateEventsToMongoAsync()
        {
            var correlationId = new CorrelationIdentity(Guid.NewGuid());
            var identity = new TestIdentity(CombGuidFactory.Instance.Create());

            var v1NameAndVersion = TypeDefinitions.ResolveNameAndVersion(typeof(TestEventCountV1));
            var v2NameAndVersion = TypeDefinitions.ResolveNameAndVersion(typeof(TestEventCountV2));
            var timestamp = DateTime.UtcNow;

            var events = new[] {
                new PendingEvent(
                    Metadata: new AggregateEventMetadata<TestIdentity>(
                        IdempotencyIdentity: new IdempotencyIdentity(Guid.NewGuid()),
                        CorrelationIdentity: correlationId,
                        EventIdentity: new AggregateEventIdentity(Guid.NewGuid()),
                        TypeName: v1NameAndVersion.Name,
                        TypeVersion: v1NameAndVersion.Version,
                        AggregateIdentity: identity,
                        AggregateVersion: 1,
                        Timestamp: timestamp),
                    AggregateEvent: new TestEventCountV1()),
                new PendingEvent(
                    Metadata: new AggregateEventMetadata<TestIdentity>(
                        IdempotencyIdentity: new IdempotencyIdentity(Guid.NewGuid()),
                        CorrelationIdentity: correlationId,
                        EventIdentity: new AggregateEventIdentity(Guid.NewGuid()),
                        TypeName: v2NameAndVersion.Name,
                        TypeVersion: v2NameAndVersion.Version,
                        AggregateIdentity: identity,
                        AggregateVersion: 2,
                        Timestamp: timestamp),
                    AggregateEvent: new TestEventCountV2(100)),
            };

            await _mongoWriter.WriteAsync(events, default)
                .ConfigureAwait(false);

            var exception = Assert.Throws<AggregateOutOfSyncException>(() =>
                _mongoWriter.WriteAsync(events, default).GetAwaiter().GetResult());

            Assert.Equal(identity, exception.Identity);
            Assert.Equal(1u, exception.StartVersion);
            Assert.Equal(2u, exception.EndVersion);
        }

        [Fact]
        public async Task CanFailOnEarlyEventsToMongoAsync()
        {
            var correlationId = new CorrelationIdentity(Guid.NewGuid());
            var identity = new TestIdentity(CombGuidFactory.Instance.Create());

            var v1NameAndVersion = TypeDefinitions.ResolveNameAndVersion(typeof(TestEventCountV1));
            var v2NameAndVersion = TypeDefinitions.ResolveNameAndVersion(typeof(TestEventCountV2));
            var timestamp = DateTime.UtcNow;

            var events = new[] {
                new PendingEvent(
                    Metadata: new AggregateEventMetadata<TestIdentity>(
                        IdempotencyIdentity: new IdempotencyIdentity(Guid.NewGuid()),
                        CorrelationIdentity: correlationId,
                        EventIdentity: new AggregateEventIdentity(Guid.NewGuid()),
                        TypeName: v1NameAndVersion.Name,
                        TypeVersion: v1NameAndVersion.Version,
                        AggregateIdentity: identity,
                        AggregateVersion: 1,
                        Timestamp: timestamp),
                    AggregateEvent: new TestEventCountV1()),
                new PendingEvent(
                    Metadata: new AggregateEventMetadata<TestIdentity>(
                        IdempotencyIdentity: new IdempotencyIdentity(Guid.NewGuid()),
                        CorrelationIdentity: correlationId,
                        EventIdentity: new AggregateEventIdentity(Guid.NewGuid()),
                        TypeName: v2NameAndVersion.Name,
                        TypeVersion: v2NameAndVersion.Version,
                        AggregateIdentity: identity,
                        AggregateVersion: 2,
                        Timestamp: timestamp),
                    AggregateEvent: new TestEventCountV2(100)),
            };

            await _mongoWriter.WriteAsync(events, default)
                .ConfigureAwait(false);

            var exception = Assert.Throws<AggregateOutOfSyncException>(() =>
                _mongoWriter.WriteAsync(new[]
                {
                    new PendingEvent(
                    Metadata: new AggregateEventMetadata<TestIdentity>(
                        IdempotencyIdentity: new IdempotencyIdentity(Guid.NewGuid()),
                        CorrelationIdentity: correlationId,
                        EventIdentity: new AggregateEventIdentity(Guid.NewGuid()),
                        TypeName: v2NameAndVersion.Name,
                        TypeVersion: v2NameAndVersion.Version,
                        AggregateIdentity: identity,
                        AggregateVersion: 4,
                        Timestamp: timestamp),
                    AggregateEvent: new TestEventCountV2(100))
                }, default).GetAwaiter().GetResult());

            Assert.Equal(identity, exception.Identity);
            Assert.Equal(4u, exception.StartVersion);
            Assert.Equal(4u, exception.EndVersion);
        }

        [Fact]
        public void CanFailOnConflictingEventsVersionsAsync()
        {
            var correlationId = new CorrelationIdentity(Guid.NewGuid());
            var identity = new TestIdentity(CombGuidFactory.Instance.Create());

            var v1NameAndVersion = TypeDefinitions.ResolveNameAndVersion(typeof(TestEventCountV1));
            var v2NameAndVersion = TypeDefinitions.ResolveNameAndVersion(typeof(TestEventCountV2));
            var timestamp = DateTime.UtcNow;

            var events = new[] {
                new PendingEvent(
                    Metadata: new AggregateEventMetadata<TestIdentity>(
                        IdempotencyIdentity: new IdempotencyIdentity(Guid.NewGuid()),
                        CorrelationIdentity: correlationId,
                        EventIdentity: new AggregateEventIdentity(Guid.NewGuid()),
                        TypeName: v1NameAndVersion.Name,
                        TypeVersion: v1NameAndVersion.Version,
                        AggregateIdentity: identity,
                        AggregateVersion: 1,
                        Timestamp: timestamp),
                    AggregateEvent: new TestEventCountV1()),
                new PendingEvent(
                    Metadata: new AggregateEventMetadata<TestIdentity>(
                        IdempotencyIdentity: new IdempotencyIdentity(Guid.NewGuid()),
                        CorrelationIdentity: correlationId,
                        EventIdentity: new AggregateEventIdentity(Guid.NewGuid()),
                        TypeName: v2NameAndVersion.Name,
                        TypeVersion: v2NameAndVersion.Version,
                        AggregateIdentity: identity,
                        AggregateVersion: 1,
                        Timestamp: timestamp),
                    AggregateEvent: new TestEventCountV2(100)),
            };

            _ = Assert.Throws<InvalidEventSequenceException>(() =>
                _mongoWriter.WriteAsync(events, default).GetAwaiter().GetResult());
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
