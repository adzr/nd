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
using Nd.Aggregates.Events;
using Nd.Aggregates.Exceptions;
using Nd.Aggregates.Identities;
using Nd.Core.Extensions;

namespace Nd.Aggregates.Extensions
{
    public static class AggregateExtensions
    {
        public static (TAggregate Aggregate, IAggregateState<TState> State) CreateAggregateAndState<TIdentity, TState, TAggregate>(
            this TIdentity aggregateId,
            AggregateFactoryFunc<TIdentity, TState, TAggregate> initializeAggregate,
            AggregateStateFactoryFunc<TState> initializeState,
            uint version)
            where TAggregate : notnull, IAggregateRoot<TIdentity, TState>
            where TIdentity : notnull, IAggregateIdentity
            where TState : notnull
        {
            if (initializeState is null)
            {
                throw new ArgumentNullException(nameof(initializeState));
            }

            if (initializeAggregate is null)
            {
                throw new ArgumentNullException(nameof(initializeAggregate));
            }

            IAggregateState<TState> state;

            try
            {
                state = initializeState() ?? throw new AggregateStateCreationException(typeof(TState));
            }
            catch (Exception exception)
            {
                throw new AggregateStateCreationException(typeof(TState), exception);
            }

            TAggregate aggregate;

            try
            {
                aggregate = initializeAggregate(aggregateId, () => state, version) ??
                    throw new AggregateCreationException(typeof(TAggregate).GetName());
            }
            catch (Exception exception)
            {
                throw new AggregateCreationException(typeof(TAggregate).GetName(), exception);
            }

            return (aggregate, state);
        }
    }
}
