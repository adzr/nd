/*
 * Copyright © 2015 - 2021 Rasmus Mikkelsen
 * Copyright © 2015 - 2021 eBay Software Foundation
 * Modified from original source https://github.com/eventflow/EventFlow
 * 
 * Copyright © 2018 - 2021 Lutando Ngqakaza
 * Modified from original source https://github.com/Lutando/Akkatecture
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
using Nd.Commands.Results;
using Nd.Core.Types;
using Nd.Identities;
using Nd.ValueObjects;

namespace Nd.Commands
{
    public abstract record class Command<TResult> : ValueObject, ICommand<TResult>
        where TResult : notnull, IExecutionResult
    {
        private string? _cachedTypeName;
        private uint? _cachedTypeVersion;

        protected Command(IIdempotencyIdentity idempotencyIdentity, ICorrelationIdentity correlationIdentity, DateTimeOffset? acknowledged = default)
        {
            IdempotencyIdentity = idempotencyIdentity;
            CorrelationIdentity = correlationIdentity;
            Acknowledged = acknowledged;
        }

        public string TypeName
        {
            get
            {
                if (_cachedTypeName is null)
                {
                    (_cachedTypeName, _cachedTypeVersion) = TypeDefinitions.ResolveNameAndVersion(GetType());
                }

                return _cachedTypeName;
            }
        }

        public uint TypeVersion
        {
            get
            {
                if (_cachedTypeVersion is null)
                {
                    (_cachedTypeName, _cachedTypeVersion) = TypeDefinitions.ResolveNameAndVersion(GetType());
                }

                return _cachedTypeVersion ?? 0u;
            }
        }

        public IIdempotencyIdentity IdempotencyIdentity { get; }

        public ICorrelationIdentity CorrelationIdentity { get; }

        public DateTimeOffset? Acknowledged { get; private set; }

        public Task AcknowledgeAsync()
        {
            Acknowledged ??= DateTimeOffset.UtcNow;
            return Task.CompletedTask;
        }
    }
}
