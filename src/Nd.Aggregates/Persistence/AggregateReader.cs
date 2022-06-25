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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nd.Aggregates.Events;
using Nd.Aggregates.Exceptions;
using Nd.Aggregates.Extensions;
using Nd.Aggregates.Identities;
using Nd.Core.Extensions;

namespace Nd.Aggregates.Persistence
{
    public abstract class AggregateReader<TIdentity, TState> : IAggregateReader<TIdentity>
        where TIdentity : IAggregateIdentity
        where TState : class
    {
        private readonly AggregateFactoryFunc<TIdentity, TState, IAggregateRoot<TIdentity, TState>> _aggregateFactory;
        private readonly AggregateStateFactoryFunc<TState> _stateFactory;
        private readonly IAggregateEventReader<TIdentity, TState> _eventReader;

        protected AggregateReader(AggregateStateFactoryFunc<TState> stateFactory,
            AggregateFactoryFunc<TIdentity, TState, IAggregateRoot<TIdentity, TState>> aggregateFactory,
            IAggregateEventReader<TIdentity, TState> eventReader)
        {
            _aggregateFactory = aggregateFactory ?? throw new ArgumentNullException(nameof(aggregateFactory));
            _stateFactory = stateFactory ?? throw new ArgumentNullException(nameof(stateFactory));
            _eventReader = eventReader ?? throw new ArgumentNullException(nameof(eventReader));
        }

        public async Task<TAggregate> ReadAsync<TAggregate>(TIdentity aggregateId,
                uint version = 0u, CancellationToken cancellation = default)
            where TAggregate : IAggregateRoot<TIdentity>
        {
            if (aggregateId is null)
            {
                throw new ArgumentNullException(nameof(aggregateId));
            }

            var events = await _eventReader.ReadAsync<ICommittedEvent<TIdentity, TState>>(aggregateId, version, cancellation)
                .ConfigureAwait(false);

            (var aggregate, var state) = aggregateId
                .CreateAggregateAndState(_aggregateFactory, _stateFactory, events.Max(e => e.Metadata.AggregateVersion));

            foreach (var @event in events)
            {
                try
                {
                    if (await @event.AggregateEvent.UpgradeRecursiveAsync(cancellation).ConfigureAwait(false) is IAggregateEvent e)
                    {
                        state.Apply(e);
                    }
                    else
                    {
                        throw new InvalidCastException($"Failed to cast upgraded version of event '{@event.AggregateEvent.TypeName}' to type '{nameof(IAggregateEvent)}'");
                    }
                }
                catch (Exception ex)
                {
                    throw new EventUpgradeException(@event.AggregateEvent.TypeName, ex);
                }
            }

            return (TAggregate)aggregate;
        }
    }
}
