﻿/*
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
using FakeItEasy;
using Nd.Aggregates.Events;
using Nd.Aggregates.Exceptions;
using Nd.Aggregates.Identities;
using Nd.Aggregates.Persistence;
using Nd.Core.Factories;
using Nd.Core.Types;
using Nd.Core.Types.Versions;
using Nd.Identities;
using Xunit;
using Xunit.Categories;

namespace Nd.Aggregates.Tests
{
    [UnitTest]
    public class SessionTests
    {
        #region Test types definitions

        [Identity("SessionTest")]
        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        public sealed record class TestIdentity : GuidAggregateIdentity
        {
            public TestIdentity(Guid value) : base(value) { }

            public TestIdentity(IGuidFactory factory) : base(factory) { }

            public override IAggregateRootFactory CreateAggregateFactory() => new TestAggregateRootFactory().ConfigureIdentity(this);
        }

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        public sealed record class TestAggregateState : AggregateState<TestAggregateState>,
            ICanHandleAggregateEvent<TestEventA>,
            ICanHandleAggregateEvent<TestEventB>,
            ICanHandleAggregateEvent<TestEventC2>,
            ICanHandleAggregateEvent<TestEventCount>
        {
            private readonly ConcurrentQueue<IAggregateEvent<TestAggregateState>> _events = new();

            public IReadOnlyCollection<IAggregateEvent<TestAggregateState>> Events => _events;

            public uint Counter { get; private set; }

            public override TestAggregateState State => this;

            public void Handle(TestEventA aggregateEvent) => _events.Enqueue(aggregateEvent);

            public void Handle(TestEventB aggregateEvent) => _events.Enqueue(aggregateEvent);

            public void Handle(TestEventC2 aggregateEvent) => _events.Enqueue(aggregateEvent);


            [SuppressMessage("Naming", "CA1725:Parameter names should match base declaration", Justification = "Useless parameter.")]
            public void Handle(TestEventCount _) => Counter++;

            public IAggregateEvent<TestAggregateState>? Yield() => _events.TryDequeue(out var e) ? e : default;
        }

        internal interface IHasValue
        {
            string Value { get; }
        }

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        [Event(nameof(TestEventA), 1)]
        public sealed record class TestEventA(string Value) : AggregateEvent<TestAggregateState>, IHasValue;

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        [Event(nameof(TestEventB), 1)]
        public sealed record class TestEventB : AggregateEvent<TestAggregateState>;

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        [Event("TestEventC", 1)]
        public sealed record class TestEventC1(string Value) : AggregateEvent<TestAggregateState>, IHasValue, IVersionedType
        {
            Task<IVersionedType?> IVersionedType.UpgradeAsync(CancellationToken _) => Task.FromResult<IVersionedType?>(new TestEventC2(Value));
        }

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        [Event("TestEventC", 2)]
        public sealed record class TestEventC2(string Value) : AggregateEvent<TestAggregateState>, IHasValue;

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        public sealed record class TestEventCount : AggregateEvent<TestAggregateState>;

        internal class TestAggregateRootFactory : AggregateRootFactory<TestIdentity, TestAggregateState>
        {
            public override IAggregateRoot<TestIdentity, TestAggregateState> Build(ISession session) => new TestAggregateRoot(Identity!, State, session);

            protected override IAggregateState<TestAggregateState> CreateState() => new TestAggregateState();
        }

        internal class TestAggregateRoot : AggregateRoot<TestIdentity, TestAggregateState>
        {
            public TestAggregateRoot(TestIdentity identity, IAggregateState<TestAggregateState> state, ISession session) : base(identity, state, session) { }

            public void LetThisHappen<T>(IAggregateEventMetadata? meta = default, Func<DateTimeOffset>? currentTimestampProvider = default)
                where T : AggregateEvent<TestAggregateState> => Emit(Activator.CreateInstance<T>(), meta, currentTimestampProvider);

            public void LetThisHappen<T>(T @event, IAggregateEventMetadata? meta = default, Func<DateTimeOffset>? currentTimestampProvider = default)
                where T : AggregateEvent<TestAggregateState> => Emit(@event, meta, currentTimestampProvider);

            public void Increment() => Emit(new TestEventCount(),
                new AggregateEventMetadata(
                    new IdempotencyIdentity(RandomGuidFactory.Instance.Create()),
                    new CorrelationIdentity(RandomGuidFactory.Instance.Create())));
        }

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        public record class TestAggregateCommittedEvent : ICommittedEvent<TestIdentity>
        {
            public TestAggregateCommittedEvent(IAggregateEvent aggregateEvent, IAggregateEventMetadata<TestIdentity> metadata)
            {
                AggregateEvent = aggregateEvent;
                Metadata = metadata;
            }

            public IAggregateEvent AggregateEvent { get; }

            public IAggregateEventMetadata<TestIdentity> Metadata { get; }
        }

        #endregion

        [Fact]
        public void CanFailOnDuplicateEvents()
        {
            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var idempotency = new IdempotencyIdentity(RandomGuidFactory.Instance.Create());

            var reader = A.Fake<IAggregateEventReader>();
            var writer = A.Fake<IAggregateEventWriter>();

            ISession session = new Session(reader, writer);

            var aggregate = (TestAggregateRoot)identity.CreateAggregateFactory().Build(session);

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);

            aggregate.LetThisHappen<TestEventB>(new AggregateEventMetadata(idempotency, new CorrelationIdentity(RandomGuidFactory.Instance.Create())));

            Assert.False(aggregate.IsNew);
            Assert.Equal(1u, aggregate.Version);
            Assert.Equal(typeof(TestEventB), aggregate.State.Yield()?.GetType());

            _ = Assert.Throws<RedundantAggregateEventException>(() =>
                  aggregate.LetThisHappen<TestEventB>(new AggregateEventMetadata(idempotency,
                  new CorrelationIdentity(RandomGuidFactory.Instance.Create()))));

            Assert.False(aggregate.IsNew);
            Assert.Equal(1u, aggregate.Version);
            Assert.Null(aggregate.State.Yield());
        }

        [Fact]
        public void CanReadEvents()
        {
            var expectedCorrelationId = new CorrelationIdentity(Guid.NewGuid());
            var expectedIdentity = new TestIdentity(RandomGuidFactory.Instance.Create());

            var expectedEventA = new TestEventA(RandomGuidFactory.Instance.Create().ToString());
            var expectedEventB = new TestEventB();

            var reader = A.Fake<IAggregateEventReader>();
            var writer = A.Fake<IAggregateEventWriter>();

            ISession session = new Session(reader, writer);

            ExpectEvents(expectedIdentity, new AggregateEvent<TestAggregateState>[] { expectedEventA, expectedEventB }, reader);

            var aggregate = session.LoadAsync<TestIdentity, TestAggregateRoot>(expectedIdentity, expectedCorrelationId, cancellation: CancellationToken.None).GetAwaiter().GetResult();

            Assert.NotNull(aggregate);

            AssertEvent(expectedEventA, aggregate!.State.Yield());
            AssertEvent(expectedEventB, aggregate!.State.Yield());
        }

        [Fact]
        public void CanUpgradeEvents()
        {
            var expectedCorrelationId = new CorrelationIdentity(Guid.NewGuid());
            var expectedIdentity = new TestIdentity(RandomGuidFactory.Instance.Create());

            var expectedEventA = new TestEventC1(RandomGuidFactory.Instance.Create().ToString());
            var expectedEventB = new TestEventC2(RandomGuidFactory.Instance.Create().ToString());

            var reader = A.Fake<IAggregateEventReader>();
            var writer = A.Fake<IAggregateEventWriter>();

            ISession session = new Session(reader, writer);

            ExpectEvents(expectedIdentity, new AggregateEvent<TestAggregateState>[] { expectedEventA, expectedEventB }, reader);

            var aggregate = session.LoadAsync<TestIdentity, TestAggregateRoot>(expectedIdentity, expectedCorrelationId, cancellation: CancellationToken.None).GetAwaiter().GetResult();

            Assert.NotNull(aggregate);

            AssertEvent(new TestEventC2(expectedEventA.Value), aggregate!.State.Yield());
            AssertEvent(expectedEventB, aggregate!.State.Yield());
        }

        private static void ExpectEvents(TestIdentity expectedIdentity, IEnumerable<AggregateEvent<TestAggregateState>> events, IAggregateEventReader reader)
        {
            _ = A.CallTo(() => reader.ReadAsync(A<TestIdentity>._, A<ICorrelationIdentity>._, A<uint>._, A<CancellationToken>._))
                .ReturnsLazily((fakeObjCall) => YieldEvents(expectedIdentity, events));
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private static async IAsyncEnumerable<TestAggregateCommittedEvent> YieldEvents(TestIdentity expectedIdentity, IEnumerable<AggregateEvent<TestAggregateState>> events)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            foreach (var @event in events
                    .Select(e =>
                    {
                        var eventType = TypeDefinitions.NamesAndVersionsTypes.TryGetValue((e.TypeName, e.TypeVersion), out var type) ?
                            type : throw new InvalidOperationException($"Definition of type name and version: ({e.TypeName}, {e.TypeVersion}) has no Type defined");

                        return new TestAggregateCommittedEvent(e,
                            new AggregateEventMetadata<TestIdentity>(
                                new IdempotencyIdentity(RandomGuidFactory.Instance.Create()),
                                new CorrelationIdentity(RandomGuidFactory.Instance.Create()),
                                expectedIdentity,
                                new AggregateEventIdentity(RandomGuidFactory.Instance.Create()),
                                e.TypeName,
                                e.TypeVersion,
                                1u,
                                DateTimeOffset.UtcNow));
                    })
                    .OrderBy(e => e.Metadata.AggregateVersion)
                    .ToList())
            {
                yield return @event;
            }
        }

        private static void AssertEvent(AggregateEvent<TestAggregateState>? expected, IAggregateEvent<TestAggregateState>? actual)
        {
            if (expected is null && actual is null)
            {
                return;
            }

            Assert.NotNull(expected);
            Assert.NotNull(actual);

            Assert.Equal(expected!.TypeName, actual!.TypeName);
            Assert.Equal(expected!.TypeVersion, actual!.TypeVersion);

            if (expected is IHasValue v)
            {
                Assert.Equal(v.Value, (actual as IHasValue)?.Value);
            }
        }
    }
}
