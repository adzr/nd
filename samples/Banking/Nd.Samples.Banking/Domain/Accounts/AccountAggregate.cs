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
using System.Threading;
using System.Threading.Tasks;
using Nd.Aggregates;
using Nd.Aggregates.Events;
using Nd.Aggregates.Persistence;
using Nd.Commands;
using Nd.Commands.Results;
using Nd.Samples.Banking.Domain.Accounts.Commands;
using Nd.Samples.Banking.Domain.Accounts.Events;
using Nd.Samples.Banking.Domain.Accounts.Exceptions;
using Nd.Samples.Banking.Domain.Common;

namespace Nd.Samples.Banking.Domain.Accounts
{
    [NamedAggregate(AccountAggregateName)]
    public class AccountAggregate : AggregateRoot<AccountId, IAccountState>
    {
        public const string AccountAggregateName = "ACCOUNT";

        public AccountAggregate(AccountId identity, AggregateStateFactoryFunc<IAccountState> initialStateProvider, uint version) : base(identity, initialStateProvider, version) { }

        public void OpenAccount(AccountType accountType, Currency currency, AggregateEventMetadata? meta = default)
        {
            if (State.IsClosed)
            {
                throw new UsingClosedAccountException(State.AccountNumber);
            }

            if (State.IsOpened)
            {
                throw new AccountAlreadyOpenedException(State.AccountNumber);
            }

            Emit(new AccountOpenedV1(Identity.Value, currency, accountType), meta);
        }

        public void CloseAccount(string description, AggregateEventMetadata? meta = default)
        {
            if (State.IsClosed)
            {
                throw new UsingClosedAccountException(State.AccountNumber);
            }

            if (!State.IsOpened)
            {
                throw new UsingNonOpenedAccountException(State.AccountNumber);
            }

            Emit(new AccountClosedV1(Identity.Value, description), meta);
        }

        public void FreezeAccount(string description, AggregateEventMetadata? meta = default)
        {
            if (State.IsClosed)
            {
                throw new UsingClosedAccountException(State.AccountNumber);
            }

            if (!State.IsOpened)
            {
                throw new UsingNonOpenedAccountException(State.AccountNumber);
            }

            if (State.IsFrozen)
            {
                throw new UsingFrozenAccountException(State.AccountNumber);
            }

            Emit(new AccountFreezedV1(Identity.Value, description), meta);
        }

        public void UnFreezeAccount(string description, AggregateEventMetadata? meta = default)
        {
            if (State.IsClosed)
            {
                throw new UsingClosedAccountException(State.AccountNumber);
            }

            if (!State.IsOpened)
            {
                throw new UsingNonOpenedAccountException(State.AccountNumber);
            }

            if (!State.IsFrozen)
            {
                throw new NonFrozenAccountException(State.AccountNumber);
            }

            Emit(new AccountUnFreezedV1(Identity.Value, description), meta);
        }

        public void Deposit(decimal amount, Currency currency = Currency.None, decimal rate = 1, string description = "", AggregateEventMetadata? meta = default)
        {
            if (State.IsClosed)
            {
                throw new UsingClosedAccountException(State.AccountNumber);
            }

            if (!State.IsOpened)
            {
                throw new UsingNonOpenedAccountException(State.AccountNumber);
            }

            if (State.IsFrozen)
            {
                throw new UsingFrozenAccountException(State.AccountNumber);
            }

            Emit(new AccountAmountDepositedV2(Identity.Value, amount, currency.Equals(Currency.None) ? State.Currency : currency, rate, amount, description), meta);
        }

        public void DepositLegacy(decimal amount, string description = "", AggregateEventMetadata? meta = default)
        {
            if (State.IsClosed)
            {
                throw new UsingClosedAccountException(State.AccountNumber);
            }

            if (!State.IsOpened)
            {
                throw new UsingNonOpenedAccountException(State.AccountNumber);
            }

            if (State.IsFrozen)
            {
                throw new UsingFrozenAccountException(State.AccountNumber);
            }

            Emit(new AccountAmountDepositedV1(Identity.Value, amount, description), meta);
        }
    }

    public class AccountAggregateReader : AggregateReader<AccountId, IAccountState>
    {
        public AccountAggregateReader(IAggregateEventReader<AccountId, IAccountState> eventReader) : base(
            () => new AccountState(),
            (id, state, version) => new AccountAggregate(id, state, version),
            eventReader)
        { }
    }

    public class AccountCommandHandler :
        ICommandHandler<OpenAccountCommandV1, GenericExecutionResult>
    {
        private readonly IAggregateReader<AccountId> _aggregateReader;

        public AccountCommandHandler(IAggregateReader<AccountId> aggregateReader)
        {
            _aggregateReader = aggregateReader ?? throw new ArgumentNullException(nameof(aggregateReader));
        }

        public async Task<GenericExecutionResult> ExecuteAsync([NotNull] OpenAccountCommandV1 command, CancellationToken cancellation = default)
        {
            var aggregate = await _aggregateReader.ReadAsync<AccountAggregate>(command.AggregateIdentity, cancellation: cancellation).ConfigureAwait(false);

            try
            {
                aggregate.OpenAccount(command.AccountType, command.Currency,
                    new AggregateEventMetadata(command.IdempotencyIdentity, command.CorrelationIdentity));
            }
            catch (UsingClosedAccountException e)
            {
                return new GenericExecutionResult(command, e);
            }
            catch (AccountAlreadyOpenedException e)
            {
                return new GenericExecutionResult(command, e);
            }

            return new GenericExecutionResult(command);
        }
    }
}
