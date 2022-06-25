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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nd.Aggregates.Identities;
using Nd.Commands.Exceptions;
using Nd.Commands.Persistence;
using Nd.Commands.Results;
using Nd.Core.Extensions;

namespace Nd.Commands
{
    public class CommandBus : ICommandBus
    {
        private readonly ILookup<Type, ICommandHandler> _commandHandlerRegistery;

        private readonly ICommandWriter _commandWriter;

        public CommandBus(ICommandWriter commandWriter, params ICommandHandler[] commandHandlers)
        {
            _commandWriter = commandWriter ?? throw new ArgumentNullException(nameof(commandWriter));

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

        public async Task<TResult> ExecuteAsync<TIdentity, TResult>(
            ICommand<TIdentity, TResult> command,
            CancellationToken cancellationToken = default)
            where TIdentity : IAggregateIdentity
            where TResult : IExecutionResult
        {
            // Validating command reference.
            if (command is null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            // Fetching and validating command aggregate identity.
            var identity = command.AggregateIdentity;

            if (identity is null)
            {
                throw new CommandInvalidAggregateIdentityException(command);
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
                // Execute the command and keep the result.
                result = await handler.ExecuteAsync<TResult>(command, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new CommandExecutionException(command, ex);
            }

            try
            {
                // Store the command and its result.
                await _commandWriter.WriteAsync(command, result, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new CommandPersistenceException(command, result, ex);
            }

            // Finally return the result.
            return result;
        }
    }
}
