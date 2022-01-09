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

using Nd.Core.Extensions;
using Nd.ValueObjects.Common;
using Nd.ValueObjects.Identities;

namespace Nd.Aggregates.Events
{
    public sealed record class EventMetaData<TAggregate, TIdentity, TState>
        (
            IEventId EventId,
            string Name,
            uint Version,
            ISourceId SourceId,
            TIdentity AggregateId,
            uint AggregateVersion,
            DateTimeOffset Timestamp,
            long TimestampEpochInMillis
        ) : ValueObject, IEventMetaData<TAggregate, TIdentity, TState>
        where TAggregate : IAggregateRoot<TIdentity, TState>
        where TIdentity : IIdentity<TIdentity>
        where TState : AggregateState<TState, TAggregate, TIdentity>
    {
        private static readonly string EventAggregateName = typeof(TAggregate).GetName();

        public string AggregateName => EventAggregateName;

        IIdentity IEventMetaData.AggregateId => AggregateId;
    }
}
