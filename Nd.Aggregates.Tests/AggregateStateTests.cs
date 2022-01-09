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

    internal sealed class SampleAggregateRoot : AggregateRoot<SampleAggregateRoot, SampleId, SampleAggregateState>
    {
        public SampleAggregateRoot(SampleId identity) : base(identity) { }
    }
    internal sealed class SampleAggregateState : AggregateState<SampleAggregateState, SampleAggregateRoot, SampleId>,
        IApplyEvent<SampleEventA>,
        IApplyEvent<SampleEventB>,
        IApplyEvent<SampleEventC>
    {
        private readonly ConcurrentQueue<IEvent<SampleAggregateRoot, SampleId, SampleAggregateState>> _events = new();

        public IReadOnlyCollection<IEvent<SampleAggregateRoot, SampleId, SampleAggregateState>> Events { get => _events; }

        public void On(SampleEventA @event) => _events.Enqueue(@event);

        public void On(SampleEventB @event) => _events.Enqueue(@event);

        public void On(SampleEventC @event) => _events.Enqueue(@event);
    }

    internal sealed record class SampleEventA : Event<SampleAggregateRoot, SampleId, SampleAggregateState>
    {
        public SampleEventA(IEventMetaData<SampleAggregateRoot, SampleId, SampleAggregateState> EventMetaData) : base(EventMetaData) { }
    }

    internal sealed record class SampleEventB : Event<SampleAggregateRoot, SampleId, SampleAggregateState>
    {
        public SampleEventB(IEventMetaData<SampleAggregateRoot, SampleId, SampleAggregateState> EventMetaData) : base(EventMetaData) { }
    }

    internal sealed record class SampleEventC : Event<SampleAggregateRoot, SampleId, SampleAggregateState>
    {
        public SampleEventC(IEventMetaData<SampleAggregateRoot, SampleId, SampleAggregateState> EventMetaData) : base(EventMetaData) { }
    }

    internal sealed record class SampleEventD : Event<SampleAggregateRoot, SampleId, SampleAggregateState>
    {
        public SampleEventD(IEventMetaData<SampleAggregateRoot, SampleId, SampleAggregateState> EventMetaData) : base(EventMetaData) { }
    }

    public class AggregateStateTests
    {
        private static IEventMetaData<SampleAggregateRoot, SampleId, SampleAggregateState> CreateEventMeta<T>(SampleId id) =>
            new EventMetaData<SampleAggregateRoot, SampleId, SampleAggregateState>(
                    new EventId(RandomGuidFactory.Instance),
                    typeof(T).Name,
                    0,
                    new SourceId(RandomGuidFactory.Instance),
                    id,
                    0,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                );

        [Fact]
        public void CanApplyCorrectEventsBasedOnEventType()
        {
            var id = new SampleId(Guid.NewGuid());
            var state = new SampleAggregateState();

            var events = new IEvent<SampleAggregateRoot, SampleId, SampleAggregateState>[] {
                new SampleEventC(CreateEventMeta<SampleEventC>(id)),
                new SampleEventA(CreateEventMeta<SampleEventA>(id)),
                new SampleEventB(CreateEventMeta<SampleEventB>(id)),
                new SampleEventA(CreateEventMeta<SampleEventA>(id)),
                new SampleEventC(CreateEventMeta<SampleEventC>(id)),
                new SampleEventB(CreateEventMeta<SampleEventB>(id))
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
            var id = new SampleId(Guid.NewGuid());
            var state = new SampleAggregateState();

            var events = new IEvent<SampleAggregateRoot, SampleId, SampleAggregateState>[] {
                new SampleEventD(CreateEventMeta<SampleEventD>(id)),
                new SampleEventD(CreateEventMeta<SampleEventD>(id)),
                new SampleEventD(CreateEventMeta<SampleEventD>(id))
            };

            foreach (var e in events)
            {
                state.Apply(e);
            }

            Assert.False(state.Events.Any());
        }
    }
}