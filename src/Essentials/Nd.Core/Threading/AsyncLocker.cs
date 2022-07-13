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
using System.Threading;
using System.Threading.Tasks;

namespace Nd.Core.Threading
{
    public interface IAsyncLock : IDisposable
    {
        void Release();
    }

    internal class ExclusiveAsyncLock : IAsyncLock
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly object _lock = new();
        private bool _released;

        public ExclusiveAsyncLock(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Release();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ExclusiveAsyncLock() => Dispose(false);

        public void Release()
        {
            lock (_lock)
            {
                if (!_released)
                {
                    _ = _semaphore.Release();
                    _released = true;
                }
            }
        }
    }

    public interface IAsyncLocker : IDisposable
    {
        void Release();
        Task<IAsyncLock> WaitAsync(CancellationToken cancellation = default);
        IAsyncLock Wait(CancellationToken cancellation = default);
    }

    public sealed class ExclusiveAsyncLocker : IAsyncLocker
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly object _lock = new();
        private bool _disposed;
        private bool _released;

        private ExclusiveAsyncLocker(bool autoRelease)
        {
            _semaphore = new SemaphoreSlim(0, 1);

            if (autoRelease)
            {
                Release();
            }
        }

        public static IAsyncLocker Create(bool autoRelease = true) =>
            new ExclusiveAsyncLocker(autoRelease);

        public void Release()
        {
            lock (_lock)
            {
                if (!_released && !_disposed)
                {
                    _ = _semaphore.Release();
                    _released = true;
                }
            }
        }

        public async Task<IAsyncLock> WaitAsync(CancellationToken cancellation = default)
        {
            await _semaphore.WaitAsync(cancellation).ConfigureAwait(false);
            return new ExclusiveAsyncLock(_semaphore);
        }

        public IAsyncLock Wait(CancellationToken cancellation = default)
        {
            _semaphore.Wait(cancellation);
            return new ExclusiveAsyncLock(_semaphore);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_lock)
                {
                    if (_disposed)
                    {
                        _disposed = true;
                        _semaphore.Dispose();
                    }
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ExclusiveAsyncLocker() => Dispose(false);
    }
}
