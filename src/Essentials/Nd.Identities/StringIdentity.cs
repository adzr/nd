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
using Nd.Core.Extensions;
using Nd.Core.Types;
using Nd.ValueObjects;

namespace Nd.Identities
{
    public abstract record class StringIdentity : ValueObject, IIdentity<string>
    {
        private readonly string _stringValue;

        public string Value { get; }

        protected StringIdentity(string value)
        {
            TypeName = TypeDefinitions.ResolveNameAndVersion(GetType()).Name;
            Value = value;
            _stringValue = $"{TypeName.ToSnakeCase().TrimEnd(StringComparison.OrdinalIgnoreCase, "_id", "_identity")}-{value}";
        }

        public string TypeName { get; }

        public sealed override string ToString() => _stringValue;
    }
}
