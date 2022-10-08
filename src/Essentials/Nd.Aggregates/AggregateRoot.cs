/*
 * Copyright © 2015 - 2021 Rasmus Mikkelsen
 * Copyright © 2015 - 2021 eBay Software Foundation
 * Modified from original source https://github.com/eventflow/EventFlow
 * 
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
using Nd.Aggregates.Events;
using Nd.Aggregates.Identities;
using Nd.Aggregates.Persistence;
using Nd.Core.Factories;
using Nd.Identities;

namespace Nd.Aggregates
{
    public abstract class AggregateRoot<TIdentity, TState> : IAggregateRoot<TIdentity, TState>
        where TIdentity : notnull, IAggregateIdentity
        where TState : notnull
    {
        private readonly IAggregateState<TState> _state;
        private readonly ISession _session;

        private readonly object _lock = new();

        protected AggregateRoot(TIdentity identity, IAggregateState<TState> state, ISession session)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public TState State => _state.State;

        public TIdentity Identity { get; }

        IAggregateIdentity IAggregateRoot.Identity => Identity;

        public uint Version { get; protected set; }

        public bool IsNew => Version == 0u;

        protected virtual void Emit<TEvent>(TEvent aggregateEvent, IAggregateEventMetadata? metadata = default, Func<DateTimeOffset>? currentTimestampProvider = default)
            where TEvent : AggregateEvent<TState>
        {
            if (aggregateEvent == null)
            {
                throw new ArgumentNullException(nameof(aggregateEvent));
            }

            lock (_lock)
            {
                // Creating event meta-data to be stored along side the event.
                var meta = new AggregateEventMetadata<TIdentity>
                (
                    metadata?.IdempotencyIdentity ?? new IdempotencyIdentity(RandomGuidFactory.Instance),
                    metadata?.CorrelationIdentity ?? new CorrelationIdentity(RandomGuidFactory.Instance),
                    Identity,
                    new AggregateEventIdentity(DeterministicGuidFactory.Instance(AggregateEventIdentity.NamespaceIdentifier, $"{Identity}-V{Version + 1}")),
                    aggregateEvent.TypeName,
                    aggregateEvent.TypeVersion,
                    Version + 1,
                    currentTimestampProvider?.Invoke() ?? DateTimeOffset.UtcNow
                );

                _session.EnqueuePendingEvents(new PendingEvent<TIdentity>(aggregateEvent, meta));

                _state.Apply(aggregateEvent);

                Version++;
            }
        }
    }
}
