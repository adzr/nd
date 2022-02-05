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
using Nd.Aggregates.Exceptions;
using Nd.Aggregates.Snapshots;
using Nd.Core.Extensions;
using Nd.Core.Types.Versions;
using Nd.Identities;

namespace Nd.Aggregates.Persistence
{
    public static class AggregateReaderExtensions
    {
        public static Task TakeSnapshotAsync<TAggregate, TIdentity, TEventApplier, TState>(
            this AggregateRoot<TAggregate, TIdentity, TEventApplier, TState> aggregate,
            IAggregateSnapshotWriter<TIdentity> writer, CancellationToken cancellation = default)
            where TAggregate : AggregateRoot<TAggregate, TIdentity, TEventApplier, TState>
            where TIdentity : IIdentity<TIdentity>
            where TEventApplier : IAggregateEventApplier<TAggregate, TIdentity>, TState
            where TState : class, IVersionedType
                => writer.WriteAsync(new AggregateSnapshot<TIdentity, TState>(aggregate.State, aggregate.Version, aggregate.Identity, aggregate.TypeName), cancellation);

        public static async Task<TAggregate?> ReadAsync<TAggregate, TIdentity, TEventApplier, TState>(
            TIdentity aggregateId, uint version,
            IAggregateEventReader<TAggregate, TIdentity, TEventApplier> eventReader,
            IAggregateSnapshotReader<TIdentity> snapshotReader,
            IAggregateFactory<TAggregate, TIdentity, TEventApplier, TState> aggregateFactory,
            IAggregateEventApplierFactory<TEventApplier>? aggregateStateFactory = default,
            CancellationToken cancellation = default)
            where TAggregate : AggregateRoot<TAggregate, TIdentity, TEventApplier, TState>
            where TIdentity : IIdentity<TIdentity>
            where TEventApplier : IAggregateEventApplier<TAggregate, TIdentity>, TState
            where TState : class, IVersionedType
        {
            if (aggregateId is null)
            {
                throw new ArgumentNullException(nameof(aggregateId));
            }

            List<ICommittedEvent> events = new();

            IAggregateSnapshot<TIdentity, TState>? snapshot = null;

            var storedSnapshot = await snapshotReader.ReadAsync<TState>(aggregateId, version, cancellation).ConfigureAwait(false);

            if (storedSnapshot is not null)
            {


                try
                {
                    if (await storedSnapshot.State.UpgradeRecursiveAsync(cancellation).ConfigureAwait(false) is TState upgradedState)
                    {
                        snapshot = new AggregateSnapshot<TIdentity, TState>(
                            upgradedState,
                            storedSnapshot.AggregateVersion,
                            storedSnapshot.AggregateIdentity,
                            storedSnapshot.AggregateName);
                    }
                    else
                    {
                        throw new InvalidCastException($"Failed to cast upgraded version of state '{storedSnapshot.State.TypeName}' to type '{typeof(TState).Name}'");
                    }
                }
                catch (Exception ex)
                {
                    throw new StateUpgradeException(storedSnapshot.State.TypeName, ex);
                }
            }

            events.AddRange(await eventReader.ReadAsync<ICommittedEvent<TAggregate, TIdentity, TEventApplier>>(
                aggregateId,
                snapshot is null ? 0 : snapshot.AggregateVersion + 1,
                version,
                cancellation).ConfigureAwait(false));

            if (!events.Any())
            {
                return default;
            }

            (TAggregate aggregate, TEventApplier state) = Aggregates
                .CreateAggregateAndState(aggregateId, events.Max(e => e.MetaData.AggregateVersion), aggregateFactory, aggregateStateFactory);

            if (snapshot is not null)
            {
                if (state is ICanConsumeState<TState> stateConsumer)
                {
                    stateConsumer.ConsumeState(snapshot.State);
                }
                else
                {
                    throw new InvalidOperationException($"Aggregate state {state.GetType().GetNameAndVersion()} " +
                        $"must implement {nameof(ICanConsumeState<TState>)} to support snapshot loading");
                }
            }

            foreach (var @event in events)
            {
                try
                {
                    if (await @event.Event.UpgradeRecursiveAsync(cancellation).ConfigureAwait(false) is IAggregateEvent e)
                    {
                        state.Apply(e);
                    }
                    else
                    {
                        throw new InvalidCastException($"Failed to cast upgraded version of event '{@event.Event.TypeName}' to type '{nameof(IAggregateEvent)}'");
                    }
                }
                catch (Exception ex)
                {
                    throw new EventUpgradeException(@event.Event.TypeName, ex);
                }
            }

            return aggregate;
        }
    }
}