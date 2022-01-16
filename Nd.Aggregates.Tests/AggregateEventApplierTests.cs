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
using Nd.ValueObjects.Identities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Nd.Aggregates.Tests
{
    internal sealed record class SampleId : Identity<SampleId>
    {
        public SampleId(Guid value) : base(value) { }
    }

    internal interface ISampleAggregateState
    {

    }

    internal sealed class SampleAggregateRoot : AggregateRoot<SampleAggregateRoot, SampleId, SampleAggregateEventApplier, ISampleAggregateState>
    {
        public SampleAggregateRoot(SampleId identity) : base(identity) { }
    }

    internal abstract record class SampleAggregateEvent<TEvent>
        : AggregateEvent<TEvent, SampleAggregateRoot, SampleId, SampleAggregateEventApplier>
        where TEvent : AggregateEvent<TEvent, SampleAggregateRoot, SampleId, SampleAggregateEventApplier>;

    internal interface IApplySampleAggregateEvent<TEvent> : IAggregateEventHandler<TEvent, SampleAggregateRoot, SampleId, SampleAggregateEventApplier>
            where TEvent : SampleAggregateEvent<TEvent>
    {

    }

    internal sealed class SampleAggregateEventApplier : AggregateEventApplier<SampleAggregateEventApplier, SampleAggregateRoot, SampleId>,
        IApplySampleAggregateEvent<SampleEventA>,
        IApplySampleAggregateEvent<SampleEventB>,
        IApplySampleAggregateEvent<SampleEventC>,
        ISampleAggregateState
    {
        private readonly ConcurrentQueue<IAggregateEvent<SampleAggregateRoot, SampleId, SampleAggregateEventApplier>> _events = new();

        public IReadOnlyCollection<IAggregateEvent<SampleAggregateRoot, SampleId, SampleAggregateEventApplier>> Events { get => _events; }

        public void On(SampleEventA @event) => _events.Enqueue(@event);

        public void On(SampleEventB @event) => _events.Enqueue(@event);

        public void On(SampleEventC @event) => _events.Enqueue(@event);
    }

    internal sealed record class SampleEventA : SampleAggregateEvent<SampleEventA> { }

    internal sealed record class SampleEventB : SampleAggregateEvent<SampleEventB> { }

    internal sealed record class SampleEventC : SampleAggregateEvent<SampleEventC> { }

    internal sealed record class SampleEventD : SampleAggregateEvent<SampleEventD> { }

    public class AggregateEventApplierTests
    {
        [Fact]
        public void CanApplyCorrectEventsBasedOnEventType()
        {
            var state = new SampleAggregateEventApplier();

            var events = new IAggregateEvent<SampleAggregateRoot, SampleId, SampleAggregateEventApplier>[] {
                new SampleEventC(),
                new SampleEventA(),
                new SampleEventB(),
                new SampleEventA(),
                new SampleEventC(),
                new SampleEventB()
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
            var state = new SampleAggregateEventApplier();

            var events = new IAggregateEvent<SampleAggregateRoot, SampleId, SampleAggregateEventApplier>[] {
                new SampleEventD(),
                new SampleEventD(),
                new SampleEventD()
            };

            foreach (var e in events)
            {
                state.Apply(e);
            }

            Assert.False(state.Events.Any());
        }
    }
}