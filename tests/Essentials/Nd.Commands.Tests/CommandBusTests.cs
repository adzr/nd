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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Nd.Commands.Exceptions;
using Nd.Commands.Persistence;
using Nd.Commands.Results;
using Nd.Fakes;
using Nd.Identities;
using Nd.Identities.Common;
using Xunit;
using Xunit.Categories;

namespace Nd.Commands.Tests
{
    [UnitTest]
    public class CommandBusTests
    {
        #region Test types definitions

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked.")]
        [ExecutionResult("CommandA", 1)]
        public record class CommandAResult(
            CommandA Command,
            Exception? Exception = default,
            DateTimeOffset? Acknowledged = default,
            string? Comment = default) :
            GenericExecutionResult<CommandA>(Command, Exception, Acknowledged);

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked.")]
        [ExecutionResult("CommandB", 1)]
        public record class CommandBResult(
            CommandB Command,
            Exception? Exception = default,
            DateTimeOffset? Acknowledged = default,
            uint Attempts = default) :
            GenericExecutionResult<CommandB>(Command, Exception, Acknowledged);

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked.")]
        [ExecutionResult("CommandC", 1)]
        public record class CommandCResult(
            CommandC Command,
            Exception? Exception = default,
            DateTimeOffset? Acknowledged = default,
            string? Comment = default) :
            GenericExecutionResult<CommandC>(Command, Exception, Acknowledged);

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked.")]
        [Command("CommandA", 1)]
        public record class CommandA(
                IIdempotencyIdentity IdempotencyIdentity,
                ICorrelationIdentity CorrelationIdentity,
                DateTimeOffset? Acknowledged = default) : Command<CommandAResult>(
                    IdempotencyIdentity, CorrelationIdentity, Acknowledged);

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked.")]
        [Command("CommandB", 1)]
        public record class CommandB(
            IIdempotencyIdentity IdempotencyIdentity,
            ICorrelationIdentity CorrelationIdentity,
            DateTimeOffset? Acknowledged = default) : Command<CommandBResult>(
                IdempotencyIdentity, CorrelationIdentity, Acknowledged);

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked.")]
        [Command("CommandC", 1)]
        public record class CommandC(
                IIdempotencyIdentity IdempotencyIdentity,
                ICorrelationIdentity CorrelationIdentity,
                DateTimeOffset? Acknowledged = default) : Command<CommandCResult>(
                    IdempotencyIdentity, CorrelationIdentity, Acknowledged);

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be read by type definitions.")]
        public class TestCommandHandler :
            ICommandHandler<CommandA, CommandAResult>,
            ICommandHandler<CommandB, CommandBResult>
        {
            public Task<CommandAResult> ExecuteAsync(CommandA command, CancellationToken cancellation = default) =>
                Task.FromResult(new CommandAResult(command));

            public Task<CommandBResult> ExecuteAsync(CommandB command, CancellationToken cancellation = default) =>
                Task.FromResult(new CommandBResult(command));
        }

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be read by type definitions.")]
        public class NullCommandHandler :
            ICommandHandler<CommandC, CommandCResult>
        {
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
            public Task<CommandCResult> ExecuteAsync(CommandC command, CancellationToken cancellation = default) => Task.FromResult(default(CommandCResult));
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
        }

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be read by type definitions.")]
        public class ExceptionCommandHandler :
            ICommandHandler<CommandC, CommandCResult>
        {
            public Task<CommandCResult> ExecuteAsync(CommandC command, CancellationToken cancellation = default) => throw new NotImplementedException();
        }

        #endregion

        [Fact]
        public void CanRouteCommandsCorrectly()
        {
            var writer = A.Fake<ICommandWriter>();

            var handler = new TestCommandHandler();

            var commandA = new CommandA(new IdempotencyIdentity(Guid.NewGuid()), new CorrelationIdentity(Guid.NewGuid()));
            var commandB = new CommandB(new IdempotencyIdentity(Guid.NewGuid()), new CorrelationIdentity(Guid.NewGuid()));

            var bus = new CommandBus(new ICommandHandler[] { handler }, writer, default, default);

            var resultA = bus.ExecuteAsync<CommandA, CommandAResult>(commandA, default).GetAwaiter().GetResult();
            var resultB = bus.ExecuteAsync<CommandB, CommandBResult>(commandB, default).GetAwaiter().GetResult();

            Assert.NotNull(resultA);
            Assert.NotNull(resultB);
            Assert.Equal(commandA, resultA.Command);
            Assert.Equal(commandB, resultB.Command);
        }

