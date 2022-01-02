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
using Nd.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Xunit;

namespace Nd.Aggregates.Tests
{
    internal sealed class SampleId : Identity<SampleId>
    {
        public SampleId(Guid value) : base(value) { }
    }

    internal sealed class SampleAggregateRoot : AggregateRoot<SampleAggregateRoot, SampleId, SampleAggregateState>
    {
        public SampleAggregateRoot(SampleId identity) : base(identity) { }
    }
    internal sealed class SampleAggregateState : AggregateState<SampleAggregateRoot, SampleId, SampleAggregateState>,
        IApplyEvent<SampleEventA>,
        IApplyEvent<SampleEventB>,
        IApplyEvent<SampleEventC>
    {
        private readonly ConcurrentQueue<IAggregateEvent<SampleAggregateRoot, SampleId>> _events = new();

        public IReadOnlyCollection<IAggregateEvent<SampleAggregateRoot, SampleId>> Events { get => _events; }

        public void On(SampleEventA @event) => _events.Enqueue(@event);

        public void On(SampleEventB @event) => _events.Enqueue(@event);

        public void On(SampleEventC @event) => _events.Enqueue(@event);
    }

    internal sealed class SampleEventA : AggregateEvent<SampleAggregateRoot, SampleId>
    {
        public SampleEventA(SampleId identity) : base(identity) { }
    }

    internal sealed class SampleEventB : AggregateEvent<SampleAggregateRoot, SampleId>
    {
        public SampleEventB(SampleId identity) : base(identity) { }
    }

    internal sealed class SampleEventC : AggregateEvent<SampleAggregateRoot, SampleId>
    {
        public SampleEventC(SampleId identity) : base(identity) { }
    }

    internal sealed class SampleEventD : AggregateEvent<SampleAggregateRoot, SampleId>
    {
        public SampleEventD(SampleId identity) : base(identity) { }
    }

    public class AggregateStateTests
    {
        [Fact]
        public void CanApplyCorrectEventsBasedOnEventType()
        {
            var id = new SampleId(Guid.NewGuid());
            var state = new SampleAggregateState();

            var events = new IAggregateEvent<SampleAggregateRoot, SampleId>[] {
                new SampleEventC(id),
                new SampleEventA(id),
                new SampleEventB(id),
                new SampleEventA(id),
                new SampleEventC(id),
                new SampleEventB(id),
            };

            foreach (var e in events)
            {
                state.Apply(e);
            }

            Assert.True(events.SequenceEqual(state.Events, new EventComparer()));
        }

        [Fact]
        public void CanIgnoreEventsWithNoImplementation()
        {
            var id = new SampleId(Guid.NewGuid());
            var state = new SampleAggregateState();

            var events = new IAggregateEvent<SampleAggregateRoot, SampleId>[] {
                new SampleEventD(id),
                new SampleEventD(id),
                new SampleEventD(id),
            };

            foreach (var e in events)
            {
                state.Apply(e);
            }

            Assert.False(state.Events.Any());
        }
    }

    internal class EventComparer : IEqualityComparer<IAggregateEvent<SampleAggregateRoot, SampleId>>
    {
        public bool Equals(IAggregateEvent<SampleAggregateRoot, SampleId>? x, IAggregateEvent<SampleAggregateRoot, SampleId>? y)
        {
            if (x == null && y == null)
            {
                return true;
            }
            else if (x == null)
            {
                return false;
            }
            else if (y == null)
            {
                return false;
            }

            var result = x.GetType().Equals(y.GetType());

            if (!result)
            {
                return false;
            }

            return x.AggregateIdentity.Equals(y.AggregateIdentity);
        }

        public int GetHashCode([DisallowNull] IAggregateEvent<SampleAggregateRoot, SampleId> obj) =>
            new object[] { obj.GetType(), obj.AggregateIdentity }.Aggregate(17, (current, obj) => current * 23 + (obj?.GetHashCode() ?? 0));
    }
}