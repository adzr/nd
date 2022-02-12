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
using System.Linq;
using System.Threading;
using FakeItEasy;
using Nd.Aggregates.Events;
using Nd.Aggregates.Exceptions;
using Nd.Aggregates.Identities;
using Nd.Aggregates.Persistence;
using Nd.Core.Factories;
using Nd.Identities;
using Xunit;

namespace Nd.Aggregates.Tests {
    public class AggregateRootTests {
        #region Test types definitions

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        public sealed record class TestIdentity : GuidIdentity, IAggregateIdentity {
            public TestIdentity(Guid value) : base(value) { }

            public TestIdentity(IGuidFactory factory) : base(factory) { }
        }

        internal record class TestAggregateEventApplier : AggregateEventApplier<TestAggregateEventApplier>,
            IAggregateEventHandler<TestEventA>,
            IAggregateEventHandler<TestEventB>,
            IAggregateEventHandler<TestEventC> {
            private readonly ConcurrentQueue<IAggregateEvent<TestAggregateEventApplier>> _events = new();

            public IReadOnlyCollection<IAggregateEvent<TestAggregateEventApplier>> Events { get => _events; }

            public override TestAggregateEventApplier State => this;

            public void On(TestEventA @event) => _events.Enqueue(@event);

            public void On(TestEventB _) => _events.Enqueue(new TestEventB());

            public void On(TestEventC _) => _events.Enqueue(new TestEventC());

            public IAggregateEvent? Yield() => _events.TryDequeue(out var e) ? e : default;
        }

        internal sealed record class TestEventA(string Value) : AggregateEvent<TestAggregateEventApplier>;

        internal sealed record class TestEventB : AggregateEvent<TestAggregateEventApplier>;

        internal sealed record class TestEventC : AggregateEvent<TestAggregateEventApplier>;

        internal class TestAggregateRoot : AggregateRoot<TestIdentity, TestAggregateEventApplier> {
            public TestAggregateRoot(TestIdentity identity, Func<TestAggregateEventApplier> stateFactory) : base(identity, stateFactory) { }

            public void LetThisHappen<T>(IAggregateEventMetaData? meta = default, bool failOnDuplicates = true, Func<DateTimeOffset>? currentTimestampProvider = default)
                where T : AggregateEvent<TestAggregateEventApplier> => Emit(Activator.CreateInstance<T>(), meta, failOnDuplicates, currentTimestampProvider);

            public void LetThisHappen<T>(T @event, IAggregateEventMetaData? meta = default, bool failOnDuplicates = true, Func<DateTimeOffset>? currentTimestampProvider = default)
                where T : AggregateEvent<TestAggregateEventApplier> => Emit(@event, meta, failOnDuplicates, currentTimestampProvider);
        }

        #endregion

        [Fact]
        public void CanBeNewAndHaveIdentity() {
            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());

