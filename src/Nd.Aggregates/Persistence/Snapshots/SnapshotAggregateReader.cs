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
using Nd.ValueObjects.Identities;

namespace Nd.Aggregates.Persistence.Snapshots
{
    public sealed class SnapshotAggregateReader : IAggregateReader
    {
        private readonly IAggregateEventReader _eventReader;
        private readonly IAggregateSnapshotReader _snapshotReader;

        public SnapshotAggregateReader(IAggregateEventReader eventReader, IAggregateSnapshotReader snapshotReader)
        {
            if (eventReader is null)
            {
                throw new ArgumentNullException(nameof(eventReader));
            }

            if (snapshotReader is null)
            {
                throw new ArgumentNullException(nameof(snapshotReader));
            }

            _eventReader = eventReader;
            _snapshotReader = snapshotReader;
        }

        public async Task<TAggregate?> ReadAsync<TAggregate, TIdentity, TEventApplier, TState>(TIdentity aggregateId,
                uint version, IAggregateFactory<TAggregate, TIdentity, TEventApplier, TState> aggregateFactory,
                IAggregateEventApplierFactory<TEventApplier>? aggregateStateFactory = default,
                CancellationToken cancellation = default)
            where TAggregate : IAggregateRoot<TIdentity, TState>
            where TIdentity : IIdentity<TIdentity>
            where TEventApplier : IAggregateEventApplier<TAggregate, TIdentity>, TState
            where TState : class
        {
            if (aggregateId is null)
            {
                throw new ArgumentNullException(nameof(aggregateId));
            }

            List<ICommittedEvent> events = new();

            var snapshot = await _snapshotReader.ReadAsync<TIdentity, TState>(aggregateId, version, cancellation);

            events.AddRange(await _eventReader.ReadAsync(
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

            if (snapshot is not null && state is ICanConsumeState<TState> stateConsumer)
            {
                stateConsumer.ConsumeState(snapshot.State);
            }

            foreach (var @event in events)
            {
                state.Apply(await @event.Event.UpgradeAsync<IAggregateEvent>(cancellation));
            }

            return aggregate;
        }
    }
}