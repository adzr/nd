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
using Nd.Core.Types;
using Nd.Identities.Extensions;

namespace Nd.Commands
{
    public class CommandBus : ICommandBus
    {
        private static readonly ILookup<Type, Type> s_catalog = TypeDefinitions
            .GetAllImplementations<ICommandHandler>()
            .SelectMany(t => t.GetInterfacesOfType<ICommandHandler>().Select(i => (Interface: i, Implementation: t)))
            .Where(item =>
            {
                var commandType = item.Interface.GetGenericTypeArgumentsOfType<ICommand>().FirstOrDefault();

                return
                    commandType is not null &&
                    item.Implementation.HasMethodWithParametersOfTypes("ExecuteAsync", commandType, typeof(CancellationToken));
            })
            .Select(item => (CommandType: item.Interface.GetGenericTypeArgumentsOfType<ICommand>().First(), item.Implementation))
            .ToLookup(item => item.Implementation, item => item.CommandType);

        private static readonly Action<ILogger, Exception?> s_commandReceived =
            LoggerMessage.Define(LogLevel.Trace, new EventId(
                                CommandBusLoggingEventsConstants.CommandReceived,
                                nameof(CommandBusLoggingEventsConstants.CommandReceived)),
                                "CommandType received");

        private static readonly Action<ILogger, Exception?> s_commandExecuted =
            LoggerMessage.Define(LogLevel.Trace, new EventId(
                                CommandBusLoggingEventsConstants.CommandExecuted,
                                nameof(CommandBusLoggingEventsConstants.CommandExecuted)),
                                "CommandType executed");

        private static readonly Action<ILogger, Exception?> s_commandStored =
            LoggerMessage.Define(LogLevel.Trace, new EventId(
                                CommandBusLoggingEventsConstants.CommandStored,
                                nameof(CommandBusLoggingEventsConstants.CommandStored)),
                                "CommandType stored");

        private readonly IDictionary<Type, ICommandHandler> _commandHandlerRegistery;

        private readonly ICommandWriter _commandWriter;
        private readonly ILogger<CommandBus>? _logger;
        private readonly ActivitySource _activitySource;

        public CommandBus(
            ICommandHandler[] commandHandlers,
            ICommandWriter commandWriter,
            ILogger<CommandBus>? logger,
            ActivitySource? activitySource)
        {
            _commandWriter = commandWriter ?? throw new ArgumentNullException(nameof(commandWriter));
            _logger = logger;
            _activitySource = activitySource ?? new ActivitySource(GetType().Name);

            if (commandHandlers is null)
            {
                throw new ArgumentNullException(nameof(commandHandlers));
            }

            _commandHandlerRegistery = commandHandlers
                .Where(h => h is not null)
                .SelectMany(h => s_catalog[h.GetType()].Select(commandType => (CommandType: commandType, Handler: h)))
                .GroupBy(g => g.CommandType)
                .ToDictionary(g => g.Key, g =>
                {
                    try
                    {
                        return g.Single().Handler;
                    }
                    catch (Exception e)
                    {
                        throw new CommandHandlerConflictException(g.Key, g.Select(h => h.GetType()).ToArray(), e);
                    }
                });
        }

        public async Task<TResult> ExecuteAsync<TCommand, TResult>(
            TCommand command,
            CancellationToken cancellation = default)
            where TResult : notnull, IExecutionResult<TCommand>
            where TCommand : ICommand<TResult>
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

            // Fetching and validating command handler call.
            if (!_commandHandlerRegistery.TryGetValue(command.GetType(), out var handler))
            {
                throw new CommandNotRegisteredException(command);
            }

            TResult result;

            try
            {
                // Notify that the command is received and about to be executed.
                await command.AcknowledgeAsync().ConfigureAwait(false);

                // Execute the command and keep the result.
                result = await ((ICommandHandler<TCommand, TResult>)handler).ExecuteAsync(command, cancellation)
                    .ConfigureAwait(false);

                if (result is null)
                {
                    throw new CommandExecutionException(command, new CommandExecutionException($"Command handler cannot return a null result"));
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
