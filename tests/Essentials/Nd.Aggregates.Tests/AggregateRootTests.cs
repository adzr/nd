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
using System.Threading.Tasks;
using FakeItEasy;
using Nd.Aggregates.Events;
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

        [Identity("AggregateRootTest")]
        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        public sealed record class TestIdentity : GuidAggregateIdentity
        {
            public TestIdentity(Guid value) : base(value) { }

            public TestIdentity(IGuidFactory factory) : base(factory) { }

            public override IAggregateRootFactory CreateAggregateFactory() => new TestAggregateRootFactory().ConfigureIdentity(this);
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

        [Event("AggregateRootTestEventA", 1)]
        internal sealed record class TestEventA(string Value) : AggregateEvent<TestAggregateState>;

        [Event("AggregateRootTestEventB", 1)]
        internal sealed record class TestEventB : AggregateEvent<TestAggregateState>;

        [Event("AggregateRootTestEventC", 1)]
        internal sealed record class TestEventC : AggregateEvent<TestAggregateState>;

        [Event("AggregateRootTestEventCount", 1)]
        internal sealed record class TestEventCount : AggregateEvent<TestAggregateState>;

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

        #endregion

        [Fact]
        public void CanBeNewAndHaveIdentity()
        {
            var session = A.Fake<ISession>();

            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());

            var aggregate = (TestAggregateRoot)identity.CreateAggregateFactory().Build(session);

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);
            Assert.Equal(identity, aggregate.Identity);
        }

        [Fact]
        public void CanTriggerEventAndIncrementVersion()
        {
            var session = A.Fake<ISession>();

            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var aggregate = (TestAggregateRoot)identity.CreateAggregateFactory().Build(session);

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);

            aggregate.LetThisHappen<TestEventC>();

            Assert.False(aggregate.IsNew);
            Assert.Equal(1u, aggregate.Version);
            Assert.Equal(typeof(TestEventC), aggregate.State.Yield()?.GetType());
        }

        [Fact]
        public void CanPassImmutableEventData()
        {
            var session = A.Fake<ISession>();

            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var idempotency = new IdempotencyIdentity(RandomGuidFactory.Instance.Create());
            var aggregate = (TestAggregateRoot)identity.CreateAggregateFactory().Build(session);

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);

            const string Message = "Hello World!";

            aggregate.LetThisHappen(new TestEventA(Value: Message));

            Assert.False(aggregate.IsNew);
            Assert.Equal(1u, aggregate.Version);

            var @event = aggregate.State.Yield();

            Assert.Equal(typeof(TestEventA), @event?.GetType());
            Assert.Equal(Message, ((TestEventA)@event!).Value);
        }

        [Fact]
        public void CanEnqueueEventsIntoSession()
        {
            var session = A.Fake<ISession>();

            var pendingEvents = new Queue<IPendingEvent>();

            _ = A.CallTo(() => session.EnqueuePendingEvents(A<IPendingEvent[]>._))
                .Invokes(call => pendingEvents.Enqueue(call.Arguments.Get<IPendingEvent[]>(0)![0]));

            var identity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var aggregate = (TestAggregateRoot)identity.CreateAggregateFactory().Build(session);

            var metas = new List<AggregateEventMetadata>();
            var events = new List<TestEventA>();

            const string Message = "Hello World!";
            const uint EventCount = 5u;

            Assert.True(aggregate.IsNew);
            Assert.Equal(0u, aggregate.Version);

            for (var i = 0; i < EventCount; i++)
            {
                metas.Add(new(new IdempotencyIdentity(RandomGuidFactory.Instance.Create()),
                            new CorrelationIdentity(RandomGuidFactory.Instance.Create())));
                events.Add(new TestEventA($"{Message} {i}"));
            }

            foreach (var (@event, meta) in events.Zip(metas))
            {
                aggregate.LetThisHappen(@event, meta);
                var result = pendingEvents.Dequeue();
                Assert.Equal(@event, result.AggregateEvent);
                Assert.Equal(meta.CorrelationIdentity, result.Metadata.CorrelationIdentity);
                Assert.Equal(meta.IdempotencyIdentity, result.Metadata.IdempotencyIdentity);
            }

            Assert.False(aggregate.IsNew);
            Assert.Equal(EventCount, aggregate.Version);
            Assert.True(events.All(e => e.Equals(aggregate.State.Yield())));
            Assert.False(pendingEvents.TryDequeue(out _));
        }

        [Fact]
        public void CanProcessEventsConcurrently()
        {
            var session = A.Fake<ISession>();

            var pendingEvents = new ConcurrentQueue<IPendingEvent>();

            _ = A.CallTo(() => session.EnqueuePendingEvents(A<IPendingEvent[]>._))
                .Invokes(call => pendingEvents.Enqueue(call.Arguments.Get<IPendingEvent[]>(0)![0]));

            var identityA = new TestIdentity(RandomGuidFactory.Instance.Create());
            var aggregateA = (TestAggregateRoot)identityA.CreateAggregateFactory().Build(session);

            var identityB = new TestIdentity(RandomGuidFactory.Instance.Create());
            var aggregateB = (TestAggregateRoot)identityB.CreateAggregateFactory().Build(session);

            var identityC = new TestIdentity(RandomGuidFactory.Instance.Create());
            var aggregateC = (TestAggregateRoot)identityC.CreateAggregateFactory().Build(session);

            var idempotency = new IdempotencyIdentity(RandomGuidFactory.Instance.Create());

            Assert.True(aggregateA.IsNew);
            Assert.Equal(0u, aggregateA.Version);
            Assert.True(aggregateB.IsNew);
            Assert.Equal(0u, aggregateB.Version);
            Assert.True(aggregateC.IsNew);
            Assert.Equal(0u, aggregateC.Version);

            var startSignal = false;

            var aggregateATasks = from _ in Enumerable.Range(0, 1000)
                                  select Task.Run(() =>
                                  {
                                      while (!startSignal) { }
                                      aggregateA.Increment();
                                  });

            var aggregateBTasks = from _ in Enumerable.Range(0, 500)
                                  select Task.Run(() =>
                                  {
                                      while (!startSignal) { }
                                      aggregateB.Increment();
                                  });

            var aggregateCTasks = from _ in Enumerable.Range(0, 200)
                                  select Task.Run(() =>
                                  {
                                      while (!startSignal) { }
                                      aggregateC.Increment();
                                  });

            startSignal = true;

            Task.WaitAll(aggregateATasks
                .Union(aggregateBTasks)
                .Union(aggregateCTasks)
                .ToArray());

            Assert.Equal(1000u, aggregateA.Version);
            Assert.Equal(1000u, aggregateA.State.Counter);

            Assert.Equal(500u, aggregateB.Version);
            Assert.Equal(500u, aggregateB.State.Counter);

            Assert.Equal(200u, aggregateC.Version);
            Assert.Equal(200u, aggregateC.State.Counter);

            Assert.Equal(1700, pendingEvents.Count);

            Assert.Equal(1000, pendingEvents.Where(e => e.Metadata.AggregateIdentity.Equals(identityA)).Count());
            Assert.Equal(500, pendingEvents.Where(e => e.Metadata.AggregateIdentity.Equals(identityB)).Count());
            Assert.Equal(200, pendingEvents.Where(e => e.Metadata.AggregateIdentity.Equals(identityC)).Count());
        }
    }
}
