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

using Nd.Aggregates.Extensions;
using Nd.Core.Extensions;
using Nd.Entities;

namespace Nd.Aggregates
{
    public abstract class AggregateRoot<TAggregate, TIdentity, TState> : IAggregateRoot<TIdentity, TState>
        where TState : AggregateState<TAggregate, TIdentity, TState>
        where TAggregate : AggregateRoot<TAggregate, TIdentity, TState>
        where TIdentity : IIdentity
    {
        private static readonly IAggregateName AggregateName = typeof(TAggregate).GetAggregateName();
        private static readonly string StateInitializationExceptionString =
            $"Unable to activate AggregateState of Type \"{typeof(TState).ToPrettyString()}\" for AggregateRoot of Name \"{AggregateName}\".";

        protected AggregateRoot(TIdentity identity)
        {
            if (this is not TAggregate)
            {
                throw new InvalidOperationException(
                    $"Aggregate '{GetType().ToPrettyString()}' specifies '{typeof(TAggregate).ToPrettyString()}' as generic argument, it should be its own type");
            }

            try
            {
                State = Activator.CreateInstance(typeof(TState)) as TState ?? throw new NullReferenceException(StateInitializationExceptionString);
            }
            catch (Exception exception)
            {
                throw new SystemException(StateInitializationExceptionString, exception);
            }

            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Version = 0;
        }

        public TState State { get; }

        public TIdentity Identity { get; }

        public IAggregateName Name => AggregateName;

        public ulong Version { get; private set; }

        public bool IsNew => Version == 0;

        public IIdentity GetIdentity() => Identity;
    }
}