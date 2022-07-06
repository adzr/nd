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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Nd.Aggregates.Identities;
using Nd.Core.Extensions;

namespace Nd.Aggregates.Persistence
{
    public abstract class CachingAggregateManager<TAggregate, TIdentity> : IAggregateManager<TAggregate, TIdentity>
        where TAggregate : notnull, IAggregateRoot<TIdentity>
        where TIdentity : notnull, IAggregateIdentity
    {
        private readonly IAggregateReader<TIdentity> _reader;
        private readonly IAggregateEventWriter<TIdentity> _writer;
        private readonly IDistributedCache? _cache;
        private readonly string _cacheKeyPrefix;
        private readonly Action<DistributedCacheEntryOptions>? _configureCacheOptions;

        protected CachingAggregateManager(IAggregateReader<TIdentity> reader, IAggregateEventWriter<TIdentity> writer, IDistributedCache? cache = default, string cacheKeyPrefix = "", Action<DistributedCacheEntryOptions>? configureCacheOptions = default)
        {
            TypeName = GetType().GetName();
            _reader = reader;
            _writer = writer;
            _cache = cache;
            _cacheKeyPrefix = !string.IsNullOrWhiteSpace(cacheKeyPrefix) ? cacheKeyPrefix : $"{TypeName}-";
            _configureCacheOptions = configureCacheOptions;
        }

        public string TypeName { get; }

        public async Task<TAggregate> LoadAsync(TIdentity identity, uint version = 0u, CancellationToken cancellation = default)
        {
            if (identity is null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            var cacheKey = $"{_cacheKeyPrefix}{identity.TypeName}-{identity}";

            TAggregate? cachedAggregate = default;

            if (_cache is not null)
            {
                var foundInCache = await _cache.GetAsync(cacheKey, cancellation).ConfigureAwait(false);

                if (foundInCache is not null)
                {
                    cachedAggregate = await DeserializeAsync(foundInCache, cancellation).ConfigureAwait(false);

                    await _cache.RefreshAsync(cacheKey, cancellation).ConfigureAwait(false);

                    return cachedAggregate;
                }
            }

            var storedAggregate = await _reader.ReadAsync<TAggregate>(identity, version, cancellation).ConfigureAwait(false);

            if (_cache is not null)
            {
                var serialized = await SerializeAsync(storedAggregate, cancellation).ConfigureAwait(false);

                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60),
                    SlidingExpiration = TimeSpan.FromSeconds(30)
                };

                _configureCacheOptions?.Invoke(options);

                await _cache.SetAsync(cacheKey, serialized, options, cancellation).ConfigureAwait(false);
            }

            return storedAggregate;
        }

        public async Task SaveAsync(TAggregate aggregate, CancellationToken cancellation = default)
        {
            if (aggregate is null)
            {
                throw new ArgumentNullException(nameof(aggregate));
            }

            await aggregate.CommitAsync(_writer, cancellation).ConfigureAwait(false);

            if (_cache is not null)
            {
                var cacheKey = $"{_cacheKeyPrefix}{aggregate.Identity.TypeName}-{aggregate.Identity}";

                await _cache.RemoveAsync(cacheKey, cancellation).ConfigureAwait(false);
            }
        }

        protected virtual Task<TAggregate> DeserializeAsync(byte[] payload, CancellationToken cancellation = default) =>
            throw new NotSupportedException($"Cache deserialization is not supported for this aggregate manager, please override {nameof(DeserializeAsync)}");

        protected virtual Task<byte[]> SerializeAsync(TAggregate aggregate, CancellationToken cancellation = default) =>
            throw new NotSupportedException($"Cache serialization is not supported for this aggregate manager, please override {nameof(SerializeAsync)}");
    }
}
