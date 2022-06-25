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
using Nd.Aggregates.Events;
using Xunit;

namespace Nd.Aggregates.Tests
{
    public class AggregateStateTests
    {
        #region Test types definitions

        internal record class TestAggregateState : AggregateState<TestAggregateState>,
            ICanHandleAggregateEvent<TestEventA>,
            ICanHandleAggregateEvent<TestEventB>,
            ICanHandleAggregateEvent<TestEventC>
        {
            private readonly ConcurrentQueue<IAggregateEvent<TestAggregateState>> _events = new();

            public IReadOnlyCollection<IAggregateEvent<TestAggregateState>> Events => _events;

            public override TestAggregateState State => this;

            public void Handle([NotNull] TestEventA _) => _events.Enqueue(new TestEventA());

            public void Handle([NotNull] TestEventB _) => _events.Enqueue(new TestEventB());

            public void Handle([NotNull] TestEventC _) => _events.Enqueue(new TestEventC());
        }

        internal sealed record class TestEventA : AggregateEvent<TestAggregateState>;

        internal sealed record class TestEventB : AggregateEvent<TestAggregateState>;

        internal sealed record class TestEventC : AggregateEvent<TestAggregateState>;

        internal sealed record class TestEventD : AggregateEvent<TestAggregateState>;

        #endregion

        [Fact]
        public void CanApplyCorrectEventsBasedOnEventType()
        {
            var state = new TestAggregateState();

            var events = new IAggregateEvent<TestAggregateState>[] {
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
            var state = new TestAggregateState();

            var events = new IAggregateEvent<TestAggregateState>[] {
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
