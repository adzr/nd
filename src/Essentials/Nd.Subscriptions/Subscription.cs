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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nd.Aggregates.Events;
using Nd.Core.Extensions;
using Nd.Core.Threading;
using Nd.Core.Types;
using Nd.Subscriptions.Persistence;

namespace Nd.Subscriptions
{
    public class Subscription : ISubscription
    {
        private static readonly IDictionary<Type, Type> s_eventTypedHandlers =
            TypeDefinitions
            .GetAllImplementations<ISubscriptionHandler>()
            .Where(t => t.GetGenericTypeArgumentsOfType<IAggregateEvent>().FirstOrDefault() is not null &&
                        t.HasMethodWithParametersOfTypes(nameof(ISubscriptionHandler.HandleAsync), typeof(IDomainEvent), typeof(CancellationToken)))
            .ToDictionary(t => t, t => t.GetGenericTypeArgumentsOfType<IAggregateEvent>().FirstOrDefault()!);

        private readonly ISubscriptionHandler _handler;
        private readonly IEventWatcher _watcher;
        private readonly Func<CancellationToken, Task> _unsubscribe;
        private readonly ISubscriptionState _state;
        private readonly Type _aggregateEventType;
        private readonly IAsyncLocker _lock = ExclusiveAsyncLocker.Create();
        private CancellationTokenSource _cancellation = new();

        public Subscription(ISubscriptionHandler handler, IEventWatcher watcher, ISubscriptionState subscriptionState, Func<CancellationToken, Task> unsubscribe)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
            _unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
            _state = subscriptionState ?? throw new ArgumentNullException(nameof(subscriptionState));
            _aggregateEventType = s_eventTypedHandlers[_handler.GetType()];
        }

        public async Task StartProcessingAsync(CancellationToken cancellation = default)
        {
            // Cannot be started multiple times, only one instance should be running.
            using var @lock = await _lock.WaitAsync(cancellation).ConfigureAwait(false);

            // Create an internal cancellation token.
            _cancellation = new();
            // Request cancellation on the internal token when cancellation is requested on the external one.
            var cancellationTokenRegistration = cancellation.Register(() => _cancellation.Cancel());

            // Start watching for incoming events.
            await _watcher.Watch(async (events, c) =>
            {
                foreach (var @event in events)
                {
                    if (c.IsCancellationRequested)
                    {
                        break;
                    }

                    if (_aggregateEventType.Equals(@event.AggregateEvent.GetType()))
                    {
                        await _handler.HandleAsync(new DomainEvent(@event.AggregateEvent, @event.Metadata), c).ConfigureAwait(false);
                    }
                }
            }, _state, cancellation).ConfigureAwait(false);
        }

        public async Task StopProcessingAsync(CancellationToken cancellation = default)
        {
            _cancellation.Cancel();
            using var @lock = await _lock.WaitAsync(cancellation).ConfigureAwait(false);
        }

        public async Task UnsubscribeAsync(CancellationToken cancellation = default)
        {
            _cancellation.Cancel();
            using var @lock = await _lock.WaitAsync(cancellation).ConfigureAwait(false);
            await _unsubscribe(cancellation).ConfigureAwait(false);
        }
    }

    internal class DomainEvent : IDomainEvent
    {
        public DomainEvent(IAggregateEvent aggregateEvent, IAggregateEventMetadata metadata)
        {
            AggregateEvent = aggregateEvent;
            Metadata = metadata;
        }

        public IAggregateEvent AggregateEvent { get; }

        public IAggregateEventMetadata Metadata { get; }
    }
}
