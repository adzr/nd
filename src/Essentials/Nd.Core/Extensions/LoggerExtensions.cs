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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Nd.Core.Extensions
{
    public static class LoggerExtensions
    {
        public interface IScopeBuilder
        {
            IScopeBuilder WithProperty(string key, object value);

            IDisposable Build();
        }

        internal class ScopeBuilder : IScopeBuilder
        {
            internal sealed class EmptyDisposable : IDisposable
            {
                public void Dispose()
                {

                }
            }

            private readonly ILogger? _logger;
            private readonly IDictionary<string, object?> _properties = new ConcurrentDictionary<string, object?>();

            public ScopeBuilder(ILogger? logger)
            {
                _logger = logger;
            }

            public IScopeBuilder WithProperty(string key, object value)
            {
                if (_logger is not null)
                {
                    _properties[key] = value;
                }

                return this;
            }

            public IDisposable Build() => _logger?.BeginScope(_properties.AsEnumerable()) ?? new EmptyDisposable();
        }

        public static IScopeBuilder BeginScope(this ILogger? logger) => new ScopeBuilder(logger);
    }
}
