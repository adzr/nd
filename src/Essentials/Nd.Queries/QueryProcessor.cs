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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Nd.Core.Extensions;
using Nd.Core.Factories;
using Nd.Identities.Extensions;
using Nd.Queries.Exceptions;
using Nd.Queries.Extensions;

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

        private static readonly Action<ILogger, Exception?> s_failedCreatingCacheKey =
            LoggerMessage.Define(LogLevel.Error, new EventId(
                                LoggingEventsConstants.FailedCreatingCacheKey,
                                nameof(LoggingEventsConstants.FailedCreatingCacheKey)),
                                "Failed creating cache key");

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
        private readonly ILookup<Type, IQueryHandler> _queryHandlerRegistery;

        public QueryProcessor(IQueryHandler[] queryHandlers, ILogger<QueryProcessor>? logger, IDistributedCache? cache = default, DistributedCacheEntryOptions? defaultOptions = default)
        {
            _logger = logger;
            _cache = cache;
            _options = defaultOptions;

            _queryHandlerRegistery = queryHandlers
                .Where(handler => handler is not null)
                // Mapping all key types to there query handlers instances.
                .SelectMany(handler => handler!
                        // So foreach handler type.
                        .GetType()
                        // We get all of the interfaces that it implements that are of type IQueryHandler.
                        .GetInterfacesOfType<IQueryHandler>()
                        // If any of these IQueryHandler interfaces has a generic type of IQuery then get it along with the handler instance.
                        .Select(i => (Handler: handler, QueryType: i.GetGenericTypeArgumentsOfType<IQuery>().FirstOrDefault()))
                )
                // After flattening our selection, now filter on those interfaces that actually have a generic type of IQuery.
                .Where(r => r.QueryType is not null)
                // Then group by query type which cannot be null at this point.
                .GroupBy(r => r.QueryType!)
                // Validate that there are no multiple handlers for any single query.
                .Select(g => g.Count() == 1 ? g.Single() : throw new QueryHandlerConflictException(g.Key.GetName(), g.Select(r => r.Handler!.GetType().ToPrettyString()).ToArray()))
                // Finally convert it to a lookup of a query type as a key and a handler instance as a one record value.
                .ToLookup(r => r.QueryType!, r => r.Handler);
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Exception is being logged in all cases.")]
        public async Task<TResult> ProcessAsync<TQuery, TResult>(TQuery query, CancellationToken cancellation = default)
            where TQuery : notnull, IQuery<TResult>
            where TResult : notnull
        {
            // Validating query reference.
            if (query is null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            // Fetching and validating query handler.
            var handlers = _queryHandlerRegistery[query.GetType()];

            if (handlers is null || !handlers.Any())
            {
                throw new QueryNotRegisteredException(query.TypeName);
            }

            if (handlers.Count() > 1)
            {
                throw new QueryHandlerConflictException(query.TypeName, handlers.Select(h => h.GetType().ToPrettyString()).ToArray());
            }

            var handler = (IQueryHandler<TQuery, TResult>)handlers.Single();

            TResult result;

            var queryId = CreateQueryId<TQuery, TResult>(query);

            using var scope = _logger?
                    .BeginScope()
                    .WithCorrelationId(query.CorrelationIdentity)
                    .WithQueryId(queryId)
                    .Build();

            if (_logger is not null)
            {
                s_queryReceived(_logger, default);
            }

            var cacheKey = CreateCacheKey<TQuery, TResult>(query);

            if (_cache is not null && !string.IsNullOrWhiteSpace(cacheKey))
            {
                var foundInCache = await _cache.GetAsync(cacheKey, cancellation).ConfigureAwait(false);

                if (foundInCache is not null)
                {
                    try
                    {
                        return JsonSerializer.Deserialize<TResult>(Encoding.UTF8.GetString(foundInCache))!;
                    }
                    catch (Exception e)
                    {
                        if (_logger is not null)
                        {
                            s_failedReadingCacheContent(_logger, e);
                        }

                        await _cache.RemoveAsync(cacheKey, cancellation).ConfigureAwait(false);
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
                result = await handler.ExecuteAsync(query, cancellation)
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

            if (_cache is not null && !string.IsNullOrWhiteSpace(cacheKey))
            {
                try
                {
                    if (_options is null)
                    {
                        await _cache.SetAsync(cacheKey, JsonSerializer.SerializeToUtf8Bytes(result), cancellation).ConfigureAwait(false);
                    }
                    else
                    {
                        await _cache.SetAsync(cacheKey, JsonSerializer.SerializeToUtf8Bytes(result), _options, cancellation).ConfigureAwait(false);
                    }
                }
                catch (Exception e)
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

        private static Guid CreateQueryId<TQuery, TResult>(TQuery query)
            where TQuery : notnull, IQuery<TResult>
            where TResult : notnull =>
            DeterministicGuidFactory.Instance(query.CorrelationIdentity.Value, Guid.NewGuid().ToByteArray()).Create();

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Exception is being logged in all cases.")]
        private string? CreateCacheKey<TQuery, TResult>(TQuery query)
            where TQuery : notnull, IQuery<TResult>
            where TResult : notnull
        {
            var cacheKey = string.Empty;

            try
            {
                cacheKey = DeterministicGuidFactory.Instance(s_queriesNamespace, JsonSerializer.SerializeToUtf8Bytes(query)).Create().ToString("D");
            }
            catch (Exception e)
            {
                if (_logger is not null)
                {
                    s_failedCreatingCacheKey(_logger, e);
                }
            }

            return cacheKey;
        }
    }
}
