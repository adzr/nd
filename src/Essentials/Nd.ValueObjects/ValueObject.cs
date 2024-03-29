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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Nd.Core.Extensions;

namespace Nd.ValueObjects
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public abstract record class ValueObject : IComparable
    {
        private static readonly ConcurrentDictionary<Type, IReadOnlyCollection<PropertyInfo>> s_typePropertiesCache = new();

        protected virtual IEnumerable<PropertyInfo> GetProperties() =>
            s_typePropertiesCache.GetOrAdd(GetType(), t => t
                    .GetTypeInfo()
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(p => p.CanRead)
                    .OrderBy(p => p.Name)
                    .ToList());

        protected virtual IEnumerable<object?> GetComparisonProperties() => GetProperties()
            .Select(x => x.GetValue(this));

        public virtual int CompareTo(object? obj) => Equals(obj)
                ? 0
                : obj is null
                ? 1
                : obj is not ValueObject other
                ? throw new ArgumentException($"Cannot compare '{GetType().ToPrettyString()}' and '{obj.GetType().ToPrettyString()}'")
                : GetComparisonProperties().CompareTo(other.GetComparisonProperties());

        public int CompareTo(ValueObject? other) => CompareTo((object?)other);

        public static bool operator <(ValueObject left, ValueObject right) => (left?.CompareTo(right) ?? (right is null ? 0 : -1)) < 0;

        public static bool operator <=(ValueObject left, ValueObject right) => (left?.CompareTo(right) ?? (right is null ? 0 : -1)) <= 0;

        public static bool operator >(ValueObject left, ValueObject right) => (left?.CompareTo(right) ?? (right is null ? 0 : -1)) > 0;

        public static bool operator >=(ValueObject left, ValueObject right) => (left?.CompareTo(right) ?? (right is null ? 0 : -1)) >= 0;

        private string GetDebuggerDisplay() => ToString();
    }
}
