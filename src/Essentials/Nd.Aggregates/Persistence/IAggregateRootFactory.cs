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
    public interface IAggregateRootFactory
    {
        IAggregateRootFactory ConfigureIdentity(IAggregateIdentity identity);

        IAggregateRootFactory ConfigureState(Action<IAggregateState> configure);

        IAggregateRootFactory ConfigureVersion(uint version);

        IAggregateRoot Build(ISession session);
    }

    public interface IAggregateRootFactory<TIdentity> : IAggregateRootFactory
        where TIdentity : notnull, IAggregateIdentity
    {
        IAggregateRootFactory IAggregateRootFactory.ConfigureIdentity(IAggregateIdentity identity) => ConfigureIdentity((TIdentity)identity);

        IAggregateRootFactory<TIdentity> ConfigureIdentity(TIdentity identity);

        IAggregateRootFactory IAggregateRootFactory.ConfigureState(Action<IAggregateState> configure) => ConfigureState(configure);

        new IAggregateRootFactory<TIdentity> ConfigureState(Action<IAggregateState> configure);

        IAggregateRootFactory IAggregateRootFactory.ConfigureVersion(uint version) => ConfigureVersion(version);

        new IAggregateRootFactory<TIdentity> ConfigureVersion(uint version);

        IAggregateRoot IAggregateRootFactory.Build(ISession session) => Build(session);

        new IAggregateRoot<TIdentity> Build(ISession session);
    }

    public interface IAggregateRootFactory<TIdentity, TState> : IAggregateRootFactory<TIdentity>
        where TIdentity : notnull, IAggregateIdentity
        where TState : notnull
    {
        IAggregateRootFactory<TIdentity> IAggregateRootFactory<TIdentity>.ConfigureIdentity(TIdentity identity) => ConfigureIdentity(identity);

        new IAggregateRootFactory<TIdentity, TState> ConfigureIdentity(TIdentity identity);

        IAggregateRootFactory<TIdentity> IAggregateRootFactory<TIdentity>.ConfigureState(Action<IAggregateState> configure) => ConfigureState(configure);

        IAggregateRootFactory<TIdentity, TState> ConfigureState(Action<IAggregateState<TState>> configure);

        IAggregateRootFactory<TIdentity> IAggregateRootFactory<TIdentity>.ConfigureVersion(uint version) => ConfigureVersion(version);

        new IAggregateRootFactory<TIdentity, TState> ConfigureVersion(uint version);

        IAggregateRoot<TIdentity> IAggregateRootFactory<TIdentity>.Build(ISession session) => Build(session);

        new IAggregateRoot<TIdentity, TState> Build(ISession session);
    }
}
