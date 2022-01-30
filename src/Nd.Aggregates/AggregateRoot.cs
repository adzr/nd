/*
 * Copyright © 2015 - 2021 Rasmus Mikkelsen
 * Copyright © 2015 - 2021 eBay Software Foundation
 * Modified from original source https://github.com/eventflow/EventFlow
 * 
 * Copyright © 2018 - 2021 Lutando Ngqakaza
 * Modified from original source https://github.com/Lutando/Akkatecture
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

using Nd.Aggregates.Events;
using Nd.Aggregates.Exceptions;
using Nd.Aggregates.Identities;
using Nd.Aggregates.Persistence;
using Nd.Core.Extensions;
using Nd.Core.Factories;
using Nd.Identities;

namespace Nd.Aggregates
{
    public abstract class AggregateRoot<TAggregate, TIdentity, TEventApplier, TState> : IAggregateRoot<TIdentity, TState>
        where TAggregate : AggregateRoot<TAggregate, TIdentity, TEventApplier, TState>
        where TIdentity : IIdentity<TIdentity>
        where TEventApplier : IAggregateEventApplier<TAggregate, TIdentity>, TState
        where TState : class
    {
        private static readonly string AggregateTypeName = typeof(TAggregate).GetName();
        private static readonly IAggregateEventApplierFactory<TEventApplier> DefaultEventApplierFactory = new DefaultAggregateEventApplierFactory<TEventApplier>();

        private readonly object _uncommittedEventsLock = new();

        private readonly List<IUncommittedEvent<TAggregate, TIdentity, TEventApplier>> _uncommittedEvents = new();

        private readonly List<IIdempotencyIdentity> _idempotencyCheckList = new();

        private readonly TEventApplier _eventApplier;

        protected AggregateRoot(TIdentity identity, TEventApplier? eventApplier = default, uint version = default)
        {
            if (this is not TAggregate)
            {
                throw new InvalidOperationException(
                    $"Aggregate '{GetType().ToPrettyString()}' specifies '{typeof(TAggregate).ToPrettyString()}' as generic argument, it should be its own type");
            }

            _eventApplier = eventApplier ?? EventApplierFactory.Create();

            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Version = version;
        }

        protected virtual IAggregateEventApplierFactory<TEventApplier> EventApplierFactory => DefaultEventApplierFactory;

        public TState State => _eventApplier;

        public TIdentity Identity { get; }

        IIdentity IAggregateRoot.Identity => Identity;

        public string TypeName => AggregateTypeName;

        public uint Version { get; private set; }

        public bool IsNew => Version == 0;

        public bool HasPendingChanges => _uncommittedEvents.Any();

        protected virtual void Emit<TEvent>(TEvent @event, IAggregateEventMetaData? metaData = default, bool failOnDuplicates = true, Func<DateTimeOffset>? currentTimestampProvider = default)
            where TEvent : AggregateEvent<TEvent, TAggregate, TIdentity, TEventApplier>
        {
            if (@event == null)
            {
                throw new ArgumentNullException(nameof(@event));
            }

            if (metaData is not null && _idempotencyCheckList.Contains(metaData.IdempotencyIdentity))
            {
                if (failOnDuplicates)
                {
                    throw new DuplicateAggregateEventException(@event, metaData);
                }

                return;
            }

            // Creating event meta-data to be stored along side the event.
            var meta = new AggregateEventMetaData<TAggregate, TIdentity>
            (
                metaData?.IdempotencyIdentity ?? new IdempotencyIdentity(RandomGuidFactory.Instance),
                metaData?.CorrelationIdentity ?? new CorrelationIdentity(RandomGuidFactory.Instance),
                new AggregateEventIdentity(DeterministicGuidFactory.Instance(AggregateEventIdentity.NamespaceIdentifier, $"{Identity.Value}-v{Version + 1}")),
                @event.TypeName,
                @event.TypeVersion,
                Identity,
                Version + 1,
                currentTimestampProvider?.Invoke() ?? DateTimeOffset.UtcNow
            );

            lock (_uncommittedEventsLock)
            {
                _eventApplier.Apply(@event);
                _idempotencyCheckList.Add(meta.IdempotencyIdentity);
                _uncommittedEvents.Add(new UncommittedEvent<TAggregate, TIdentity, TEventApplier>(@event, meta));
                Version++;
            }
        }

        public virtual async Task CommitAsync(IAggregateEventWriter<TAggregate, TIdentity, TEventApplier> writer, CancellationToken cancellation = default)
        {
            // Creating a list for the events about to be stored.
            var outgoingEvents = new List<IUncommittedEvent<TAggregate, TIdentity, TEventApplier>>();
            var outgoingIdempotencyIds = new List<IIdempotencyIdentity>();

            // Freezing the aggregate event-list and copying all
            // of its events into the new list just created,
            // then clearing the event-list and de-freezing it.
            lock (_uncommittedEventsLock)
            {
                outgoingEvents.AddRange(_uncommittedEvents);
                outgoingIdempotencyIds.AddRange(_idempotencyCheckList);
                _uncommittedEvents.Clear();
                _idempotencyCheckList.Clear();
            }

            try
            {
                // Trying to store all of the events copied from
                // the aggregate event-list.
                await writer.WriteAsync(outgoingEvents, cancellation)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // In case of a failure, freeze the aggregate
                // event-list again and put back the
                // failed-to-store events into the beginning
                // of the event-list then de-freeze.
                lock (_uncommittedEventsLock)
                {
                    var incomingEvents = new List<IUncommittedEvent<TAggregate, TIdentity, TEventApplier>>(_uncommittedEvents);
                    _uncommittedEvents.Clear();
                    _uncommittedEvents.AddRange(outgoingEvents);
                    _uncommittedEvents.AddRange(incomingEvents);

                    var incomingIdempotencyIds = new List<IIdempotencyIdentity>(_idempotencyCheckList);
                    _idempotencyCheckList.Clear();
                    _idempotencyCheckList.AddRange(outgoingIdempotencyIds);
                    _idempotencyCheckList.AddRange(incomingIdempotencyIds);
                }

                // Wrap the exception and throw it.
                throw new AggregatePersistenceException(AggregateTypeName, Identity, ex);
            }
        }
    }
}