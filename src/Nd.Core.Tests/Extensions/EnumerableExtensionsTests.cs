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

using Nd.Core.Extensions;
using Xunit;

namespace Nd.Core.Tests.Extensions
{
    public class EnumerableExtensionsTests
    {
        [Theory]
        [InlineData(null, null, 0)]
        [InlineData(new string?[0], null, 1)]
        [InlineData(null, new string?[0], -1)]
        [InlineData(new string?[0], new string?[] { null }, -1)]
        [InlineData(new string?[] { null }, new string?[0], 1)]
        [InlineData(new string?[] { null }, new string?[] { null }, 0)]
        [InlineData(new string?[] { "" }, new string?[] { null }, 1)]
        [InlineData(new string?[] { null }, new string?[] { "" }, -1)]
        [InlineData(new string?[] { "" }, new string?[] { "" }, 0)]
        [InlineData(new string?[] { "abc" }, new string?[] { "ab" }, 1)]
        [InlineData(new string?[] { "ab" }, new string?[] { "abc" }, -1)]
        [InlineData(new string?[] { "ab", "abc" }, new string?[] { "abc", "ab" }, -1)]
        [InlineData(new string?[] { "ab", "abc" }, new string?[] { "ab", "abc" }, 0)]
        [InlineData(new string?[] { "ab", "abc", "a" }, new string?[] { "ab", "abc", null }, 1)]
        [InlineData(new string?[] { "ab", "abc", null }, new string?[] { "ab", "abc", "a" }, -1)]
        public void CanCompareTo(string?[]? left, string?[]? right, int expected)
            => Assert.Equal(expected, left.CompareTo(right));
    }
}