        [Fact]
        public void CanStoreCommandsCorrectly()
        {
            var writer = A.Fake<ICommandWriter>();
            var storedCommands = new Queue<IExecutionResult>();

            var commandWriteCall = A.CallTo(() => writer.WriteAsync(A<CommandAResult>._, A<CancellationToken>._))
                .Invokes((CommandAResult result, CancellationToken _) => storedCommands.Enqueue(result));

            var handler = new TestCommandHandler();

            var command = new CommandA(new IdempotencyIdentity(Guid.NewGuid()), new CorrelationIdentity(Guid.NewGuid()));

            var bus = new CommandBus(new ICommandHandler[] { handler }, writer, default, default);

            var result = bus.ExecuteAsync<CommandA, CommandAResult>(command, default).GetAwaiter().GetResult();

            Assert.NotNull(result);
            Assert.Equal(command, result.Command);
            Assert.NotNull(command.Acknowledged);
            _ = commandWriteCall.MustHaveHappenedOnceExactly();
            Assert.Equal(result, Assert.Single(storedCommands));
            Assert.NotNull(result.Acknowledged);
        }

        [Fact]
        public void CanLogOnSuccessCorrectly()
        {
            var (logger, context) = FakeLogger.Create<CommandBus>();

            var writer = A.Fake<ICommandWriter>();

            var handler = new TestCommandHandler();

            var command = new CommandA(new IdempotencyIdentity(Guid.NewGuid()), new CorrelationIdentity(Guid.NewGuid()));

            var bus = new CommandBus(new ICommandHandler[] { handler }, writer, logger, default);

            var result = bus.ExecuteAsync<CommandA, CommandAResult>(command, default).GetAwaiter().GetResult();

            Assert.NotNull(result);
            Assert.Equal(command, result.Command);

            Assert.Empty(context.Exceptions);
            Assert.Empty(context.GetCriticals());
            Assert.Empty(context.GetErrors());
            Assert.Empty(context.GetWarnings());
            Assert.Empty(context.GetInfos());
            Assert.Empty(context.GetDebugs());
            var traces = context.GetTraces();
            Assert.Equal(3, traces.Length);
            Assert.True(traces.All(t =>
                t.State.ContainsState(LoggingScopeConstants.CorrelationKey, command.CorrelationIdentity.Value) &&
                t.State.ContainsState(Common.LoggingScopeConstants.CommandId, command.IdempotencyIdentity.Value)));
        }

        [Fact]
        public void CanThrowOnUnsupportedCommandExecution()
        {
            var handler = new TestCommandHandler();

            var bus = new CommandBus(new ICommandHandler[] { handler }, A.Fake<ICommandWriter>(), default, default);

            var command = new CommandC(new IdempotencyIdentity(Guid.NewGuid()), new CorrelationIdentity(Guid.NewGuid()));

            var exception = Assert.Throws<CommandNotRegisteredException>(() => bus.ExecuteAsync<CommandC, CommandCResult>(command, default).GetAwaiter().GetResult());

            Assert.Equal(command, exception.Command);
        }

        [Fact]
        public void CanThrowOnNullResult()
        {
            var handler = new NullCommandHandler();

            var bus = new CommandBus(new ICommandHandler[] { handler }, A.Fake<ICommandWriter>(), default, default);

            var command = new CommandC(new IdempotencyIdentity(Guid.NewGuid()), new CorrelationIdentity(Guid.NewGuid()));

            var exception = Assert.Throws<CommandExecutionException>(() => bus.ExecuteAsync<CommandC, CommandCResult>(command, default).GetAwaiter().GetResult());

            Assert.Equal(command, exception.Command);
            Assert.NotNull(command.Acknowledged);
        }

        [Fact]
        public void CanThrowOnExceptionResult()
        {
            var handler = new ExceptionCommandHandler();

            var bus = new CommandBus(new ICommandHandler[] { handler }, A.Fake<ICommandWriter>(), default, default);

            var command = new CommandC(new IdempotencyIdentity(Guid.NewGuid()), new CorrelationIdentity(Guid.NewGuid()));

            var exception = Assert.Throws<CommandExecutionException>(() => bus.ExecuteAsync<CommandC, CommandCResult>(command, default).GetAwaiter().GetResult());

            Assert.Equal(command, exception.Command);
            Assert.NotNull(command.Acknowledged);
        }

        [Fact]
        public void CanThrowOnFailedStorage()
        {
            var handler = new TestCommandHandler();

            var writer = A.Fake<ICommandWriter>();

            _ = A.CallTo(() => writer.WriteAsync(A<CommandAResult>._, A<CancellationToken>._)).Throws<NotImplementedException>();

            var bus = new CommandBus(new ICommandHandler[] { handler }, writer, default, default);

            var command = new CommandA(new IdempotencyIdentity(Guid.NewGuid()), new CorrelationIdentity(Guid.NewGuid()));

            var exception = Assert.Throws<CommandPersistenceException>(() => bus.ExecuteAsync<CommandA, CommandAResult>(command, default).GetAwaiter().GetResult());

            Assert.Equal(command, exception.Command);
            Assert.NotNull(exception.Result);
            Assert.NotNull(command.Acknowledged);
        }
    }
}
