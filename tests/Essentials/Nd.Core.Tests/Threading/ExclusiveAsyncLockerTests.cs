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

using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Nd.Core.Threading;
using Xunit;
using Xunit.Categories;

namespace Nd.Core.Tests.Threading
{
    [UnitTest]
    public class ExclusiveAsyncLockerTests
    {
        private enum TaskStage
        {
            Proccessing,
            Completed
        }

        [Fact]
        public async Task CanAllowOneTaskAtTimeAsync()
        {
            const int TaskCount = 100;

            using var locker = ExclusiveAsyncLocker.Create(false);

            var stages = new ConcurrentQueue<(int, TaskStage)>();
            var tasks = Enumerable
                .Range(0, TaskCount)
                .Select(i => Task.Run(async () =>
                {
#pragma warning disable CA1849 // Call async methods when in an async method
                    using var @lock =
                        i % 2 == 0 ?
                        await locker.WaitAsync().ConfigureAwait(false) :
                        locker.Wait();
#pragma warning restore CA1849 // Call async methods when in an async method
                    stages.Enqueue((i, TaskStage.Proccessing));
                    stages.Enqueue((i, TaskStage.Completed));
                }))
                .ToList();

            locker.Release();

            await Task.WhenAll(tasks.ToArray()).ConfigureAwait(false);

            Assert.Equal(TaskCount * 2, stages.Count);

            Assert.True(stages
                .GroupBy(((int Index, TaskStage Stage) item) => item.Index)
                .All(g => g
                    .Select(((int Index, TaskStage Stage) item) => item.Stage)
                    .SequenceEqual(new[]
                    {
                        TaskStage.Proccessing,
                        TaskStage.Completed
                    })));

            for (var i = 0; i < TaskCount * 2; i++)
            {
                Assert.True(stages.TryDequeue(out (int Index, TaskStage Stage) stage));
                Assert.Equal(i % 2 == 0 ? TaskStage.Proccessing : TaskStage.Completed, stage.Stage);
            }
        }
    }
}
