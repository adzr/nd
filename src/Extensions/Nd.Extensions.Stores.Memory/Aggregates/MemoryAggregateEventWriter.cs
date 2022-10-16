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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nd.Aggregates.Events;
using Nd.Aggregates.Exceptions;
using Nd.Aggregates.Identities;
using Nd.Aggregates.Persistence;
using Nd.Core.Threading;

namespace Nd.Extensions.Stores.Memory.Aggregates
{
    public abstract class MemoryAggregateEventWriter : IAggregateEventWriter
    {
        private readonly Dictionary<IAggregateIdentity, Queue<ICommittedEvent>> _events;

        private readonly IAsyncLocker _asyncLocker = ExclusiveAsyncLocker.Create();

        protected MemoryAggregateEventWriter(Dictionary<IAggregateIdentity, Queue<ICommittedEvent>> events)
        {
            _events = events;
        }

        public async Task WriteAsync<TEvent>(IEnumerable<TEvent> events, CancellationToken cancellation = default)
            where TEvent : notnull, IPendingEvent
        {
            if (events is null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            var aggregates = events
                .GroupBy(e => e.Metadata.AggregateIdentity)
                .Select(g => (Identity: g.Key, Events: g.OrderBy(e => e.Metadata.AggregateVersion).ToImmutableArray()))
                .ToImmutableArray();

            foreach (var (identity, aggregateEvents) in aggregates)
            {
                cancellation.ThrowIfCancellationRequested();

                ValidatePendingEventSequence(identity, aggregateEvents);
            }

            using var @lock = await _asyncLocker.WaitAsync(cancellation).ConfigureAwait(false);

            foreach (var (identity, aggregateEvents) in aggregates)
            {
                cancellation.ThrowIfCancellationRequested();

                var (startVersion, endVersion) =
                    (aggregateEvents.First().Metadata.AggregateVersion,
                    aggregateEvents.Last().Metadata.AggregateVersion);

                if (!_events.TryGetValue(identity, out var queue))
                {
                    queue = new Queue<ICommittedEvent>();
                    _events.Add(identity, queue);
                }

                var versions = queue.Select(e => e.Metadata.AggregateVersion).OrderBy(v => v).ToImmutableArray();

                if (!versions.Max().Equals(aggregateEvents.Select(e => e.Metadata.AggregateVersion).Min() + 1))
                {
                    throw new AggregateOutOfSyncException(identity, startVersion, endVersion);
                }

                foreach (var @event in aggregateEvents)
                {
                    queue.Enqueue(new CommittedEvent(@event.AggregateEvent, @event.Metadata));
                }
            }
        }

        private static void ValidatePendingEventSequence<TEvent>(IAggregateIdentity identity, ImmutableArray<TEvent> events) where TEvent : notnull, IPendingEvent
        {
            var versions = events
                .Select(e => e.Metadata.AggregateVersion)
                .ToImmutableArray();

            if (!versions.Any())
            {
                throw new InvalidEventSequenceException($"Aggregate {identity} has no pending event sequence");
            }

            var expectedVersion = versions.First();

            foreach (var version in versions)
            {
                if (expectedVersion < 1)
                {
                    throw new InvalidEventSequenceException($"Aggregate {identity} cannot have event version less than 0, found {version}");
                }

                if (expectedVersion != version)
                {
                    throw new InvalidEventSequenceException($"Aggregate {identity} has unexpected pending event version sequence, expected {expectedVersion} found {version}");
                }

                expectedVersion++;
            }
        }
    }

    internal class CommittedEvent : ICommittedEvent
    {
        public CommittedEvent(IAggregateEvent aggregateEvent, IAggregateEventMetadata metadata)
        {
            AggregateEvent = aggregateEvent;
            Metadata = metadata;
        }

        public IAggregateEvent AggregateEvent { get; }

        public IAggregateEventMetadata Metadata { get; }
    }
}
