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

using Nd.Core.Extensions;
using Nd.Core.Types;
using Nd.ValueObjects;

namespace Nd.Aggregates.Events {

    public abstract record class AggregateEventApplier<TState> : ValueObject, IAggregateEventApplier<TState>
        where TState : class {
        /// <summary>
        /// Contains a cached lookup of aggregate events and a lambda method to help
        /// applying the received events based on types to the correct event handler interface.
        /// </summary>
        private static readonly ILookup<Type, ILookup<Type, Action<IAggregateEventApplier, IAggregateEvent>>> s_eventApplicationMethods =
            Definitions
            .GetAllImplementations<IAggregateEventApplier>()
            .Select(t => (Type: t,
                // Get all the interfaces of type IAggregateEventHandler that TEventApplier implements.
                Lookup: t.GetInterfacesOfType<IAggregateEventHandler>()
                // Filter only on interfaces that has a first generic argument of a type that implements IAggregateEvent,
                // and have a method with the name "On" that takes a single parameter of a type assignable to
                // the found generic type.
                .Where(t => {
                    var eventType = t.GetGenericTypeArgumentsOfType<IAggregateEvent>().FirstOrDefault();

                    if (eventType is null) {
                        return false;
                    }

                    return t.HasMethodWithParametersOfTypes(nameof(IAggregateEventHandler.On), eventType);
                })
                // Convert the findings into a record that contains the aggregate event type
                // and a compiled lambda of its matching "On" method that receives an aggregate
                // event of the same type.
                .Select(t => {
                    var type = t.GetGenericTypeArgumentsOfType<IAggregateEvent>().First();

                    return (
                        Type: type,
                        Method: t.GetMethodWithParametersOfTypes(nameof(IAggregateEventHandler.On), type)
                        .CompileMethodInvocation<Action<IAggregateEventApplier, IAggregateEvent>>()
                    );
                })
                // Return a lookup of the [IAggregateEvent] => Method.
                .ToLookup(r => r.Type, r => r.Method)))
            // Return a lookup of the [IAggregateEventApplier] => ILookup.
            .ToLookup(r => r.Type, r => r.Lookup);

        private readonly ILookup<Type, Action<IAggregateEventApplier, IAggregateEvent>> _eventApplicationMethods;

        public abstract TState State { get; }

        protected AggregateEventApplier() =>
            // On construction, get the correct lookup for this IAggregateEventApplier.
            _eventApplicationMethods = s_eventApplicationMethods[GetType()]?.FirstOrDefault() ??
            throw new TypeDefinitionNotFoundException($"Definition of type has no {nameof(IAggregateEvent)} lookup defined: {GetType().ToPrettyString()}");

        void IAggregateEventApplier.Apply(IAggregateEvent @event) {
            var actions = _eventApplicationMethods[@event.GetType()];

            if (actions is not null && actions.Any()) {
                actions.Single()(this, @event);
            }
        }

        public void Apply(IAggregateEvent<TState> @event) => ((IAggregateEventApplier)this).Apply(@event);
    }
}
