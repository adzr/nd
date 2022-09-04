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

using System;
using System.Linq;
using Nd.Core.Exceptions;
using Nd.Core.Extensions;
using Nd.Core.Types;
using Nd.ValueObjects;

namespace Nd.Aggregates.Events
{
    public abstract record class AggregateState<TState> : ValueObject, IAggregateState<TState>
        where TState : notnull
    {
        /// <summary>
        /// Contains a cached lookup of aggregate events and a lambda method to help
        /// applying the received events based on types to the correct event handler interface.
        /// </summary>
        private static readonly ILookup<Type, ILookup<Type, Action<IAggregateState, IAggregateEvent>>> s_eventApplicationMethods =
            TypeDefinitions
            .GetAllImplementations<IAggregateState>()
            .Select(t => (Type: t,
                // Get all the interfaces of type ICanHandleAggregateEvent that IAggregateState implements.
                Lookup: t.GetInterfacesOfType<ICanHandleAggregateEvent>()
                // Filter only on interfaces that has a first generic argument of a type that implements IAggregateEvent,
                // and have a method with the name "On" that takes a single parameter of a type assignable to
                // the found generic type.
                .Where(t =>
                {
                    var eventType = t.GetGenericTypeArgumentsOfType<IAggregateEvent>().FirstOrDefault();
                    return eventType is not null && t.HasMethodWithParametersOfTypes(nameof(ICanHandleAggregateEvent.Handle), eventType);
                })
                // Convert the findings into a record that contains the aggregate event type
                // and a compiled lambda of its matching "On" method that receives an aggregate
                // event of the same type.
                .Select(t =>
                {
                    var type = t.GetGenericTypeArgumentsOfType<IAggregateEvent>().First();

                    return (
                        Type: type,
                        Method: t.GetMethodWithParametersOfTypes(nameof(ICanHandleAggregateEvent.Handle), type)
                        .CompileMethodInvocation<Action<IAggregateState, IAggregateEvent>>()
                    );
                })
                // Return a lookup of the [IAggregateEvent] => Method.
                .ToLookup(r => r.Type, r => r.Method)))
            // Return a lookup of the [IAggregateState] => ILookup.
            .ToLookup(r => r.Type, r => r.Lookup);

        private readonly ILookup<Type, Action<IAggregateState, IAggregateEvent>> _eventApplicationMethods;

        public abstract TState State { get; }

        protected AggregateState()
        {
            // On construction, get the correct lookup for this IAggregateState.
            _eventApplicationMethods = s_eventApplicationMethods[GetType()]?.FirstOrDefault() ??
                throw new TypeDefinitionNotFoundException($"Definition of type has no {nameof(IAggregateEvent)} lookup defined: {GetType().ToPrettyString()}");
        }

        void IAggregateState.Apply(IAggregateEvent @event)
        {
            var actions = _eventApplicationMethods[@event.GetType()];

            if (actions is not null && actions.Any())
            {
                actions.Single()(this, @event);
            }
        }

        public void Apply(IAggregateEvent<TState> aggregateEvent) => ((IAggregateState)this).Apply(aggregateEvent);
    }
}
