﻿/*
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nd.Aggregates.Identities;
using Nd.Aggregates.Persistence;

namespace Nd.Extensions.Stores.Memory.Aggregates
{
    public abstract class MemoryAggregateEventReader<TIdentity, TState> : IAggregateEventReader<TIdentity, TState>
        where TIdentity : IAggregateIdentity
        where TState : notnull
    {
        private readonly IDictionary<TIdentity, IReadOnlyCollection<ICommittedEvent<TIdentity, TState>>> _events;

        protected MemoryAggregateEventReader(IDictionary<TIdentity, IReadOnlyCollection<ICommittedEvent<TIdentity, TState>>> events)
        {
            _events = events;
        }

        public Task<IEnumerable<TEvent>> ReadAsync<TEvent>(TIdentity aggregateId, uint versionStart, uint versionEnd, CancellationToken cancellation = default)
            where TEvent : ICommittedEvent<TIdentity, TState> =>
            Task.FromResult(_events.TryGetValue(aggregateId, out var events) ?
                events
                .Where(e => e.Metadata.AggregateVersion >= versionStart && (versionEnd == 0u || e.Metadata.AggregateVersion <= versionEnd))
                .Select(e => (TEvent)e) :
                Array.Empty<TEvent>());
    }
}