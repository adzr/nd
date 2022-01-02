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

using Nd.Core.Extensions;
using System;
using System.Collections.Generic;
using Xunit;

namespace Nd.Core.Tests.Extensions
{
    public class TypeExtensionsTests
    {
        [Theory]
        [InlineData(typeof(string), "String")]
        [InlineData(typeof(int), "Int32")]
        [InlineData(typeof(IEnumerable<>), "IEnumerable<>")]
        [InlineData(typeof(KeyValuePair<,>), "KeyValuePair<,>")]
        [InlineData(typeof(IEnumerable<string>), "IEnumerable<String>")]
        [InlineData(typeof(IEnumerable<IEnumerable<string>>), "IEnumerable<IEnumerable<String>>")]
        [InlineData(typeof(KeyValuePair<bool, long>), "KeyValuePair<Boolean,Int64>")]
        [InlineData(typeof(KeyValuePair<KeyValuePair<bool, long>, KeyValuePair<bool, long>>), "KeyValuePair<KeyValuePair<Boolean,Int64>,KeyValuePair<Boolean,Int64>>")]
        public void CanConvertToPrettyString(Type type, string expectedPrettyString) =>
            Assert.Equal(expectedPrettyString, type.ToPrettyString());
    }
}