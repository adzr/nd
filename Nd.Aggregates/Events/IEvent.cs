﻿/*
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

using Nd.ValueObjects.Identities;

namespace Nd.Aggregates.Events
{
    public interface IEvent
    {
        public static readonly Guid NamespaceIdentifier = Guid.ParseExact("8a563c72-5604-4ca2-9d5f-bb6eb7f960c7", "D");

        IEventMetaData EventMetaData { get; }
    }

    public interface IEvent<TAggregate, TIdentity, TState> : IEvent
        where TAggregate : IAggregateRoot<TIdentity, TState>
        where TIdentity : IIdentity<TIdentity>
        where TState : AggregateState<TState, TAggregate, TIdentity>
    {
        new IEventMetaData<TAggregate, TIdentity, TState> EventMetaData { get; }
    }
}