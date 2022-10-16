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
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Nd.Core.Extensions;
using Nd.Core.Factories;
using Nd.Core.Types;
using Nd.Identities.Extensions;
using Nd.Queries.Exceptions;
using Nd.Queries.Extensions;
using Nd.Queries.Identities;

namespace Nd.Queries
{
    internal static class LoggingEventsConstants
    {
        public const int QueryReceived = 411;
        public const int ExecutingQuery = 412;
        public const int FailedCreatingCacheKey = 413;
        public const int FailedReadingCacheContent = 414;
        public const int FailedWritingCacheContent = 415;
    }

    public class QueryProcessor : IQueryProcessor
    {
        private static readonly ILookup<Type, Type> s_catalog = TypeDefinitions
            .GetAllImplementations<IQueryHandler>()
            .SelectMany(t => t.GetInterfacesOfType<IQueryHandler>().Select(i => (Interface: i, Implementation: t)))
            .Where(item =>
            {
                var queryType = item.Interface.GetGenericTypeArgumentsOfType<IQuery>().FirstOrDefault();

                return
                    queryType is not null &&
                    item.Implementation.HasMethodWithParametersOfTypes("ExecuteAsync", queryType, typeof(CancellationToken));
            })
            .Select(item => (QueryType: item.Interface.GetGenericTypeArgumentsOfType<IQuery>().First(), item.Implementation))
            .ToLookup(item => item.Implementation, item => item.QueryType);

        private static readonly Action<ILogger, Exception?> s_queryReceived =
            LoggerMessage.Define(LogLevel.Trace, new EventId(
                                LoggingEventsConstants.QueryReceived,
                                nameof(LoggingEventsConstants.QueryReceived)),
                                "Query received");

        private static readonly Action<ILogger, Exception?> s_executingQuery =
            LoggerMessage.Define(LogLevel.Trace, new EventId(
                                LoggingEventsConstants.ExecutingQuery,
                                nameof(LoggingEventsConstants.ExecutingQuery)),
                                "Executing query");

        private static readonly Action<ILogger, Exception?> s_failedReadingCacheContent =
            LoggerMessage.Define(LogLevel.Error, new EventId(
                                LoggingEventsConstants.FailedReadingCacheContent,
                                nameof(LoggingEventsConstants.FailedReadingCacheContent)),
                                "Failed reading cache content");

        private static readonly Action<ILogger, Exception?> s_failedWritingCacheContent =
            LoggerMessage.Define(LogLevel.Error, new EventId(
                                LoggingEventsConstants.FailedWritingCacheContent,
                                nameof(LoggingEventsConstants.FailedWritingCacheContent)),
                                "Failed writing cache content");

        private static readonly Guid s_queriesNamespace = Guid.ParseExact("ec434ae3-3778-43b8-83c4-866df4e1b28e", "D");

        private readonly IDistributedCache? _cache;
        private readonly DistributedCacheEntryOptions? _options;
        private readonly ILogger<QueryProcessor>? _logger;
        private readonly IDictionary<Type, IQueryHandler> _queryHandlerRegistery;
        private readonly ActivitySource _activitySource;

        public QueryProcessor(IQueryHandler[] queryHandlers, ILogger<QueryProcessor>? logger, IDistributedCache? cache = default, DistributedCacheEntryOptions? defaultOptions = default, ActivitySource? activitySource = default)
        {
            _logger = logger;
            _cache = cache;
            _options = defaultOptions;
            _activitySource = activitySource ?? new ActivitySource(GetType().Name);

            if (queryHandlers is null)
            {
                throw new ArgumentNullException(nameof(queryHandlers));
            }

            _queryHandlerRegistery = queryHandlers
                .Where(h => h is not null)
                .SelectMany(h => s_catalog[h.GetType()].Select(queryType => (QueryType: queryType, Handler: h)))
                .GroupBy(g => g.QueryType)
                .ToDictionary(g => g.Key, g =>
                {
                    try
                    {
                        return g.Single().Handler;
                    }
                    catch (Exception e)
                    {
                        throw new QueryHandlerConflictException(g.Key, g.Select(h => h.GetType()).ToImmutableArray(), e);
                    }
                });
        }

        public async Task<TResult> ProcessAsync<TQuery, TResult>(TQuery query, CancellationToken cancellation = default)
            where TQuery : notnull, IQuery<TResult>
            where TResult : notnull
        {
            // Validating query reference.
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            IQueryIdentity? queryId;

            try
            {
                queryId = CreateQueryIdentity(query);
            }
            catch (Exception ex)
            {
                throw new QueryIdCreationException(query, ex);
            }

            if (queryId is null)
            {
                throw new QueryIdCreationException(query, new QueryIdCreationException("Null query identifier"));
            }

            using var activity = _activitySource.StartActivity(nameof(ProcessAsync));

            _ = activity?
                .AddCorrelationsTag(new[] { query.CorrelationIdentity.Value })
                .AddQueryIdTag(queryId);

            // Fetching and validating query handler.
            if (!_queryHandlerRegistery.TryGetValue(query.GetType(), out var handler))
            {
                throw new QueryNotRegisteredException(query.TypeName);
            }

            TResult result;

            using var scope = _logger?
                    .BeginScope()
                    .WithCorrelationId(query.CorrelationIdentity)
                    .WithQueryId(queryId)
                    .Build();

            if (_logger is not null)
            {
                s_queryReceived(_logger, default);
            }

            if (_cache is not null)
            {
                var foundInCache = await _cache.GetAsync(queryId.ToString(), cancellation).ConfigureAwait(false);

                if (foundInCache is not null)
                {
                    try
                    {
                        return JsonSerializer.Deserialize<TResult>(Encoding.UTF8.GetString(foundInCache))!;
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                        if (_logger is not null)
                        {
                            s_failedReadingCacheContent(_logger, e);
                        }

                        await _cache.RemoveAsync(queryId.ToString(), cancellation).ConfigureAwait(false);
                    }
                }
            }

            if (_logger is not null)
            {
                s_executingQuery(_logger, default);
            }

            try
            {
                // Execute the query and keep the result.
                result = await ((IQueryHandler<TQuery, TResult>)handler).ExecuteAsync(query, cancellation)
                    .ConfigureAwait(false);

                if (result is null)
                {
                    throw new QueryExecutionException($"Query {queryId} handler {handler.GetType().FullName} cannot return a null result");
                }
            }
            catch (Exception ex)
            {
                throw new QueryExecutionException(query, queryId, ex);
            }

            if (_cache is not null)
            {
                try
                {
                    if (_options is null)
                    {
                        await _cache.SetAsync(queryId.ToString(), JsonSerializer.SerializeToUtf8Bytes(result), cancellation).ConfigureAwait(false);
                    }
                    else
                    {
                        await _cache.SetAsync(queryId.ToString(), JsonSerializer.SerializeToUtf8Bytes(result), _options, cancellation).ConfigureAwait(false);
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    if (_logger is not null)
                    {
                        s_failedWritingCacheContent(_logger, e);
                    }
                }
            }

            // Finally return the result.
            return result;
        }

        private static IQueryIdentity CreateQueryIdentity(IQuery query)
            => new QueryIdentity(DeterministicGuidFactory.Instance(s_queriesNamespace, JsonSerializer.SerializeToUtf8Bytes(query)));
    }
}
