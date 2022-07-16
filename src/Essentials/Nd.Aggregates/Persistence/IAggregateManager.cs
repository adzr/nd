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

using System.Threading;
using System.Threading.Tasks;
using Nd.Aggregates.Identities;
using Nd.Core.Types.Names;
using Nd.Identities;

namespace Nd.Aggregates.Persistence
{
    public interface IAggregateManager : INamedType
    {
        Task<IAggregateRoot> LoadAsync(IAggregateIdentity identity, ICorrelationIdentity correlationId, uint version = 0u, CancellationToken cancellation = default);

        Task SaveAsync(IAggregateRoot aggregate, CancellationToken cancellation = default);
    }

    public interface IAggregateManager<TAggregate, TIdentity> : IAggregateManager
        where TAggregate : notnull, IAggregateRoot<TIdentity>
        where TIdentity : notnull, IAggregateIdentity
    {
        async Task<IAggregateRoot> IAggregateManager.LoadAsync(IAggregateIdentity identity, ICorrelationIdentity correlationId, uint version, CancellationToken cancellation) =>
            await LoadAsync((TIdentity)identity, correlationId, version, cancellation).ConfigureAwait(false);

        Task IAggregateManager.SaveAsync(IAggregateRoot aggregate, CancellationToken cancellation) =>
            SaveAsync((TAggregate)aggregate, cancellation);

        Task<TAggregate> LoadAsync(TIdentity identity, ICorrelationIdentity correlationId, uint version = 0u, CancellationToken cancellation = default);

        Task SaveAsync(TAggregate aggregate, CancellationToken cancellation = default);
    }
}
