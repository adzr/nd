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
using Nd.Aggregates.Events;
using Nd.Aggregates.Exceptions;
using Nd.Aggregates.Extensions;
using Nd.Aggregates.Identities;
using Nd.Aggregates.Persistence;
using Nd.Core.Extensions;
using Nd.Core.Types.Versions;

namespace Nd.Aggregates.Snapshots.Extensions
{
    public static class AggregateReaderExtensions
    {
        public static async Task<TAggregate> ReadAsync<TIdentity, TState, TAggregate>(
            TIdentity aggregateId, uint version,
            IAggregateEventReader<TIdentity, TState> eventReader,
            IAggregateSnapshotReader<TIdentity> snapshotReader,
            AggregateFactoryFunc<TIdentity, TState, TAggregate> initializeAggregate,
            AggregateStateFactoryFunc<TState> initializeState,
            CancellationToken cancellation = default)
            where TAggregate : notnull, AggregateRoot<TIdentity, TState>
            where TIdentity : notnull, IAggregateIdentity
            where TState : notnull, IVersionedType
        {

            if (aggregateId is null)
            {
                throw new ArgumentNullException(nameof(aggregateId));
            }

            if (snapshotReader is null)
            {
                throw new ArgumentNullException(nameof(snapshotReader));
            }

            if (eventReader is null)
            {
                throw new ArgumentNullException(nameof(eventReader));
            }

            List<ICommittedEvent> events = new();

            IAggregateSnapshot<TIdentity, TState>? snapshot = null;

            var storedSnapshot = await snapshotReader.ReadAsync<TState>(aggregateId, version, cancellation).ConfigureAwait(false);

            if (storedSnapshot is not null)
            {
                try
                {
                    snapshot = await storedSnapshot.State.UpgradeRecursiveAsync(cancellation).ConfigureAwait(false) is TState upgradedState
                        ? (IAggregateSnapshot<TIdentity, TState>)new AggregateSnapshot<TIdentity, TState>(
                            upgradedState,
                            storedSnapshot.AggregateVersion,
                            storedSnapshot.AggregateIdentity,
                            storedSnapshot.AggregateName)
                        : throw new InvalidCastException($"Failed to cast upgraded version of state '{storedSnapshot.State.TypeName}' to type '{typeof(TState).Name}'");
                }
                catch (Exception ex)
                {
                    throw new StateUpgradeException(storedSnapshot.State.TypeName, ex);
                }
            }

            events.AddRange(await eventReader.ReadAsync<ICommittedEvent<TIdentity, TState>>(
                aggregateId,
                snapshot is null ? 0 : snapshot.AggregateVersion + 1,
                version,
                cancellation).ConfigureAwait(false));

            (var aggregate, var state) = aggregateId
                .CreateAggregateAndState(initializeAggregate, initializeState, events.Max(e => e.Metadata.AggregateVersion));

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

            return aggregate;
        }
    }
}
