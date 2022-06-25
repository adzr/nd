﻿/*
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
using Nd.Commands;
using Nd.Core.Factories;
using Nd.Identities;

namespace Nd.Samples.Banking.Domain.Accounts.Commands
{
    [VersionedCommand(CloseAccountCommandName, 1)]
    public record class CloseAccountCommandV1(
        ICorrelationIdentity CorrelationIdentity,
        AccountId AggregateIdentity,
        string Description) : Command<AccountId>(new IdempotencyIdentity(DeterministicGuidFactory.Instance(s_namespace, AggregateIdentity.Value)), CorrelationIdentity, AggregateIdentity)
    {
        public const string CloseAccountCommandName = "CLOSE_ACCOUNT_COMMAND";

        private static readonly Guid s_namespace = Guid.Parse("1c06ed84-694b-4910-a2ae-a3a449cac38a");
    };
}
