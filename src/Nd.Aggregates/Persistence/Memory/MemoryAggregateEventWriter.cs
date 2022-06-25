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

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nd.Aggregates.Events;
using Nd.Aggregates.Identities;

namespace Nd.Aggregates.Persistence.Memory
{
    public abstract class MemoryAggregateEventWriter<TIdentity, TState> : IAggregateEventWriter<TIdentity>
        where TIdentity : IAggregateIdentity
        where TState : class
    {
        private readonly ConcurrentDictionary<TIdentity, ConcurrentQueue<ICommittedEvent<TIdentity, TState>>> _events;

        protected MemoryAggregateEventWriter(ConcurrentDictionary<TIdentity, ConcurrentQueue<ICommittedEvent<TIdentity, TState>>> events)
        {
            _events = events;
        }

        public Task WriteAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellation = default) where TEvent : IUncommittedEvent<TIdentity>
        {
            if (events is null)
            {
                return Task.CompletedTask;
            }

            foreach (var e in events)
            {
                _events.GetOrAdd(e.Metadata.AggregateIdentity, (id) => new ConcurrentQueue<ICommittedEvent<TIdentity, TState>>())
                    .Enqueue(new CommittedEvent<TIdentity, TState>((IAggregateEvent<TState>)e.AggregateEvent, e.Metadata));
            }

            return Task.CompletedTask;
        }
    }

    internal class CommittedEvent<TIdentity, TState> : ICommittedEvent<TIdentity, TState>
        where TIdentity : IAggregateIdentity
        where TState : class
    {
        public CommittedEvent(IAggregateEvent<TState> aggregateEvent, IAggregateEventMetadata<TIdentity> metadata)
        {
            AggregateEvent = aggregateEvent;
            Metadata = metadata;
        }

        public IAggregateEvent<TState> AggregateEvent { get; }

        public IAggregateEventMetadata<TIdentity> Metadata { get; }
    }
}
