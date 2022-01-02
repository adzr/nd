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

using System.Collections.Concurrent;
using System.Reflection;

namespace Nd.ValueObjects
{
    public abstract class ValueObject
    {
        private static readonly ConcurrentDictionary<Type, IReadOnlyCollection<PropertyInfo>> TypePropertiesCache = new();

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is null || !GetType().Equals(obj.GetType()))
            {
                return false;
            }

            return GetEqualityAttributes().SequenceEqual(((ValueObject)obj).GetEqualityAttributes());
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return GetEqualityAttributes().Aggregate(17, (current, obj) => current * 23 + (obj?.GetHashCode() ?? 0));
            }
        }

        public static bool operator ==(ValueObject left, ValueObject right) => Equals(left, right);

        public static bool operator !=(ValueObject left, ValueObject right) => !Equals(left, right);

        public override string ToString() =>
            $"{{{string.Join(", ", GetProperties().Select(f => $"{f.Name}: {f.GetValue(this)}"))}}}";

        protected virtual IEnumerable<object?> GetEqualityAttributes() =>
            GetProperties().Select(x => x.GetValue(this));

        protected virtual IEnumerable<PropertyInfo> GetProperties() =>
            TypePropertiesCache.GetOrAdd(GetType(), t => t
                    .GetTypeInfo()
                    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(p => p.CanRead)
                    .OrderBy(p => p.Name)
                    .ToList());
    }
}