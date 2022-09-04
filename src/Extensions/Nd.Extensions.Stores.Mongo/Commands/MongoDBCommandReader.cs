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
using Nd.Identities;
using Nd.Identities.Extensions;

namespace Nd.Extensions.Stores.Mongo.Commands
{
    public sealed class MongoDBCommandReader : MongoAccessor, ICommandReader
    {
        private static readonly Action<ILogger, Exception?> s_commandQueryReceived =
            LoggerMessage.Define(LogLevel.Trace, new EventId(
                                MongoLoggingEventsConstants.CommandQueryReceived,
                                nameof(MongoLoggingEventsConstants.CommandQueryReceived)),
                                "Command query received");

        static MongoDBCommandReader()
        {
            BsonDefaultsInitializer.Initialize();
        }

        private readonly ILogger? _logger;
        private readonly ActivitySource _activitySource;

        public MongoDBCommandReader(MongoClient client,
            string databaseName,
            string collectionName,
            ILogger<MongoDBCommandReader>? logger,
            ActivitySource? activitySource) :
            base(client, databaseName, collectionName)
        {
            _logger = logger;
            _activitySource = activitySource ?? new ActivitySource(GetType().Name);
        }

        public async Task<TResult?> ReadAsync<TResult>(
            Guid commandId,
            ICorrelationIdentity correlationIdentity,
            CancellationToken cancellation = default)
            where TResult : IExecutionResult
        {
            using var activity = _activitySource.StartActivity(nameof(ReadAsync));

            using var scope = _logger
                .BeginScope()
                .WithCorrelationId(correlationIdentity)
                .WithCommandId(commandId)
                .Build();

            if (_logger is not null)
            {
                s_commandQueryReceived(_logger, default);
            }

            var cursor = await GetCollection<MongoCommandDocument>()
                .FindAsync<MongoCommandDocument>(
                Builders<MongoCommandDocument>.Filter.Eq(d => d.Id, commandId),
                cancellationToken: cancellation)
                .ConfigureAwait(false);

            _ = activity?.AddCommandIdTag(commandId);

            if (correlationIdentity is not null)
            {
                _ = activity?.AddCorrelationsTag(new[] { correlationIdentity.Value });
            }

            cancellation.ThrowIfCancellationRequested();

            var document = await cursor
                .SingleOrDefaultAsync(cancellation)
                .ConfigureAwait(false);

            return document?.Result is null ? default : (TResult)document.Result;
        }
    }
}
