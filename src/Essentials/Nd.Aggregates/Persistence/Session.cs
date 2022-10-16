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
using Nd.Core.Extensions;
using Nd.Core.Threading;
using Nd.Identities;

namespace Nd.Aggregates.Persistence
{
    public class Session : ISession
    {
        private readonly IAsyncLocker _locker = ExclusiveAsyncLocker.Create();
        private readonly List<IPendingEvent> _events = new();
        private readonly List<IIdempotencyIdentity> _idempotencyIdentities = new();
        private readonly IAggregateEventReader _reader;
        private readonly IAggregateEventWriter _writer;

        public Session(IAggregateEventReader reader, IAggregateEventWriter writer)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        public async Task CommitAsync(CancellationToken cancellation = default)
        {
            using var @lock = _locker.WaitAsync(cancellation);

            try
            {
                // Trying to store all of the existingEvents copied from
                // the aggregate event-list.
                await _writer.WriteAsync(_events.ToImmutableArray(), cancellation)
                    .ConfigureAwait(false);

                _events.Clear();
                _idempotencyIdentities.Clear();
            }
            catch (Exception ex)
            {
                // Wrap the exception and throw it.
                throw new SessionPersistenceException(ex);
            }
        }

        public void EnqueuePendingEvents(params IPendingEvent[] pendingEvents)
        {
            if (pendingEvents is null)
            {
                throw new ArgumentNullException(nameof(pendingEvents));
            }

            using var @lock = _locker.Wait();

            var idempotencyIdentities = pendingEvents.Select(e => e.Metadata.IdempotencyIdentity).ToImmutableArray();

            var conflicts = _idempotencyIdentities.Intersect(idempotencyIdentities).ToImmutableArray();

            if (conflicts.Any())
            {
                throw new RedundantAggregateEventException(conflicts);
            }

            _events.AddRange(pendingEvents);
            _idempotencyIdentities.AddRange(idempotencyIdentities);
        }

        public async Task<TAggregate> LoadAsync<TIdentity, TAggregate>(TIdentity identity, ICorrelationIdentity correlationId, uint version = 0, CancellationToken cancellation = default)
            where TAggregate : notnull, IAggregateRoot<TIdentity>
            where TIdentity : notnull, IAggregateIdentity
        {
            if (identity is null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            using var @lock = _locker.WaitAsync(cancellation);

            var discardedEvents = _events.Where(e => e.Metadata.AggregateIdentity.Equals(identity)).ToImmutableArray();

            if (discardedEvents.Any())
            {
                var existingEvents = _events.ToImmutableArray();
                var existingIdempotencyIdentities = _idempotencyIdentities.ToImmutableArray();
                var discardedIdempotencyIdentities = discardedEvents.Select(e => e.Metadata.IdempotencyIdentity).ToImmutableArray();

                _events.Clear();
                _events.AddRange(existingEvents.Except(discardedEvents));
                _idempotencyIdentities.Clear();
                _idempotencyIdentities.AddRange(existingIdempotencyIdentities.Except(discardedIdempotencyIdentities));
            }

            var factory = identity
                    .CreateAggregateFactory()
                    .ConfigureIdentity(identity);

            await foreach (var @event in _reader
                .ReadAsync(identity, correlationId, version, cancellation)
                .ConfigureAwait(false))
            {
                IAggregateEvent aggregateEvent;

                try
                {
                    aggregateEvent = await @event.AggregateEvent.UpgradeRecursiveAsync(cancellation).ConfigureAwait(false) is IAggregateEvent e ? e :
                        throw new InvalidCastException($"Failed to cast upgraded version of event '{@event.AggregateEvent.TypeName}' to type '{nameof(IAggregateEvent)}'");
                }
                catch (Exception ex)
                {
                    throw new EventUpgradeException(@event.AggregateEvent.TypeName, ex);
                }

                try
                {
                    factory = factory
                    .ConfigureVersion(@event.Metadata.AggregateVersion)
                    .ConfigureState(state => state.Apply(aggregateEvent));
                }
                catch (Exception ex)
                {
                    throw new EventUpgradeException(@event.AggregateEvent.TypeName, ex);
                }
            }

            try
            {
                return (TAggregate)factory.Build(this) ?? throw new AggregateCreationException(typeof(TAggregate).ResolveName(),
                    new AggregateCreationException($"Aggregate factory {factory.GetType().ResolveName()} returns a null aggregate"));
            }
            catch (Exception exception)
            {
                throw new AggregateCreationException(typeof(TAggregate).ResolveName(), exception);
            }
        }
    }
}
