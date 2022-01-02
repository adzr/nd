/*
 * Copyright © 2015 - 2021 Rasmus Mikkelsen
 * Copyright © 2015 - 2021 eBay Software Foundation
 * Modified from original source https://github.com/eventflow/EventFlow
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
using Nd.Core.Extensions;
using Nd.Entities;
using System.Reflection;

namespace Nd.Aggregates
{
    public abstract class AggregateState<TAggregate, TIdentity, TEventApplier> : IEventApplier<TAggregate, TIdentity>
        where TEventApplier : class, IEventApplier<TAggregate, TIdentity>
        where TAggregate : IAggregateRoot<TIdentity, TEventApplier>
        where TIdentity : IIdentity
    {
        private static readonly IReadOnlyDictionary<Type, MethodInfo> StateMutationMethods;

        static AggregateState() => StateMutationMethods = typeof(TEventApplier)
                .GetInterfacesOfType<IApplyEvent>()
                .Where(t => t.GetGenericTypeArgumentsOfType<IAggregateEvent>().Any())
                .Select(t =>
                {
                    var type = t.GetGenericTypeArgumentsOfType<IAggregateEvent>().First();
                    return (Type: type, Method: t.GetMethodWithSingleParameterOfType(nameof(IApplyEvent.On), type));
                })
                .ToDictionary(r => r.Type, r => r.Method);

        protected AggregateState() => _ = this as TEventApplier ?? throw new InvalidOperationException(
                $"Event applier of type '{GetType().ToPrettyString()}' has a wrong generic argument '{typeof(TEventApplier).ToPrettyString()}'");

        public void Apply(IAggregateEvent @event) => On(@event);

        public void On(IAggregateEvent @event)
        {
            if (!StateMutationMethods.ContainsKey(@event.GetType()))
            {
                return;
            }

            StateMutationMethods[@event.GetType()].Invoke(this, new object[] { @event });
        }
    }
}