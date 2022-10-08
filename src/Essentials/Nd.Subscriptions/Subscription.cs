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
using Nd.Aggregates.Identities;
using Nd.Aggregates.Persistence;
using Nd.Core.Extensions;
using Nd.Core.Threading;
using Nd.Core.Types;
using Nd.Subscriptions.Exceptions;
using Nd.Subscriptions.Persistence;

namespace Nd.Subscriptions
{
    public abstract class SubscriptionBase : ISubscription
    {
        protected static readonly IDictionary<Type, Func<ISubscriptionHandler, IDomainEvent, CancellationToken, Task>> HandlersForAll =
            TypeDefinitions
            .GetAllImplementations<ISubscriptionHandler>()
            .Where(t => t.HasMethodWithParametersOfTypes(nameof(ISubscriptionHandler.HandleAsync), typeof(IDomainEvent), typeof(CancellationToken)))
            .Select(t => (Type: t, Method: t.GetMethodWithParametersOfTypes(nameof(ISubscriptionHandler.HandleAsync), typeof(IDomainEvent), typeof(CancellationToken))
                        .CompileMethodInvocation<Func<ISubscriptionHandler, IDomainEvent, CancellationToken, Task>>()))
            .ToDictionary(t => t.Type, t => t.Method);

        protected static readonly IDictionary<Type, IDictionary<Type, Func<ISubscriptionHandler, IDomainEvent, CancellationToken, Task>>> HandlersForOne =
            TypeDefinitions
            .GetAllImplementations<ISubscriptionHandler>()
            .SelectMany(t => t.GetInterfacesOfType<ISubscriptionHandler>().Select(i => (Interface: i, Implementation: t)))
            .Where(item =>
            {
                var type = item.Interface.GetGenericTypeArgumentsOfType<IDomainEvent>().FirstOrDefault();
                return type is not null && item.Implementation.HasMethodWithParametersOfTypes(nameof(ISubscriptionHandler.HandleAsync), type, typeof(CancellationToken));
            })
            .Select(item =>
            {
                var type = item.Interface.GetGenericTypeArgumentsOfType<IDomainEvent>().First();

                return (
                    EventType: type,
                    item.Implementation,
                    Method: item.Implementation.GetMethodWithParametersOfTypes(nameof(ISubscriptionHandler.HandleAsync), type, typeof(CancellationToken))
                    .CompileMethodInvocation<Func<ISubscriptionHandler, IDomainEvent, CancellationToken, Task>>()
                );
            })
            .GroupBy(item => item.EventType)
            .ToDictionary(g => g.Key, g => (IDictionary<Type, Func<ISubscriptionHandler, IDomainEvent, CancellationToken, Task>>)g.ToDictionary(i => i.Implementation, i => i.Method));

        private readonly IEventWatcher _watcher;
        private readonly Func<CancellationToken, Task> _unsubscribeCallback;
        private readonly Func<ICommittedEvent, Exception, CancellationToken, Task> _failureCallback;
        private readonly ISubscriptionState _state;
        private readonly IAsyncLocker _lock = ExclusiveAsyncLocker.Create();
        private CancellationTokenSource _cancellation = new();

        protected EventWatcherOptions? EventWatcherOptions { get; set; }

