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

using Microsoft.Extensions.Logging;

namespace Nd.Containers
{
    public static class LocalPortManager
    {
        private static readonly Action<ILogger, Exception?> s_failedToAcquireLock =
            LoggerMessage.Define(LogLevel.Trace, new EventId(999, "FailedAcquiringFileLock"), "Failed to acquire file lock");

        public static async Task AcquireRandomPortAsync(Func<int, CancellationToken, Task> acquirePort, string portLockPath = "nd/port.lock", ILogger? logger = default, CancellationToken cancellation = default)
        {
            using var @lock = await TryToAcquireFileLockAsync(portLockPath, logger, cancellation).ConfigureAwait(false);

            var port = Helpers.GetRandomOpenPort();

            if (acquirePort is null)
            {
                return;
            }

            await acquirePort(port, cancellation).ConfigureAwait(false);
        }

        private static async Task<FileStream> TryToAcquireFileLockAsync(string lockPath, ILogger? logger = default, CancellationToken cancellation = default)
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                Directory.CreateDirectory(
                    Path.GetDirectoryName(
                        lockPath ??
                        throw new ArgumentNullException(nameof(lockPath))) ??
                        throw new ArgumentException($"Failed to create path {lockPath}")).Name);

            while (!cancellation.IsCancellationRequested)
            {
                try
                {
                    return File.Open(path, FileMode.OpenOrCreate, FileAccess.Read, FileShare.None);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    if (logger is not null)
                    {
                        s_failedToAcquireLock(logger, e);
                    }

                    await Task.Delay(100, cancellation).ConfigureAwait(false);
                }
            }

            throw new IOException($"Failed to acquire file lock on {lockPath}");
        }
    }
}
