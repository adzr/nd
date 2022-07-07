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
using FakeItEasy;
using Nd.Aggregates.Events;
using Nd.Aggregates.Exceptions;
using Nd.Aggregates.Identities;
using Nd.Aggregates.Persistence;
using Nd.Core.Factories;
using Nd.Identities;
using Xunit;
using Xunit.Categories;

namespace Nd.Aggregates.Tests
{
    [UnitTest]
    public class AggregateRootTests
    {
        #region Test types definitions

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        public sealed record class TestIdentity : GuidAggregateIdentity
        {
            public TestIdentity(Guid value) : base(value) { }

            public TestIdentity(IGuidFactory factory) : base(factory) { }
        }

        internal record class TestAggregateState : AggregateState<TestAggregateState>,
            ICanHandleAggregateEvent<TestEventA>,
            ICanHandleAggregateEvent<TestEventB>,
            ICanHandleAggregateEvent<TestEventC>,
            ICanHandleAggregateEvent<TestEventCount>
        {
            private readonly ConcurrentQueue<IAggregateEvent<TestAggregateState>> _events = new();

            public IReadOnlyCollection<IAggregateEvent<TestAggregateState>> Events => _events;

            public uint Counter { get; private set; }

            public override TestAggregateState State => this;

            public void Handle(TestEventA @event) => _events.Enqueue(@event);

            public void Handle(TestEventB _) => _events.Enqueue(new TestEventB());

            public void Handle(TestEventC _) => _events.Enqueue(new TestEventC());

            public void Handle(TestEventCount _) => Counter++;

            public IAggregateEvent? Yield() => _events.TryDequeue(out var e) ? e : default;
        }

        internal sealed record class TestEventA(string Value) : AggregateEvent<TestAggregateState>;

        internal sealed record class TestEventB : AggregateEvent<TestAggregateState>;

        internal sealed record class TestEventC : AggregateEvent<TestAggregateState>;

        internal sealed record class TestEventCount : AggregateEvent<TestAggregateState>;

        internal class TestAggregateRoot : AggregateRoot<TestIdentity, TestAggregateState>
        {
            public TestAggregateRoot(TestIdentity identity, AggregateStateFactoryFunc<TestAggregateState> stateFactory, uint version) : base(identity, stateFactory, version) { }

            public void LetThisHappen<T>(IAggregateEventMetadata? meta = default, bool failOnDuplicates = true, Func<DateTimeOffset>? currentTimestampProvider = default)
                where T : AggregateEvent<TestAggregateState> => Emit(Activator.CreateInstance<T>(), meta, failOnDuplicates, currentTimestampProvider);

            public void LetThisHappen<T>(T @event, IAggregateEventMetadata? meta = default, bool failOnDuplicates = true, Func<DateTimeOffset>? currentTimestampProvider = default)
                where T : AggregateEvent<TestAggregateState> => Emit(@event, meta, failOnDuplicates, currentTimestampProvider);

            public void Increment() => Emit(new TestEventCount(),
                new AggregateEventMetadata(
                    new IdempotencyIdentity(RandomGuidFactory.Instance.Create()),
                    new CorrelationIdentity(RandomGuidFactory.Instance.Create())));
        }

        #endregion

        [Fact]
        public void CanBeNewAndHaveIdentity()
        {
            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());

