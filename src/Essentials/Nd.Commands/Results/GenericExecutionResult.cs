/*
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
using Nd.Core.Types;
using Nd.ValueObjects;

namespace Nd.Commands.Results
{
    public record class GenericExecutionResult<T> : ValueObject, IExecutionResult<T>
        where T : notnull, ICommand
    {
        private string? _message;
        private string? _cachedTypeName;
        private uint? _cachedTypeVersion;

        public string TypeName
        {
            get
            {
                if (_cachedTypeName is null)
                {
                    (_cachedTypeName, _cachedTypeVersion) = TypeDefinitions.GetNameAndVersion(GetType());
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
                    (_cachedTypeName, _cachedTypeVersion) = TypeDefinitions.GetNameAndVersion(GetType());
                }

                return _cachedTypeVersion ?? 0u;
            }
        }

        public GenericExecutionResult(
            T command,
            Exception? exception = default,
            DateTimeOffset? acknowledged = default)
        {
            Command = command;
            Acknowledged = acknowledged;
            Exception = exception;
        }

        public T Command { get; private set; }
        public bool IsSuccess => Exception is null;
        public DateTimeOffset? Acknowledged { get; private set; }
        public Exception? Exception { get; private set; }

        private static string ResolveExceptionString(Exception exception) =>
            $"{Environment.NewLine}{Environment.NewLine}{exception}";

        public override string ToString()
        {
            if (_message is null)
            {
                _message = Exception is null ?
                    $"Successful execution: {Command}" :
                    $"Failed execution: {Command}: {ResolveExceptionString(Exception)}";
            }

            return _message;
        }

        public Task AcknowledgeAsync()
        {
            Acknowledged ??= DateTimeOffset.UtcNow;
            return Task.CompletedTask;
        }
    }
}
