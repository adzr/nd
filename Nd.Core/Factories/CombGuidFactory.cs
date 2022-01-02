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

namespace Nd.Core.Factories
{
    public sealed class CombGuidFactory : IGuidFactory
    {
        private static int _counter;
        private static readonly IGuidFactory _instance = new CombGuidFactory();

        private static long GetTicks()
        {
            var i = Interlocked.Increment(ref _counter);
            return DateTimeOffset.UtcNow.Ticks + i;
        }

        private CombGuidFactory() { }

        public static IGuidFactory Instance => _instance;

        public Guid Create()
        {
            var (uid, binDate) = (Guid.NewGuid().ToByteArray(), BitConverter.GetBytes(GetTicks()));

            return new Guid(
                new[]
                    {
                        // Integer giving the low 32 bits of the time.
                        uid[0], uid[1], uid[2], uid[3],
                        // Integer giving the middle 16 bits of the time.
                        uid[4], uid[5],
                        // 4-bit "version" in the most significant bits, followed by the high 12 bits of the time.
                        uid[6], (byte)(0xC0 | (0x0F & uid[7])),
                        // 1 to 3-bit "variant" in the most significant bits, followed by the 13 to 15-bit clock sequence.
                        binDate[1], binDate[0],
                        // The 48-bit node id.
                        binDate[7], binDate[6], binDate[5], binDate[4], binDate[3], binDate[2]
                    });
        }
    }
}
