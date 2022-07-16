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
using Microsoft.Extensions.Logging;
using Nd.Aggregates.Common;
using Nd.Aggregates.Events;
using Nd.Aggregates.Exceptions;
using Nd.Aggregates.Identities;
using Nd.Core.Extensions;
using Nd.Identities;

namespace Nd.Aggregates.Extensions
{
    public static class AggregateIdentityExtensions
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

    public static class LoggerExtensions
    {
        internal sealed class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {

            }
        }

        public static IDisposable With(this ILogger? logger, string key, object value) =>
            logger is null
                ? new EmptyDisposable()
                : logger.BeginScope(new Dictionary<string, object>
                {
                    [key] = value
                });

        public static IDisposable WithCorrelationId<TIdentity>(this ILogger? logger, TIdentity value)
            where TIdentity : notnull, ICorrelationIdentity =>
            WithCorrelationId(logger, value.Value);

        public static IDisposable WithCorrelationId(this ILogger? logger, Guid value) =>
            With(logger, LoggingScopeConstants.CorrelationKey, value);

        public static IDisposable WithAggregateId<TIdentity>(this ILogger? logger, TIdentity value)
            where TIdentity : notnull, IAggregateIdentity =>
            With(logger, LoggingScopeConstants.DomainAggregateKey, value);
    }
}
