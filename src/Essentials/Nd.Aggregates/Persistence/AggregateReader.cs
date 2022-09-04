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
using System.Threading;
using System.Threading.Tasks;
using Nd.Aggregates.Events;
using Nd.Aggregates.Exceptions;
using Nd.Aggregates.Identities;
using Nd.Core.Extensions;
using Nd.Identities;

namespace Nd.Aggregates.Persistence
{
    public abstract class AggregateReader<TIdentity, TState> : IAggregateReader<TIdentity>
        where TIdentity : notnull, IAggregateIdentity
        where TState : notnull
    {
        private readonly AggregateFactoryFunc<TIdentity, TState, IAggregateRoot<TIdentity, TState>> _aggregateFactory;
        private readonly AggregateStateFactoryFunc<TState> _stateFactory;
        private readonly IAggregateEventReader<TIdentity> _eventReader;

        protected AggregateReader(AggregateStateFactoryFunc<TState> stateFactory,
            AggregateFactoryFunc<TIdentity, TState, IAggregateRoot<TIdentity, TState>> aggregateFactory,
            IAggregateEventReader<TIdentity> eventReader)
        {
            _aggregateFactory = aggregateFactory ?? throw new ArgumentNullException(nameof(aggregateFactory));
            _stateFactory = stateFactory ?? throw new ArgumentNullException(nameof(stateFactory));
            _eventReader = eventReader ?? throw new ArgumentNullException(nameof(eventReader));
        }

        public async Task<TAggregate> ReadAsync<TAggregate>(TIdentity aggregateId,
                ICorrelationIdentity correlationId, uint version = 0u, CancellationToken cancellation = default)
            where TAggregate : notnull, IAggregateRoot<TIdentity>
        {
            if (aggregateId is null)
            {
                throw new ArgumentNullException(nameof(aggregateId));
            }

            IAggregateState<TState> state;

            try
            {
                state = _stateFactory() ?? throw new AggregateStateCreationException(typeof(TState));
            }
            catch (Exception exception)
            {
                throw new AggregateStateCreationException(typeof(TState), exception);
            }

            var lastEventVersion = 0u;

            await foreach (var @event in _eventReader
                .ReadAsync(aggregateId, correlationId, version, cancellation)
                .ConfigureAwait(false))
            {
                try
                {
                    lastEventVersion = @event.Metadata.AggregateVersion;

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

            try
            {
                return (TAggregate)_aggregateFactory(aggregateId, () => state, lastEventVersion) ??
                    throw new AggregateCreationException(typeof(TAggregate).GetName());
            }
            catch (Exception exception)
            {
                throw new AggregateCreationException(typeof(TAggregate).GetName(), exception);
            }
        }
    }
}
