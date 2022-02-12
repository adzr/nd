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
using Nd.Core.Factories;
using Nd.Identities;
using Xunit;

namespace Nd.ValueObjects.Tests {
    [NamedIdentity("sample")]
    internal record class SampleIdentity : GuidIdentity {
        public SampleIdentity(Guid value) : base(value) { }

        public SampleIdentity(IGuidFactory factory) : base(factory) { }
    }

    public class IdentityTests {
        [Theory]
        [InlineData("0b58cdd0-5220-4053-baf7-5dd9d15aa535", "5d819ee0-65b8-4afe-aaae-1849d439e217", -5)]
        [InlineData("5d819ee0-65b8-4afe-aaae-1849d439e217", "0b58cdd0-5220-4053-baf7-5dd9d15aa535", 5)]
        [InlineData("5d819ee0-65b8-4afe-aaae-1849d439e217", "5d819ee0-65b8-4afe-aaae-1849d439e217", 0)]
        public void CanBeCompared(string left, string right, int result) =>
            Assert.Equal(result,
                new SampleIdentity(Guid.ParseExact(left, "D"))
                .CompareTo(new SampleIdentity(Guid.ParseExact(right, "D"))));

        [Theory]
        [InlineData("5d819ee0-65b8-4afe-aaae-1849d439e217", "0b58cdd0-5220-4053-baf7-5dd9d15aa535", false)]
        [InlineData("5d819ee0-65b8-4afe-aaae-1849d439e217", "5d819ee0-65b8-4afe-aaae-1849d439e217", true)]
        public void CanBeEqualled(string left, string right, bool result) =>
            Assert.Equal(result,
                new SampleIdentity(Guid.ParseExact(left, "D"))
                .Equals(new SampleIdentity(Guid.ParseExact(right, "D"))));

        [Theory]
        [InlineData("5d819ee0-65b8-4afe-aaae-1849d439e217", "0b58cdd0-5220-4053-baf7-5dd9d15aa535", false)]
        [InlineData("5d819ee0-65b8-4afe-aaae-1849d439e217", "5d819ee0-65b8-4afe-aaae-1849d439e217", true)]
        public void CanHaveDeterministicHashCode(string left, string right, bool result) =>
            Assert.Equal(result,
                new SampleIdentity(Guid.ParseExact(left, "D")).GetHashCode()
                .Equals(new SampleIdentity(Guid.ParseExact(right, "D")).GetHashCode()));

        [Theory]
        [InlineData("0b58cdd0-5220-4053-baf7-5dd9d15aa535")]
        [InlineData("5d819ee0-65b8-4afe-aaae-1849d439e217")]
        [InlineData("8db0c071-4a1f-4776-9154-518725ac78cb")]
        public void CanBeConvertedToValidString(string guid) =>
            Assert.Equal($"SAMPLE-{guid.Replace("-", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant()}",
                new SampleIdentity(Guid.ParseExact(guid, "D")).ToString());
    }
}