            var aggregate = new TestAggregateRoot(identity, () => new TestAggregateEventApplier());

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);
            Assert.Equal(identity, aggregate.Identity);
        }

        [Fact]
        public void CanTriggerEventAndIncrementVersion() {
            var state = new TestAggregateEventApplier();
            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var aggregate = new TestAggregateRoot(identity, () => state);

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);

            aggregate.LetThisHappen<TestEventC>();

            Assert.False(aggregate.IsNew);
            Assert.Equal(1u, aggregate.Version);
            Assert.Equal(typeof(TestEventC), state.Yield()?.GetType());
        }

        [Fact]
        public void CanFailOnDuplicateEvents() {
            var state = new TestAggregateEventApplier();
            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var idempotency = new IdempotencyIdentity(RandomGuidFactory.Instance.Create());
            var aggregate = new TestAggregateRoot(identity, () => state);

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);

            aggregate.LetThisHappen<TestEventC>(new AggregateEventMetaData(idempotency, new CorrelationIdentity(RandomGuidFactory.Instance.Create())));

            Assert.False(aggregate.IsNew);
            Assert.Equal(1u, aggregate.Version);
            Assert.Equal(typeof(TestEventC), state.Yield()?.GetType());

            Assert.Throws<DuplicateAggregateEventException>(() =>
                aggregate.LetThisHappen<TestEventC>(new AggregateEventMetaData(idempotency, new CorrelationIdentity(RandomGuidFactory.Instance.Create()))));

            Assert.False(aggregate.IsNew);
            Assert.Equal(1u, aggregate.Version);
            Assert.Null(state.Yield());
        }

        [Fact]
        public void CanIgnoreDuplicateEvents() {
            var state = new TestAggregateEventApplier();
            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var idempotency = new IdempotencyIdentity(RandomGuidFactory.Instance.Create());
            var aggregate = new TestAggregateRoot(identity, () => state);

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);

            aggregate.LetThisHappen<TestEventC>(new AggregateEventMetaData(idempotency, new CorrelationIdentity(RandomGuidFactory.Instance.Create())));

            Assert.False(aggregate.IsNew);
            Assert.Equal(1u, aggregate.Version);
            Assert.Equal(typeof(TestEventC), state.Yield()?.GetType());

            aggregate.LetThisHappen<TestEventC>(new AggregateEventMetaData(idempotency, new CorrelationIdentity(RandomGuidFactory.Instance.Create())), false);

            Assert.False(aggregate.IsNew);
            Assert.Equal(1u, aggregate.Version);
            Assert.Null(state.Yield());
        }

        [Fact]
        public void CanPassImmutableEventData() {
            var state = new TestAggregateEventApplier();
            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var idempotency = new IdempotencyIdentity(RandomGuidFactory.Instance.Create());
            var aggregate = new TestAggregateRoot(identity, () => state);

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);

            const string Message = "Hello World!";

            aggregate.LetThisHappen(new TestEventA(Value: Message));

            Assert.False(aggregate.IsNew);
            Assert.Equal(1u, aggregate.Version);

            var @event = state.Yield();

            Assert.Equal(typeof(TestEventA), @event?.GetType());
            Assert.Equal(Message, ((TestEventA)@event!).Value);
        }

        [Fact]
        public void CanPersistEventAndMetaData() {
            var state = new TestAggregateEventApplier();
            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var aggregate = new TestAggregateRoot(identity, () => state);

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);

            var metas = new List<AggregateEventMetaData>();
            var events = new List<TestEventA>();

            const string Message = "Hello World!";

            for (int i = 0; i < 5; i++) {
                metas.Add(new(new IdempotencyIdentity(RandomGuidFactory.Instance.Create()),
                            new CorrelationIdentity(RandomGuidFactory.Instance.Create())));
                events.Add(new TestEventA($"{Message} {i}"));
            }

            var writer = A.Fake<IAggregateEventWriter<TestIdentity>>();

            foreach (var (@event, meta) in events.Zip(metas)) {
                aggregate.LetThisHappen(@event, meta);
            }

            Assert.False(aggregate.IsNew);
            Assert.Equal(5u, aggregate.Version);
            Assert.True(events.All(e => e.Equals(state.Yield())));
            Assert.True(aggregate.HasPendingChanges);

            aggregate.CommitAsync(writer, CancellationToken.None).GetAwaiter().GetResult();

            _ = A.CallTo(() => writer.WriteAsync(A<IEnumerable<IUncommittedEvent<TestIdentity>>>._, A<CancellationToken>._))
                .Invokes((IEnumerable<IUncommittedEvent<TestIdentity>> uncommittedEvents, CancellationToken cancellation) => {
                    foreach (var (expected, actual) in events
                        .Zip(metas)
                        .Select(r => (Event: r.First, MetaData: r.Second))
                        .Zip(uncommittedEvents)
                        .Select(r => (Expected: r.First, Actual: r.Second))) {
                        Assert.Equal(expected.Event, actual.Event);
                        Assert.Equal(expected.MetaData, new AggregateEventMetaData(actual.MetaData.IdempotencyIdentity, actual.MetaData.CorrelationIdentity));
                    }
                })
                .MustHaveHappenedOnceExactly();

            Assert.False(aggregate.HasPendingChanges);
        }
    }
}