        protected SubscriptionBase(
            IEventWatcher watcher,
            ISubscriptionState subscriptionState,
            Func<CancellationToken, Task>? unsubscribeCallback = default,
            Func<ICommittedEvent, Exception, CancellationToken, Task>? failureCallback = default)
        {
            _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
            _unsubscribeCallback = unsubscribeCallback ?? (_ => Task.CompletedTask);
            _failureCallback = failureCallback ?? ((_, e, _) => throw e);
            _state = subscriptionState ?? throw new ArgumentNullException(nameof(subscriptionState));
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
                if (events is null)
                {
                    throw new ArgumentNullException(nameof(events));
                }

                foreach (var @event in events)
                {
                    if (c.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        await HandleAsync(@event, c).ConfigureAwait(false);
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (Exception handlerException)
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                        try
                        {
                            await _failureCallback(@event, handlerException, c).ConfigureAwait(false);
                        }
                        catch (Exception failureCallbackException)
                        {
                            throw new SubscriptionWatchException(_state, @event, handlerException, failureCallbackException);
                        }
                    }
                }
            }, _state, EventWatcherOptions, cancellation).ConfigureAwait(false);
        }

        protected abstract Task HandleAsync(ICommittedEvent committedEvent, CancellationToken cancellation = default);

        public async Task StopProcessingAsync(CancellationToken cancellation = default)
        {
            _cancellation.Cancel();
            using var @lock = await _lock.WaitAsync(cancellation).ConfigureAwait(false);
        }

        public async Task UnsubscribeAsync(CancellationToken cancellation = default)
        {
            _cancellation.Cancel();
            using var @lock = await _lock.WaitAsync(cancellation).ConfigureAwait(false);
            await _unsubscribeCallback(cancellation).ConfigureAwait(false);
        }
    }

    public class Subscription : SubscriptionBase
    {
        private readonly (ISubscriptionHandler Handler, Func<ISubscriptionHandler, IDomainEvent, CancellationToken, Task> Method)[] _calls;

        public Subscription(
            ISubscriptionHandler handler,
            IEventWatcher watcher,
            ISubscriptionState subscriptionState,
            Func<CancellationToken, Task>? unsubscribe = default,
            Func<ICommittedEvent, Exception, CancellationToken, Task>? handleFailure = default) :
            this(new[] { handler }, watcher, subscriptionState, unsubscribe, handleFailure)
        { }

        public Subscription(
            IEnumerable<ISubscriptionHandler> handlers,
            IEventWatcher watcher,
            ISubscriptionState subscriptionState,
            Func<CancellationToken, Task>? unsubscribe = default,
            Func<ICommittedEvent, Exception, CancellationToken, Task>? handleFailure = default) :
            base(watcher, subscriptionState, unsubscribe, handleFailure)
        {
            if (handlers is null)
            {
                throw new ArgumentNullException(nameof(handlers));
            }

            _calls = handlers
                .Select(handler => (
                    Handler: handler,
                    Method: HandlersForAll
                        .TryGetValue(handler.GetType(), out var method) ?
                            method :
                            throw new InvalidHandlerException(handler)))
                .ToArray();

            if (!_calls.Any())
            {
                throw new NoOpSubscriptionException(subscriptionState.Identity);
            }
        }

        protected override Task HandleAsync(ICommittedEvent committedEvent, CancellationToken cancellation = default)
        {
            if (committedEvent is null)
            {
                throw new ArgumentNullException(nameof(committedEvent));
            }

            var results = new List<Task>();

            foreach (var (handler, method) in _calls)
            {
                results.Add(method(handler, new DomainEvent(committedEvent.AggregateEvent, committedEvent.Metadata), cancellation));
            }

            return Task.WhenAll(results);
        }
    }

    public class Subscription<TEvent, TIdentity> : SubscriptionBase
        where TEvent : notnull, IAggregateEvent
        where TIdentity : notnull, IAggregateIdentity
    {
        private readonly IList<(ISubscriptionHandler<TEvent, TIdentity> Handler, Func<ISubscriptionHandler<TEvent, TIdentity>, IDomainEvent<TEvent, TIdentity>, CancellationToken, Task> Method)> _calls;

        public Subscription(
            ISubscriptionHandler<TEvent, TIdentity> handler,
            IEventWatcher watcher,
            ISubscriptionState subscriptionState,
            Func<CancellationToken, Task>? unsubscribe = default,
            Func<ICommittedEvent, Exception, CancellationToken, Task>? handleFailure = default) :
            this(new[] { handler }, watcher, subscriptionState, unsubscribe, handleFailure)
        { }

        public Subscription(
            IEnumerable<ISubscriptionHandler<TEvent, TIdentity>> handlers,
            IEventWatcher watcher,
            ISubscriptionState subscriptionState,
            Func<CancellationToken, Task>? unsubscribe = default,
            Func<ICommittedEvent, Exception, CancellationToken, Task>? handleFailure = default) :
            base(watcher, subscriptionState, unsubscribe, handleFailure)
        {
            if (handlers is null)
            {
                throw new ArgumentNullException(nameof(handlers));
            }

            _calls = handlers
                .Select(handler => (
                    Handler: handler,
                    Method: HandlersForOne.TryGetValue(typeof(TEvent), out var dict) ?
                            dict.TryGetValue(handler.GetType(), out var method) ?
                            (Func<ISubscriptionHandler<TEvent, TIdentity>, IDomainEvent<TEvent, TIdentity>, CancellationToken, Task>)method :
                            throw new InvalidHandlerException(handler) :
                            throw new InvalidHandlerException(handler)))
                .ToArray();

            if (!_calls.Any())
            {
                throw new NoOpSubscriptionException(subscriptionState.Identity);
            }
        }

        protected override Task HandleAsync(ICommittedEvent committedEvent, CancellationToken cancellation = default)
        {
            if (committedEvent is null)
            {
                throw new ArgumentNullException(nameof(committedEvent));
            }

            var results = new List<Task>();

            foreach (var (handler, method) in _calls)
            {
                results.Add(method(
                    handler,
                    new DomainEvent<TEvent, TIdentity>(
                        (TEvent)committedEvent.AggregateEvent,
                        (IAggregateEventMetadata<TIdentity>)committedEvent.Metadata),
                    cancellation));
            }

            return Task.WhenAll(results);
        }
    }
}
