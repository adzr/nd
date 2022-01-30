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

using FakeItEasy;
using Nd.Aggregates.Events;
using Nd.Aggregates.Exceptions;
using Nd.Aggregates.Identities;
using Nd.Aggregates.Persistence;
using Nd.Core.Factories;
using Nd.Identities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

namespace Nd.Aggregates.Tests
{
    public class AggregateRootTests
    {
        #region Test types definitions
        public sealed record class TestIdentity : Identity<TestIdentity>
        {
            public TestIdentity(Guid value) : base(value) { }

            public TestIdentity(IGuidFactory factory) : base(factory) { }
        }

        public abstract record class TestAggregateEvent<TEvent>
            : AggregateEvent<TEvent, TestAggregateRoot, TestIdentity, TestAggregateEventApplier>
            where TEvent : TestAggregateEvent<TEvent>;

        internal interface IApplyTestAggregateEvent<TEvent> : IAggregateEventHandler<TEvent, TestAggregateRoot, TestIdentity, TestAggregateEventApplier>
                where TEvent : TestAggregateEvent<TEvent>
        { }

        public record class TestAggregateEventApplier : AggregateEventApplier<TestAggregateEventApplier, TestAggregateRoot, TestIdentity>,
            IApplyTestAggregateEvent<TestEventA>,
            IApplyTestAggregateEvent<TestEventB>,
            IApplyTestAggregateEvent<TestEventC>
        {
            private readonly ConcurrentQueue<IAggregateEvent<TestAggregateRoot, TestIdentity, TestAggregateEventApplier>> _events = new();

            public IReadOnlyCollection<IAggregateEvent<TestAggregateRoot, TestIdentity, TestAggregateEventApplier>> Events { get => _events; }

            public void On(TestEventA @event) => _events.Enqueue(@event);

            public void On(TestEventB _) => _events.Enqueue(new TestEventB());

            public void On(TestEventC _) => _events.Enqueue(new TestEventC());
        }

        public sealed record class TestEventA(string Value) : TestAggregateEvent<TestEventA>;

        public sealed record class TestEventB : TestAggregateEvent<TestEventB>;

        public sealed record class TestEventC : TestAggregateEvent<TestEventC>;

        public sealed record class TestEventD : TestAggregateEvent<TestEventD>;

        public class TestAggregateRoot : AggregateRoot<TestAggregateRoot, TestIdentity, TestAggregateEventApplier, TestAggregateEventApplier>
        {
            public TestAggregateRoot(TestIdentity identity) : base(identity) { }

            public TestAggregateRoot(TestIdentity identity, TestAggregateEventApplier state) : base(identity, state) { }

            public void LetThisHappen<T>(IAggregateEventMetaData? meta = default, bool failOnDuplicates = true, Func<DateTimeOffset>? currentTimestampProvider = default)
                where T : TestAggregateEvent<T> => Emit(Activator.CreateInstance<T>(), meta, failOnDuplicates, currentTimestampProvider);

            public void LetThisHappen<T>(T @event, IAggregateEventMetaData? meta = default, bool failOnDuplicates = true, Func<DateTimeOffset>? currentTimestampProvider = default)
                where T : TestAggregateEvent<T> => Emit(@event, meta, failOnDuplicates, currentTimestampProvider);
        }

        internal record class QueueTestAggregateEventApplier : TestAggregateEventApplier
        {
            private readonly Queue<IAggregateEvent> _events = new();

            public override void Apply(IAggregateEvent @event) => _events.Enqueue(@event);

            public IAggregateEvent Yield() => _events.TryDequeue(out var e) ? e :
                throw new IndexOutOfRangeException($"{typeof(QueueTestAggregateEventApplier)} is expected to contain elements but it's empty");
        }
        #endregion

        [Fact]
        public void CanBeNewAndHaveIdentity()
        {
            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());

