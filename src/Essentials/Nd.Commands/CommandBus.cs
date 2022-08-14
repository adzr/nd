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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nd.Commands.Common;
using Nd.Commands.Exceptions;
using Nd.Commands.Extensions;
using Nd.Commands.Persistence;
using Nd.Commands.Results;
using Nd.Core.Extensions;
using Nd.Identities.Extensions;

namespace Nd.Commands
{
    public class CommandBus : ICommandBus
    {
        private static readonly Action<ILogger, Exception?> s_commandReceived =
            LoggerMessage.Define(LogLevel.Trace, new EventId(
                                CommandBusLoggingEventsConstants.CommandReceived,
                                nameof(CommandBusLoggingEventsConstants.CommandReceived)),
                                "Command received");

        private static readonly Action<ILogger, Exception?> s_commandExecuted =
            LoggerMessage.Define(LogLevel.Trace, new EventId(
                                CommandBusLoggingEventsConstants.CommandExecuted,
                                nameof(CommandBusLoggingEventsConstants.CommandExecuted)),
                                "Command executed");

        private static readonly Action<ILogger, Exception?> s_commandStored =
            LoggerMessage.Define(LogLevel.Trace, new EventId(
                                CommandBusLoggingEventsConstants.CommandStored,
                                nameof(CommandBusLoggingEventsConstants.CommandStored)),
                                "Command stored");

        private readonly ILookup<Type, ICommandHandler> _commandHandlerRegistery;

        private readonly ICommandWriter _commandWriter;
        private readonly ILogger? _logger;
        private readonly ActivitySource _activitySource;

        public CommandBus(
            ICommandHandler[] commandHandlers,
            ICommandWriter commandWriter,
            ILoggerFactory? loggerFactory,
            ActivitySource? activitySource)
        {
            _commandWriter = commandWriter ?? throw new ArgumentNullException(nameof(commandWriter));
            _logger = loggerFactory?.CreateLogger(GetType());
            _activitySource = activitySource ?? new ActivitySource(GetType().Name);

            if (commandHandlers is null)
            {
                throw new ArgumentNullException(nameof(commandHandlers));
            }

            _commandHandlerRegistery = commandHandlers
                .Where(handler => handler is not null)
                // Mapping all key types to there command handlers instances.
                .SelectMany(handler => handler!
                        // So foreach handler type.
                        .GetType()
                        // We get all of the interfaces that it implements that are of type ICommandHandler.
                        .GetInterfacesOfType<ICommandHandler>()
                        // If any of these ICommandHandler interfaces has a generic type of ICommand then get it along with the handler instance.
                        .Select(i => (Handler: handler, CommandType: i.GetGenericTypeArgumentsOfType<ICommand>().FirstOrDefault()))
                )
                // After flattening our selection, now filter on those interfaces that actually have a generic type of ICommand.
                .Where(r => r.CommandType is not null)
                // Then group by command type which cannot be null at this point.
                .GroupBy(r => r.CommandType!)
                // Validate that there are no multiple handlers for any single command.
                .Select(g => g.Count() == 1 ? g.Single() : throw new CommandHandlerConflictException(g.Key.GetName(), g.Select(r => r.Handler!.GetType().ToPrettyString()).ToArray()))
                // Finally convert it to a lookup of a command type as a key and a handler instance as a one record value.
                .ToLookup(r => r.CommandType!, r => r.Handler);
        }

        public async Task<TResult> ExecuteAsync<TResult>(
            ICommand<TResult> command,
            CancellationToken cancellation = default)
            where TResult : notnull, IExecutionResult
        {
            // Validating command reference.
            if (command is null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            using var activity = _activitySource.StartActivity(nameof(ExecuteAsync));

            _ = activity?
                .AddCorrelationsTag(new[] { command.CorrelationIdentity.Value })
                .AddCommandIdTag(command.IdempotencyIdentity.Value);

            using var scope = _logger
                    .BeginScope()
                    .WithCorrelationId(command.CorrelationIdentity)
                    .WithCommandId(command.IdempotencyIdentity.Value)
                    .Build();

            if (_logger is not null)
            {
                s_commandReceived(_logger, default);
            }

            // Fetching and validating command handler.
            var handlers = _commandHandlerRegistery[command.GetType()];

            if (handlers is null || !handlers.Any())
            {
                throw new CommandNotRegisteredException(command.TypeName);
            }

            if (handlers.Count() > 1)
            {
                throw new CommandHandlerConflictException(command.TypeName, handlers.Select(h => h.GetType().ToPrettyString()).ToArray());
            }

            var handler = handlers.Single();

            TResult result;

            try
            {
                // Notify that the command is received and about to be executed.
                await command.AcknowledgeAsync().ConfigureAwait(false);

                // Execute the command and keep the result.
                result = await handler.ExecuteAsync<TResult>(command, cancellation)
                    .ConfigureAwait(false);

                if (result is null)
                {
                    throw new CommandExecutionException($"Command handler {handler.GetType().FullName} cannot return a null result");
                }

                // Notify that the result is received.
                await result.AcknowledgeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new CommandExecutionException(command, ex);
            }

            if (_logger is not null)
            {
                using var _ = _logger
                    .BeginScope()
                    .WithCommandResult(result.IsSuccess)
                    .Build();

                s_commandExecuted(_logger, default);
            }

            _ = activity?.AddTag(ActivityConstants.CommandResultSuccessTag, result.IsSuccess);

            try
            {
                // Store the command and its result.
                await _commandWriter.WriteAsync(result, cancellation).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new CommandPersistenceException(command, result, ex);
            }

            if (_logger is not null)
            {
                s_commandStored(_logger, default);
            }

            // Finally return the result.
            return result;
        }
    }
}
