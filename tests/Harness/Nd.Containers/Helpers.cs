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

using System.Net.NetworkInformation;
using System.Security.Cryptography;
using Nd.Containers.Exceptions;

namespace Nd.Containers
{
    public static class Helpers
    {
        public static string GetRandomSecureString(int lengthInBytes = 32)
        {
            using var generator = RandomNumberGenerator.Create();
            var bytes = new byte[lengthInBytes];
            generator.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        public static string GetOpenPort()
        {
            var portStartIndex = 10000;
            var portEndIndex = 60000;

            var properties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpEndPoints = properties.GetActiveTcpListeners();

            var usedPorts = tcpEndPoints.Select(p => p.Port).ToList();

            for (var port = portStartIndex; port < portEndIndex; port++)
            {
                if (!usedPorts.Contains(port))
                {
                    return $"{port}";
                }
            }

            throw new UnavailableTCPPortException("Failed to find free port");
        }
    }
}
