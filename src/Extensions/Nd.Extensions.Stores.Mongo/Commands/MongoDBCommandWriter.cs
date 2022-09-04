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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Nd.Commands.Extensions;
using Nd.Commands.Persistence;
using Nd.Commands.Results;
using Nd.Core.Extensions;
using Nd.Extensions.Stores.Mongo.Common;
using Nd.Extensions.Stores.Mongo.Exceptions;
using Nd.Identities.Extensions;

namespace Nd.Extensions.Stores.Mongo.Commands
{
    public sealed class MongoDBCommandWriter : MongoAccessor, ICommandWriter
    {
        private static readonly Action<ILogger, Exception?> s_mongoInserted =
            LoggerMessage.Define(LogLevel.Trace, new EventId(
                                MongoLoggingEventsConstants.MongoResultReceived,
                                nameof(MongoLoggingEventsConstants.MongoResultReceived)),
                                "Mongo document inserted");

        private static readonly Action<ILogger, Exception?> s_mongoNotSupportingTransactions =
            LoggerMessage.Define(LogLevel.Warning, new EventId(
                                MongoLoggingEventsConstants.MongoNotSupportingTransactions,
                                nameof(MongoLoggingEventsConstants.MongoNotSupportingTransactions)),
                                "Mongo transactions are not supported");
        static MongoDBCommandWriter()
        {
            BsonDefaultsInitializer.Initialize();
        }

        private readonly ILogger<MongoDBCommandWriter>? _logger;
        private readonly ActivitySource _activitySource;

        public MongoDBCommandWriter(
            MongoClient client,
            string databaseName,
            string collectionName,
            ILogger<MongoDBCommandWriter>? logger,
            ActivitySource? activitySource) :
            base(client, databaseName, collectionName)
        {
            _logger = logger;
            _activitySource = activitySource ?? new ActivitySource(GetType().Name);
        }

        public async Task WriteAsync<TResult>(TResult result, CancellationToken cancellation = default)
            where TResult : notnull, IExecutionResult
        {
            if (result is null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            using var activity = _activitySource.StartActivity(nameof(WriteAsync));

            using var session = await Client
                .StartSessionAsync(cancellationToken: cancellation)
                .ConfigureAwait(false);

            if (session is null)
            {
                throw new MongoSessionException("Failed to start mongo session");
            }

            await WriteInternalAsync(
                session, activity, result, cancellation)
                .ConfigureAwait(false);
        }

        private async Task WriteInternalAsync<TResult>(IClientSessionHandle session, Activity? activity, TResult result, CancellationToken cancellation)
            where TResult : notnull, IExecutionResult
        {
            var command = result.Command;

            using var _ = _logger
                .BeginScope()
                .WithCorrelationId(command.CorrelationIdentity)
                .WithCommandId(command.IdempotencyIdentity.Value)
                .Build();

            try
            {
                StartTransaction(
                    session, activity, result);
            }
            catch (NotSupportedException e)
            {
                if (_logger is not null)
                {
                    s_mongoNotSupportingTransactions(_logger, e);
                }
            }

            try
            {
                cancellation.ThrowIfCancellationRequested();

                await GetCollection<MongoCommandDocument>()
                    .InsertOneAsync(new MongoCommandDocument
                    {
                        Id = command.IdempotencyIdentity.Value,
                        Result = result
                    },
                    new InsertOneOptions { }, cancellation)
                    .ConfigureAwait(false);

                LogMongoResult(result);
            }
            catch
            {
                await AbortTransaction(
                    session, activity, result, cancellation)
                    .ConfigureAwait(false);
                throw;
            }

            await CommitTranaction(session, activity, result, cancellation)
                .ConfigureAwait(false);
        }

        private static void StartTransaction<TResult>(IClientSessionHandle session, Activity? activity, TResult result)
            where TResult : notnull, IExecutionResult
        {
            session.StartTransaction();
            AddActivityTags(activity, result);
        }

        private static void AddActivityTags<TResult>(Activity? activity, TResult result)
            where TResult : notnull, IExecutionResult =>
            _ = activity?
                .AddCorrelationsTag(new[] { result.Command.CorrelationIdentity.Value })
                .AddCommandIdTag(result.Command.IdempotencyIdentity.Value);

        private void LogMongoResult<TResult>(TResult result)
            where TResult : notnull, IExecutionResult
        {
            var command = result.Command;

            if (_logger is not null)
            {
                s_mongoInserted(_logger, default);
            }
        }

        private static async Task AbortTransaction<TResult>(IClientSessionHandle session, Activity? activity, TResult result, CancellationToken cancellation)
            where TResult : notnull, IExecutionResult
        {
            if (session.IsInTransaction)
            {
                await session.AbortTransactionAsync(cancellation).ConfigureAwait(false);
                AddActivityTags(activity, result);
                _ = activity?.AddTag(MongoActivityConstants.MongoSuccessfulResultTag, false);
            }
        }

        private static async Task CommitTranaction<TResult>(IClientSessionHandle session, Activity? activity, TResult result, CancellationToken cancellation)
            where TResult : notnull, IExecutionResult
        {
            if (session.IsInTransaction)
            {
                await session.CommitTransactionAsync(cancellation).ConfigureAwait(false);
                AddActivityTags(activity, result);
                _ = activity?.AddTag(MongoActivityConstants.MongoSuccessfulResultTag, true);
            }
        }
    }
}
