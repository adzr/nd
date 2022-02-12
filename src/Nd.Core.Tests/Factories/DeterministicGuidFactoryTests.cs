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
using System.Linq;
using Nd.Core.Factories;
using Xunit;

namespace Nd.Core.Tests.Factories {
    public class DeterministicGuidFactoryTests {
        private const int GuidCount = 1000;

        [Fact]
        public void CanGenerateUniqueGuidUnderDifferentNamespaces() =>
            Assert.Equal(GuidCount, (from _ in Enumerable.Range(0, GuidCount)
                                     select DeterministicGuidFactory
                                     .Instance(Guid.NewGuid(), "FixedName").Create()).Distinct().Count());

        [Fact]
        public void CanGenerateUniqueGuidUnderDifferentNames() =>
            Assert.Equal(GuidCount, (from _ in Enumerable.Range(0, GuidCount)
                                     select DeterministicGuidFactory
                                     .Instance(
                                         Guid.Parse("23d7c9a8-b27c-470a-81e6-3bea12a9013a"),
                                         Guid.NewGuid().ToString())
                                     .Create()).Distinct().Count());

        [Fact]
        public void CanGenerateUniqueGuidUnderDifferentNamesAndNamespaces() =>
            Assert.Equal(GuidCount, (from _ in Enumerable.Range(0, GuidCount)
                                     select DeterministicGuidFactory
                                     .Instance(
                                         Guid.NewGuid(),
                                         Guid.NewGuid().ToString())
                                     .Create()).Distinct().Count());

        [Fact]
        public void CanGenerateDeterministicGuidUnderSameNameAndNamespace() =>
            Assert.Single((from _ in Enumerable.Range(0, GuidCount)
                           select DeterministicGuidFactory
                           .Instance(
                               Guid.Parse("23d7c9a8-b27c-470a-81e6-3bea12a9013a"),
                               "FixedName")
                           .Create()).Distinct());

        [Theory]
        [InlineData(null, null)]
        [InlineData(null, "")]
        [InlineData("00000000-0000-0000-0000-000000000000", null)]
        [InlineData("00000000-0000-0000-0000-000000000000", "")]
        [InlineData("23d7c9a8-b27c-470a-81e6-3bea12a9013a", null)]
        [InlineData("23d7c9a8-b27c-470a-81e6-3bea12a9013a", "")]
        public void FailsOnInvalidInput(string @namespace, string name) =>
            Assert.Throws<ArgumentNullException>(() => DeterministicGuidFactory
            .Instance(Guid.Parse(@namespace), name).Create());
    }
}
