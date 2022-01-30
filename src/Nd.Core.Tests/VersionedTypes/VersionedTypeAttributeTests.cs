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

using Nd.Core.Types.Versions;
using System;
using Xunit;

namespace Nd.Core.Tests.VersionedTypes
{
    internal class TestVersionedTypeAttribute : VersionedTypeAttribute
    {
        public TestVersionedTypeAttribute(string name, uint version) : base(name, version) { }
    }

    public class VersionedTypeAttributeTests
    {
        [Theory]
        [InlineData("ValidName", 1)]
        [InlineData("Named With Spaces", 1)]
        [InlineData("ValidName", 2)]
        [InlineData("Named With Spaces", 2)]
        public void CanHaveValidInput(string name, uint version) =>
            Assert.Equal(version, new TestVersionedTypeAttribute(name, version).TypeVersion);

        [Fact]
        public void FailsOnInvalidVersion() =>
                    Assert.Throws<ArgumentOutOfRangeException>(() => new TestVersionedTypeAttribute("ValidName", 0));
    }
}
