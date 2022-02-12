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
using System.Collections.Generic;
using System.Linq;
using Nd.Core.Factories;
using Xunit;

namespace Nd.Core.Tests.Factories {
    public class CombGuidFactoryTests {
        private const int GuidCount = 1000;

        [Fact]
        public void CanGenerateUniqueGuid() =>
            Assert.Equal(GuidCount, (from _ in Enumerable.Range(0, GuidCount) select CombGuidFactory.Instance.Create()).Distinct().Count());

        [Fact]
        public void CanGenerateSequencialGuid() {
            var guids = (from _ in Enumerable.Range(0, GuidCount) select CombGuidFactory.Instance.Create()).ToArray();
            Assert.True(guids.SequenceEqual(guids.OrderBy(g => g, new CombGuidComparer())));
        }
    }

    internal class CombGuidComparer : IComparer<Guid> {
        public int Compare(Guid x, Guid y) {
            var bytesX = GetGuidV1OrderedBytes(x);
            var bytesY = GetGuidV1OrderedBytes(y);

            for (var i = 0; i < 16; i++) {
                var result = bytesX[i].CompareTo(bytesY[i]);

                if (result != 0) {
                    return result;
                }
            }

            return 0;
        }

        public static byte[] GetGuidV1OrderedBytes(Guid guid) {
            var b = guid.ToByteArray();

            return new[] {
                // The 48-bit node id.
                b[10], b[11], b[12], b[13], b[14], b[15],
                // 1 to 3-bit "variant" in the most significant bits, followed by the 13 to 15-bit clock sequence.
                b[8], b[9],
                // 4-bit "version" in the most significant bits, followed by the high 12 bits of the time.
                b[7], b[6],
                // Integer giving the middle 16 bits of the time.
                b[5], b[4],
                // Integer giving the low 32 bits of the time.
                b[3], b[2], b[1], b[0]
            };
        }
    }
}