            var aggregate = new TestAggregateRoot(identity);

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);
            Assert.Equal(identity, aggregate.Identity);
        }

        [Fact]
        public void CanTriggerEventAndIncrementVersion()
        {
            var state = new QueueTestAggregateEventApplier();
            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var aggregate = new TestAggregateRoot(identity, state);

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);

            aggregate.LetThisHappen<TestEventC>();

            Assert.False(aggregate.IsNew);
            Assert.Equal(1u, aggregate.Version);
            Assert.Equal(typeof(TestEventC), state.Yield().GetType());
        }

        [Fact]
        public void CanFailOnDuplicateEvents()
        {
            var state = new QueueTestAggregateEventApplier();
            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var idempotency = new IdempotencyIdentity(RandomGuidFactory.Instance.Create());
            var aggregate = new TestAggregateRoot(identity, state);

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);

            aggregate.LetThisHappen<TestEventC>(new AggregateEventMetaData(idempotency, new CorrelationIdentity(RandomGuidFactory.Instance.Create())));

            Assert.False(aggregate.IsNew);
            Assert.Equal(1u, aggregate.Version);
            Assert.Equal(typeof(TestEventC), state.Yield().GetType());

            Assert.Throws<DuplicateAggregateEventException>(() =>
                aggregate.LetThisHappen<TestEventC>(new AggregateEventMetaData(idempotency, new CorrelationIdentity(RandomGuidFactory.Instance.Create()))));

            Assert.False(aggregate.IsNew);
            Assert.Equal(1u, aggregate.Version);
            Assert.Throws<IndexOutOfRangeException>(() => state.Yield());
        }

        [Fact]
        public void CanIgnoreDuplicateEvents()
        {
            var state = new QueueTestAggregateEventApplier();
            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var idempotency = new IdempotencyIdentity(RandomGuidFactory.Instance.Create());
            var aggregate = new TestAggregateRoot(identity, state);

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);

            aggregate.LetThisHappen<TestEventC>(new AggregateEventMetaData(idempotency, new CorrelationIdentity(RandomGuidFactory.Instance.Create())));

            Assert.False(aggregate.IsNew);
            Assert.Equal(1u, aggregate.Version);
            Assert.Equal(typeof(TestEventC), state.Yield().GetType());

            aggregate.LetThisHappen<TestEventC>(new AggregateEventMetaData(idempotency, new CorrelationIdentity(RandomGuidFactory.Instance.Create())), false);

            Assert.False(aggregate.IsNew);
            Assert.Equal(1u, aggregate.Version);
            Assert.Throws<IndexOutOfRangeException>(() => state.Yield());
        }

        [Fact]
        public void CanPassImmutableEventData()
        {
            var state = new QueueTestAggregateEventApplier();
            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var idempotency = new IdempotencyIdentity(RandomGuidFactory.Instance.Create());
            var aggregate = new TestAggregateRoot(identity, state);

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);

            const string Message = "Hello World!";

            aggregate.LetThisHappen(new TestEventA(Value: Message));

            Assert.False(aggregate.IsNew);
            Assert.Equal(1u, aggregate.Version);

            var @event = state.Yield();

            Assert.Equal(typeof(TestEventA), @event.GetType());
            Assert.Equal(Message, ((TestEventA)@event).Value);
        }

        [Fact]
        public void CanPersistEventAndMetaData()
        {
            var state = new QueueTestAggregateEventApplier();
            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var aggregate = new TestAggregateRoot(identity, state);

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);

            var metas = new List<AggregateEventMetaData>();
            var events = new List<TestEventA>();

            const string Message = "Hello World!";

            for (int i = 0; i < 5; i++)
            {
                metas.Add(new(new IdempotencyIdentity(RandomGuidFactory.Instance.Create()),
                            new CorrelationIdentity(RandomGuidFactory.Instance.Create())));
                events.Add(new TestEventA($"{Message} {i}"));
            }

            var writer = A.Fake<IAggregateEventWriter<TestAggregateRoot, TestIdentity, TestAggregateEventApplier>>();

            foreach (var (@event, meta) in events.Zip(metas))
            {
                aggregate.LetThisHappen(@event, meta);
            }

            Assert.False(aggregate.IsNew);
            Assert.Equal(5u, aggregate.Version);
            Assert.True(events.All(e => e.Equals(state.Yield())));
            Assert.True(aggregate.HasPendingChanges);

            aggregate.CommitAsync(writer, CancellationToken.None).GetAwaiter().GetResult();

            A.CallTo(() => writer.WriteAsync(A<IEnumerable<IUncommittedEvent<TestAggregateRoot, TestIdentity, TestAggregateEventApplier>>>._, A<CancellationToken>._))
                .Invokes((IEnumerable<IUncommittedEvent<TestAggregateRoot, TestIdentity, TestAggregateEventApplier>> uncommittedEvents, CancellationToken cancellation) =>
                {
                    foreach (var (Expected, Actual) in events
                        .Zip(metas)
                        .Select(r => (Event: r.First, MetaData: r.Second))
                        .Zip(uncommittedEvents)
                        .Select(r => (Expected: r.First, Actual: r.Second)))
                    {
                        Assert.Equal(Expected.Event, Actual.Event);
                        Assert.Equal(Expected.MetaData, new AggregateEventMetaData(Actual.MetaData.IdempotencyIdentity, Actual.MetaData.CorrelationIdentity));
                    }
                })
                .MustHaveHappenedOnceExactly();

            Assert.False(aggregate.HasPendingChanges);
        }
    }
}