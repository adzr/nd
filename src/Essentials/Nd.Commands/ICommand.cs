﻿/*
 * Copyright © 2015 - 2021 Rasmus Mikkelsen
 * Copyright © 2015 - 2021 eBay Software Foundation
 * Modified from original source https://github.com/eventflow/EventFlow
 * 
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
using System.Threading.Tasks;
using Nd.Aggregates.Identities;
using Nd.Commands.Results;
using Nd.Core.Types.Versions;
using Nd.Identities;

namespace Nd.Commands
{
    public interface ICommand : IVersionedType
    {
        IIdempotencyIdentity IdempotencyIdentity { get; }
        ICorrelationIdentity CorrelationIdentity { get; }
        DateTimeOffset? Acknowledged { get; }
        Task AcknowledgeAsync();
    }

    public interface ICommand<TResult> : ICommand
        where TResult : notnull, IExecutionResult
    { }

    public interface ICommand<TIdentity, TResult> : ICommand<TResult>
        where TIdentity : notnull, IAggregateIdentity
        where TResult : notnull, IExecutionResult
    {
        TIdentity AggregateIdentity { get; }
    }
}
