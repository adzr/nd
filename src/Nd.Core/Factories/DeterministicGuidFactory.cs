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

using System.Security.Cryptography;
using System.Text;

namespace Nd.Core.Factories {
    /// <summary>
    /// A factory that creates a name-based UUID using the algorithm from RFC 4122 §4.3.
    /// </summary>
    public sealed class DeterministicGuidFactory : IGuidFactory {
        private const int GuidVersion = 5;

        private readonly Guid _namespaceId;
        private readonly byte[] _nameBytes;

        private DeterministicGuidFactory(Guid namespaceId, byte[] nameBytes) {
            _namespaceId = namespaceId;
            _nameBytes = nameBytes;
        }

        public static IGuidFactory Instance(Guid namespaceId, byte[] nameBytes) => new DeterministicGuidFactory(namespaceId, nameBytes);

        public static IGuidFactory Instance(Guid namespaceId, string name) => new DeterministicGuidFactory(namespaceId, Encoding.UTF8.GetBytes(name ?? throw new ArgumentNullException(nameof(name))));

        /// <summary>
        /// Creates a name-based UUID using the algorithm from RFC 4122 §4.3.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public Guid Create() {
            if (_namespaceId == default) {
                throw new ArgumentNullException(nameof(_namespaceId));
            }

            if (_nameBytes is null || _nameBytes.Length == 0) {
                throw new ArgumentNullException(nameof(_nameBytes));
            }

            // Convert the namespace UUID to network order (step 3)
            var namespaceBytes = _namespaceId.ToByteArray();

            SwapByteOrder(namespaceBytes);

            // Comput the hash of the name space ID concatenated with the name (step 4)
            byte[] hash;

            using (var algorithm = SHA512.Create()) {
                var combinedBytes = new byte[namespaceBytes.Length + _nameBytes.Length];

                Buffer.BlockCopy(namespaceBytes, 0, combinedBytes, 0, namespaceBytes.Length);
                Buffer.BlockCopy(_nameBytes, 0, combinedBytes, namespaceBytes.Length, _nameBytes.Length);

                hash = algorithm.ComputeHash(combinedBytes);
            }

            // Most bytes from the hash are copied straight to the bytes of the new
            // GUID (steps 5-7, 9, 11-12)
            var result = new byte[16];

            Array.Copy(hash, 0, result, 0, 16);

            // Set the four most significant bits (bits 12 through 15) of the time_hi_and_version
            // field to the appropriate 4-bit version number from Section 4.1.3 (step 8)
            result[6] = (byte)((result[6] & 0x0F) | (GuidVersion << 0x04));

            // Set the two most significant bits (bits 6 and 7) of the clock_seq_hi_and_reserved
            // to zero and one, respectively (step 10)
            result[8] = (byte)((result[8] & 0x3F) | 0x80);

            // Convert the resulting UUID to local byte order (step 13)
            SwapByteOrder(result);

            return new Guid(result);
        }

        // Converts a GUID (expressed as a byte array) to/from network order (MSB-first).
        private static void SwapByteOrder(byte[] guid) {
            SwapBytes(guid, 0, 3);
            SwapBytes(guid, 1, 2);
            SwapBytes(guid, 4, 5);
            SwapBytes(guid, 6, 7);
        }

        private static void SwapBytes(byte[] guid, int left, int right) {
            guid[left] ^= guid[right];
            guid[right] ^= guid[left];
            guid[left] ^= guid[right];
        }
    }
}
