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

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Nd.Aggregates.Identities;
using Nd.Aggregates.Persistence;
using Nd.Identities;

namespace Nd.Extensions.Stores.Memory.Aggregates
{
    public abstract class MemoryAggregateEventReader<TIdentity> : IAggregateEventReader<TIdentity>
        where TIdentity : IAggregateIdentity
    {
        private readonly IDictionary<TIdentity, IReadOnlyCollection<ICommittedEvent<TIdentity>>> _events;

        protected MemoryAggregateEventReader(IDictionary<TIdentity, IReadOnlyCollection<ICommittedEvent<TIdentity>>> events)
        {
            _events = events;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async IAsyncEnumerable<ICommittedEvent<TIdentity>> ReadAsync(
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
            TIdentity aggregateId,
            ICorrelationIdentity correlationId,
            uint versionStart,
            uint versionEnd,
            [EnumeratorCancellation] CancellationToken cancellation = default)
        {
            if (_events.TryGetValue(aggregateId, out var events))
            {
                foreach (var @event in events
                .Where(e =>
                    e.Metadata.AggregateVersion >= versionStart &&
                    (versionEnd == 0u || e.Metadata.AggregateVersion <= versionEnd))
                .OrderBy(e => e.Metadata.AggregateVersion))
                {
                    yield return @event;
                }
            }

            yield break;
        }
    }
}