            var aggregate = new TestAggregateRoot(identity, () => new TestAggregateState(), 0u);

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);
            Assert.Equal(identity, aggregate.Identity);
        }

        [Fact]
        public void CanTriggerEventAndIncrementVersion()
        {
            var state = new TestAggregateState();
            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var aggregate = new TestAggregateRoot(identity, () => state, 0u);

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);

            aggregate.LetThisHappen<TestEventC>();

            Assert.False(aggregate.IsNew);
            Assert.Equal(1u, aggregate.Version);
            Assert.Equal(typeof(TestEventC), state.Yield()?.GetType());
        }

        [Fact]
        public void CanFailOnDuplicateEvents()
        {
            var state = new TestAggregateState();
            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var idempotency = new IdempotencyIdentity(RandomGuidFactory.Instance.Create());
            var aggregate = new TestAggregateRoot(identity, () => state, 0u);

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);

            aggregate.LetThisHappen<TestEventC>(new AggregateEventMetadata(idempotency, new CorrelationIdentity(RandomGuidFactory.Instance.Create())));

            Assert.False(aggregate.IsNew);
            Assert.Equal(1u, aggregate.Version);
            Assert.Equal(typeof(TestEventC), state.Yield()?.GetType());

            _ = Assert.Throws<DuplicateAggregateEventException>(() =>
                  aggregate.LetThisHappen<TestEventC>(new AggregateEventMetadata(idempotency,
                  new CorrelationIdentity(RandomGuidFactory.Instance.Create()))));

            Assert.False(aggregate.IsNew);
            Assert.Equal(1u, aggregate.Version);
            Assert.Null(state.Yield());
        }

        [Fact]
        public void CanIgnoreDuplicateEvents()
        {
            var state = new TestAggregateState();
            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var idempotency = new IdempotencyIdentity(RandomGuidFactory.Instance.Create());
            var aggregate = new TestAggregateRoot(identity, () => state, 0u);

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);

            aggregate.LetThisHappen<TestEventC>(new AggregateEventMetadata(idempotency, new CorrelationIdentity(RandomGuidFactory.Instance.Create())));

            Assert.False(aggregate.IsNew);
            Assert.Equal(1u, aggregate.Version);
            Assert.Equal(typeof(TestEventC), state.Yield()?.GetType());

            aggregate.LetThisHappen<TestEventC>(new AggregateEventMetadata(idempotency, new CorrelationIdentity(RandomGuidFactory.Instance.Create())), false);

            Assert.False(aggregate.IsNew);
            Assert.Equal(1u, aggregate.Version);
            Assert.Null(state.Yield());
        }

        [Fact]
        public void CanPassImmutableEventData()
        {
            var state = new TestAggregateState();
            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var idempotency = new IdempotencyIdentity(RandomGuidFactory.Instance.Create());
            var aggregate = new TestAggregateRoot(identity, () => state, 0u);

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
        public void CanPersistEventAndMetadata()
        {
            var state = new TestAggregateState();
            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var aggregate = new TestAggregateRoot(identity, () => state, 0u);

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);

            var metas = new List<AggregateEventMetadata>();
            var events = new List<TestEventA>();

            const string Message = "Hello World!";

            for (var i = 0; i < 5; i++)
            {
                metas.Add(new(new IdempotencyIdentity(RandomGuidFactory.Instance.Create()),
                            new CorrelationIdentity(RandomGuidFactory.Instance.Create())));
                events.Add(new TestEventA($"{Message} {i}"));
            }

            var writer = A.Fake<IAggregateEventWriter<TestIdentity>>();

            foreach (var (@event, meta) in events.Zip(metas))
            {
                aggregate.LetThisHappen(@event, meta);
            }

            Assert.False(aggregate.IsNew);
            Assert.Equal(5u, aggregate.Version);
            Assert.True(events.All(e => e.Equals(state.Yield())));
            Assert.True(aggregate.HasPendingChanges);

            aggregate.CommitAsync(writer, CancellationToken.None).GetAwaiter().GetResult();
            ConfirmThatEventsAreWritten(metas, events, writer);

            Assert.False(aggregate.HasPendingChanges);
        }

        private static void ConfirmThatEventsAreWritten(List<AggregateEventMetadata> metas, List<TestEventA> events, IAggregateEventWriter<TestIdentity> writer)
        {
            _ = A.CallTo(() => writer.WriteAsync(A<IEnumerable<IUncommittedEvent<TestIdentity>>>._, A<CancellationToken>._))
                            .Invokes((IEnumerable<IUncommittedEvent<TestIdentity>> uncommittedEvents, CancellationToken cancellation) =>
                            {
                                foreach (var (expected, actual) in events
                                    .Zip(metas)
                                    .Select(r => (Event: r.First, Metadata: r.Second))
                                    .Zip(uncommittedEvents)
                                    .Select(r => (Expected: r.First, Actual: r.Second)))
                                {
                                    Assert.Equal(expected.Event, actual.AggregateEvent);
                                    Assert.Equal(expected.Metadata, new AggregateEventMetadata(actual.Metadata.IdempotencyIdentity, actual.Metadata.CorrelationIdentity));
                                }
                            })
                            .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void CanProcessEventsConcurrently()
        {
            var state = new TestAggregateState();
            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var idempotency = new IdempotencyIdentity(RandomGuidFactory.Instance.Create());
            var aggregate = new TestAggregateRoot(identity, () => state, 0u);

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);

            var wait = true;

            var tasks = from _ in Enumerable.Range(0, 1000)
                        select Task.Run(() =>
                        {
                            while (wait) { }
                            aggregate.Increment();
                        });

            wait = false;

            Task.WaitAll(tasks.ToArray());

            Assert.Equal(1000u, aggregate.Version);
            Assert.Equal(1000u, aggregate.State.Counter);
        }
    }
}
