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

using System.Collections.Generic;
using System.Threading;
using Nd.Aggregates.Identities;
using Nd.Identities;

namespace Nd.Aggregates.Persistence
{
    public interface IAggregateEventReader

    {
        /// <summary>
        /// Reads all of the events of the aggregate which its identity is specified.
        /// This method is expected to return events in the same order they occurred, sort in ascending order by Metadata.AggregateVersion.
        /// </summary>
        /// <typeparam name="TIdentity">The aggregate identity type.</typeparam>
        /// <param name="aggregateId">The aggregate identity, must never be null.</param>
        /// <param name="correlationId">The correlation identity, must never be null.</param>
        /// <param name="cancellation">A cancellation token.</param>
        /// <returns><see cref="IAsyncEnumerable"/> of <see cref="ICommittedEvent"/> containing the event and its meta data.</returns>
        IAsyncEnumerable<ICommittedEvent<TIdentity>> ReadAsync<TIdentity>(
            TIdentity aggregateId,
            ICorrelationIdentity correlationId,
            CancellationToken cancellation = default)
            where TIdentity : notnull, IAggregateIdentity =>
            ReadAsync(aggregateId, correlationId, 0u, cancellation);

        /// <summary>
        /// Reads all of the events of the aggregate which its identity is specified up to the specified aggregate version.
        /// This method is expected to return events in the same order they occurred, sort in ascending order by Metadata.AggregateVersion.
        /// </summary>
        /// <typeparam name="TIdentity">The aggregate identity type.</typeparam>
        /// <param name="aggregateId">The aggregate identity, must never be null.</param>
        /// <param name="correlationId">The correlation identity, must never be null.</param>
        /// <param name="version">The aggregate version, should be ignored if zero.</param>
        /// <param name="cancellation">A cancellation token.</param>
        /// <returns><see cref="IAsyncEnumerable"/> of <see cref="ICommittedEvent"/> containing the event and its meta data.</returns>
        IAsyncEnumerable<ICommittedEvent<TIdentity>> ReadAsync<TIdentity>(
            TIdentity aggregateId,
            ICorrelationIdentity correlationId,
            uint version,
            CancellationToken cancellation = default)
            where TIdentity : notnull, IAggregateIdentity =>
            ReadAsync(aggregateId, correlationId, 0u, 0u, cancellation);

        /// <summary>
        /// Reads all of the events of the aggregate which its identity is specified within the specified aggregate version range inclusive.
        /// This method is expected to return events in the same order they occurred, sort in ascending order by Metadata.AggregateVersion.
        /// </summary>
        /// <typeparam name="TIdentity">The aggregate identity type.</typeparam>
        /// <param name="aggregateId">The aggregate identity, must never be null.</param>
        /// <param name="correlationId">The correlation identity, must never be null.</param>
        /// <param name="versionStart">The aggregate version lower range bound, should be ignored if zero.</param>
        /// <param name="versionEnd">The aggregate version upper range bound, should be ignored if zero.</param>
        /// <param name="cancellation">A cancellation token.</param>
        /// <returns><see cref="IAsyncEnumerable"/> of <see cref="ICommittedEvent"/> containing the event and its meta data.</returns>
        IAsyncEnumerable<ICommittedEvent<TIdentity>> ReadAsync<TIdentity>(
            TIdentity aggregateId,
            ICorrelationIdentity correlationId,
            uint versionStart,
            uint versionEnd,
            CancellationToken cancellation = default)
            where TIdentity : notnull, IAggregateIdentity;
    }
}
