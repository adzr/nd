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
using Nd.Aggregates.Identities;

namespace Nd.Aggregates.Persistence
{
    public abstract class AggregateRootFactory<TIdentity, TState> : IAggregateRootFactory<TIdentity, TState>
        where TIdentity : notnull, IAggregateIdentity
        where TState : notnull
    {
        protected TIdentity? Identity { get; private set; }
        protected IAggregateState<TState> State { get; }
        protected uint Version { get; private set; }

        protected AggregateRootFactory()
        {
#pragma warning disable CA2214 // Do not call overridable methods in constructors
            State = CreateState();
#pragma warning restore CA2214 // Do not call overridable methods in constructors
        }

        protected abstract IAggregateState<TState> CreateState();

        public abstract IAggregateRoot<TIdentity, TState> Build(ISession session);

        public IAggregateRootFactory<TIdentity, TState> ConfigureIdentity(TIdentity identity)
        {
            Identity = identity;
            return this;
        }

        public IAggregateRootFactory<TIdentity, TState> ConfigureState(Action<IAggregateState<TState>> configure)
        {
            configure?.Invoke(State);
            return this;
        }

        public IAggregateRootFactory<TIdentity, TState> ConfigureVersion(uint version)
        {
            Version = version;
            return this;
        }
    }
}
