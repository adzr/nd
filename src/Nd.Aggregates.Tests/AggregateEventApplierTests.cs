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
using Nd.Aggregates.Events;
using Xunit;

namespace Nd.Aggregates.Tests {
    public class AggregateEventApplierTests {
        #region Test types definitions

        internal record class TestAggregateEventApplier : AggregateEventApplier<TestAggregateEventApplier>,
            IAggregateEventHandler<TestEventA>,
            IAggregateEventHandler<TestEventB>,
            IAggregateEventHandler<TestEventC> {
            private readonly ConcurrentQueue<IAggregateEvent<TestAggregateEventApplier>> _events = new();

            public IReadOnlyCollection<IAggregateEvent<TestAggregateEventApplier>> Events { get => _events; }

            public override TestAggregateEventApplier State => this;

            public void On(TestEventA _) => _events.Enqueue(new TestEventA());

            public void On(TestEventB _) => _events.Enqueue(new TestEventB());

            public void On(TestEventC _) => _events.Enqueue(new TestEventC());
        }

        internal sealed record class TestEventA : AggregateEvent<TestAggregateEventApplier>;

        internal sealed record class TestEventB : AggregateEvent<TestAggregateEventApplier>;

        internal sealed record class TestEventC : AggregateEvent<TestAggregateEventApplier>;

        internal sealed record class TestEventD : AggregateEvent<TestAggregateEventApplier>;

        #endregion

        [Fact]
        public void CanApplyCorrectEventsBasedOnEventType() {
            var state = new TestAggregateEventApplier();

            var events = new IAggregateEvent<TestAggregateEventApplier>[] {
                new TestEventC(),
                new TestEventA(),
                new TestEventB(),
                new TestEventA(),
                new TestEventC(),
                new TestEventB()
            };

            foreach (var e in events) {
                state.Apply(e);
            }

            Assert.True(events.SequenceEqual(state.Events));
        }

        [Fact]
        public void CanIgnoreEventsWithNoImplementation() {
            var state = new TestAggregateEventApplier();

            var events = new IAggregateEvent<TestAggregateEventApplier>[] {
                new TestEventD(),
                new TestEventD(),
                new TestEventD()
            };

            foreach (var e in events) {
                state.Apply(e);
            }

            Assert.False(state.Events.Any());
        }
    }
}
