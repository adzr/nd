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

using Nd.Aggregates.Events;
using Nd.Core.Factories;
using Nd.Identities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Nd.Aggregates.Tests
{
    public class AggregateEventApplierTests
    {
        #region Test types definitions
        internal sealed record class TestIdentity : Identity<TestIdentity>
        {
            public TestIdentity(Guid value) : base(value) { }

            public TestIdentity(IGuidFactory factory) : base(factory) { }
        }

        internal abstract record class TestAggregateEvent<TEvent>
            : AggregateEvent<TEvent, TestAggregateRoot, TestIdentity, TestAggregateEventApplier>
            where TEvent : TestAggregateEvent<TEvent>;

        internal interface IApplyTestAggregateEvent<TEvent> : IAggregateEventHandler<TEvent, TestAggregateRoot, TestIdentity, TestAggregateEventApplier>
                where TEvent : TestAggregateEvent<TEvent>
        { }

        internal record class TestAggregateEventApplier : AggregateEventApplier<TestAggregateEventApplier, TestAggregateRoot, TestIdentity>,
            IApplyTestAggregateEvent<TestEventA>,
            IApplyTestAggregateEvent<TestEventB>,
            IApplyTestAggregateEvent<TestEventC>
        {
            private readonly ConcurrentQueue<IAggregateEvent<TestAggregateRoot, TestIdentity, TestAggregateEventApplier>> _events = new();

            public IReadOnlyCollection<IAggregateEvent<TestAggregateRoot, TestIdentity, TestAggregateEventApplier>> Events { get => _events; }

            public void On(TestEventA _) => _events.Enqueue(new TestEventA());

            public void On(TestEventB _) => _events.Enqueue(new TestEventB());

            public void On(TestEventC _) => _events.Enqueue(new TestEventC());
        }

        internal sealed record class TestEventA : TestAggregateEvent<TestEventA>;

        internal sealed record class TestEventB : TestAggregateEvent<TestEventB>;

        internal sealed record class TestEventC : TestAggregateEvent<TestEventC>;

        internal sealed record class TestEventD : TestAggregateEvent<TestEventD>;

        internal class TestAggregateRoot : AggregateRoot<TestAggregateRoot, TestIdentity, TestAggregateEventApplier, TestAggregateEventApplier>
        {
            public TestAggregateRoot(TestIdentity identity) : base(identity) { }

            public TestAggregateRoot(TestIdentity identity, TestAggregateEventApplier state) : base(identity, state) { }

            public void FireEvent<T>(IAggregateEventMetaData? meta = default, bool failOnDuplicates = true, Func<DateTimeOffset>? currentTimestampProvider = default)
                where T : TestAggregateEvent<T> => Emit(Activator.CreateInstance<T>(), meta, failOnDuplicates, currentTimestampProvider);
        }
        #endregion

        [Fact]
        public void CanApplyCorrectEventsBasedOnEventType()
        {
            var state = new TestAggregateEventApplier();

            var events = new IAggregateEvent<TestAggregateRoot, TestIdentity, TestAggregateEventApplier>[] {
                new TestEventC(),
                new TestEventA(),
                new TestEventB(),
                new TestEventA(),
                new TestEventC(),
                new TestEventB()
            };

            foreach (var e in events)
            {
                state.Apply(e);
            }

            Assert.True(events.SequenceEqual(state.Events));
        }

        [Fact]
        public void CanIgnoreEventsWithNoImplementation()
        {
            var state = new TestAggregateEventApplier();

            var events = new IAggregateEvent<TestAggregateRoot, TestIdentity, TestAggregateEventApplier>[] {
                new TestEventD(),
                new TestEventD(),
                new TestEventD()
            };

            foreach (var e in events)
            {
                state.Apply(e);
            }

            Assert.False(state.Events.Any());
        }
    }
}