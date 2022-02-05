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

using Nd.Aggregates;
using Nd.Aggregates.Exceptions;
using Nd.Aggregates.Persistence;
using Nd.Commands.Results;
using Nd.Core.Extensions;
using Nd.Identities;

namespace Nd.Commands
{
    public class CommandBus : ICommandBus
    {
        private readonly ILookup<Type, ICommandHandler> _commandHandlerRegistery;

        private readonly ILookup<Type, IAggregateManager> _aggregateManagerRegistery;

        private static ILookup<Type, TElement> BuildLookupFor<TKey, TElement>(TElement[] elements, Func<string, string[], Exception> generateException) => elements
            .Where(element => element is not null)
            // Mapping all key types to there element instances.
            .SelectMany(element => element!
                        // So foreach element type.
                        .GetType()
                        // We get all of the interfaces that it implements that are of type TElement.
                        .GetInterfacesOfType<TElement>()
                        // If any of these ICommandHandler interfaces has a generic type of ICommand then get it along with the handler instance.
                        .Select(i => (Element: element, KeyType: i.GetGenericTypeArgumentsOfType<TKey>().FirstOrDefault()))
            )
            // After flattening our selection, now filter on those interfaces that actually have a generic type of ICommand.
            .Where(r => r.KeyType is not null)
            // Then group by command type which cannot be null at this point.
            .GroupBy(r => r.KeyType!)
            // Validate that there are no multiple handlers for any single command.
            .Select(g => g.Count() == 1 ? g.Single() : throw generateException(g.Key.GetName(), g.Select(r => r.Element!.GetType().ToPrettyString()).ToArray()))
            // Finally convert it to a lookup of a command type as a key and a handler instance as a one record value.
            .ToLookup(r => r.KeyType!, r => r.Element);


        public CommandBus(ICommandHandler[] commandHandlers, IAggregateManager[] aggregateManagers)
        {
            if (commandHandlers is null)
                throw new ArgumentNullException(nameof(commandHandlers));

            if (!commandHandlers.Any())
                throw new IndexOutOfRangeException($"Zero command handlers specified in array {nameof(commandHandlers)}");

            _commandHandlerRegistery = BuildLookupFor<ICommand, ICommandHandler>(commandHandlers,
                (commandType, commandHandlerTypes) => new CommandHandlerConflictException(commandType, commandHandlerTypes));


            _aggregateManagerRegistery = BuildLookupFor<IIdentity, IAggregateManager>(aggregateManagers,
                (commandType, commandHandlerTypes) => new CommandHandlerConflictException(commandType, commandHandlerTypes));
        }

        public async Task<TResult> ExecuteAsync<TAggregate, TIdentity, TResult>(ICommand<TAggregate, TIdentity, TResult> command, CancellationToken cancellationToken = default)
            where TAggregate : IAggregateRoot<TIdentity>
            where TIdentity : IIdentity<TIdentity>
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

            // Fetching and validating aggregate manager.
            var managers = _aggregateManagerRegistery[identity.GetType()];

            if (managers is null || !managers.Any())
            {
                throw new CommandMissingAggregateManagerException(command);
            }

            if (managers.Count() > 1)
            {
                throw new CommandAggregateManagerConflictException(command, managers.ToArray());
            }

            var manager = managers.Single();

            // Load and validate aggregate.
            var aggregate = await manager.LoadAsync(identity, cancellationToken);

            if (aggregate is null)
            {
                throw new CommandInvalidAggregateException(command);
            }

            // Finally execute the command against the aggregate.
            return (TResult)await handler.ExecuteAsync(aggregate, command, cancellationToken);
        }
    }
}