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

using System.Diagnostics.CodeAnalysis;
using Nd.Aggregates.Events;
using Nd.Samples.Banking.Domain.Accounts.Events;
using Nd.Samples.Banking.Domain.Common;

namespace Nd.Samples.Banking.Domain.Accounts
{
    public interface IAccountState : IAggregateState<IAccountState>
    {
        string AccountNumber { get; }
        Currency Currency { get; }
        AccountType AccountType { get; }
        decimal Balance { get; }
        bool IsOpened { get; }
        bool IsFrozen { get; }
        bool IsClosed { get; }
    }

    public static class AccountEventNames
    {
        public const string AccountOpened = "ACCOUNT_OPENED";
        public const string AccountClosed = "ACCOUNT_CLOSED";
        public const string AccountAmountDeposited = "ACCOUNT_AMOUNT_DEPOSITED";
        public const string AccountFreezed = "ACCOUNT_FREEZED";
        public const string AccountUnFreezed = "ACCOUNT_UNFREEZED";
    }

    public record class AccountState : AggregateState<IAccountState>, IAccountState,
        ICanHandleAggregateEvent<AccountOpenedV1>,
        ICanHandleAggregateEvent<AccountClosedV1>,
        ICanHandleAggregateEvent<AccountAmountDepositedV2>,
        ICanHandleAggregateEvent<AccountFreezedV1>,
        ICanHandleAggregateEvent<AccountUnFreezedV1>
    {
        public override IAccountState State => this;
        public string AccountNumber { get; private set; } = "";
        public Currency Currency { get; private set; }
        public AccountType AccountType { get; private set; }
        public decimal Balance { get; private set; }
        public bool IsFrozen { get; private set; }
        public bool IsOpened { get; private set; }
        public bool IsClosed { get; private set; }

        public void Handle([NotNull] AccountOpenedV1 aggregateEvent)
        {
            AccountNumber = aggregateEvent.AccountNumber;
            Currency = aggregateEvent.Currency;
            AccountType = aggregateEvent.AccountType;
            Balance = 0m;
            IsOpened = false;
            IsFrozen = false;
            IsClosed = false;
        }

        public void Handle([NotNull] AccountClosedV1 aggregateEvent) => IsClosed = true;

        public void Handle([NotNull] AccountAmountDepositedV2 aggregateEvent) => Balance += aggregateEvent.DepositedAmount;

        public void Handle([NotNull] AccountFreezedV1 aggregateEvent) => IsFrozen = true;

        public void Handle([NotNull] AccountUnFreezedV1 aggregateEvent) => IsFrozen = false;
    }
}
