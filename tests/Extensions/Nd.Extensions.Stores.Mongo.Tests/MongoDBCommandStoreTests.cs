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
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using Nd.Aggregates;
using Nd.Aggregates.Events;
using Nd.Aggregates.Identities;
using Nd.Aggregates.Persistence;
using Nd.Commands;
using Nd.Commands.Results;
using Nd.Containers;
using Nd.Core.Factories;
using Nd.Extensions.Stores.Mongo.Commands;
using Nd.Identities;
using Xunit;
using Xunit.Categories;
using static Nd.Extensions.Stores.Mongo.Tests.MongoDBAggregateEventStoreTests;

namespace Nd.Extensions.Stores.Mongo.Tests
{
    [IntegrationTest]
    public class MongoDBCommandStoreTests : IDisposable
    {
        #region Test types definitions

        [Identity("MongoDBCommandTest")]
        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        public sealed record class TestIdentity : GuidAggregateIdentity
        {
            public TestIdentity(Guid value) : base(value) { }

            public TestIdentity(IGuidFactory factory) : base(factory) { }

            public override IAggregateRootFactory CreateAggregateFactory() => new TestAggregateRootFactory().ConfigureIdentity(this);
        }

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be serialized.")]
        [ExecutionResult("CommandA", 1)]
        public record class CommandAResult(
            CommandA Command,
            Exception? Exception = default,
            DateTimeOffset? Acknowledged = default,
            string? Comment = default) :
            GenericExecutionResult<CommandA>(Command, Exception, Acknowledged);

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be serialized.")]
        [ExecutionResult("CommandB", 1)]
        public record class CommandBResult(
            CommandB Command,
            Exception? Exception = default,
            DateTimeOffset? Acknowledged = default,
            uint Attempts = default) :
            GenericExecutionResult<CommandB>(Command, Exception, Acknowledged);

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be serialized.")]
        [Command("CommandA", 1)]
        public record class CommandA(
            IIdempotencyIdentity IdempotencyIdentity,
            ICorrelationIdentity CorrelationIdentity,
            TestIdentity AggregateIdentity,
            DateTimeOffset? Acknowledged = default) : AggregateCommand<TestIdentity, CommandAResult>(
                IdempotencyIdentity, CorrelationIdentity, AggregateIdentity, Acknowledged);

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be serialized.")]
        [Command("CommandB", 1)]
        public record class CommandB(
            IIdempotencyIdentity IdempotencyIdentity,
            ICorrelationIdentity CorrelationIdentity,
            TestIdentity AggregateIdentity,
            DateTimeOffset? Acknowledged = default) : AggregateCommand<TestIdentity, CommandBResult>(
                IdempotencyIdentity, CorrelationIdentity, AggregateIdentity, Acknowledged);

        internal class TestAggregateRootFactory : AggregateRootFactory<TestIdentity, TestAggregateState>
        {
            public override IAggregateRoot<TestIdentity, TestAggregateState> Build(ISession session) => new TestAggregateRoot(Identity!, State, session);

            protected override IAggregateState<TestAggregateState> CreateState() => new TestAggregateState();
        }

        internal class TestAggregateRoot : AggregateRoot<TestIdentity, TestAggregateState>
        {
            public TestAggregateRoot(TestIdentity identity, IAggregateState<TestAggregateState> state, ISession session) : base(identity, state, session) { }
        }

        #endregion

        private const string DatabaseName = "test_db";

        private MongoContainer _mongoContainer;
        private readonly MongoClient _mongoClient;
        private readonly MongoDBCommandWriter _mongoWriter;
        private readonly MongoDBCommandReader _mongoReader;

        public MongoDBCommandStoreTests()
        {
            using var tokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(5));

            LocalPortManager.AcquireRandomPortAsync(async (port, cancellation) =>
            {
                _mongoContainer = new MongoContainer(port: $"{port}", password: Helpers.GetRandomSecureHex(16));
                await _mongoContainer.StartAsync(cancellation).ConfigureAwait(false);
            }, cancellation: tokenSource.Token).GetAwaiter().GetResult();

            var mongoSettings = MongoClientSettings.FromConnectionString(_mongoContainer!.ConnectionString);

            _mongoClient = new MongoClient(mongoSettings);

            _mongoWriter = new MongoDBCommandWriter(_mongoClient, DatabaseName, default, default);

            _mongoReader = new MongoDBCommandReader(_mongoClient, DatabaseName, default, default);
        }

        [Fact]
        public async Task CanStoreCommandsToMongoAsync()
        {
            var correlationId = new CorrelationIdentity(Guid.NewGuid());
            var identity = new TestIdentity(Guid.NewGuid());
            var timestamp = DateTime.UtcNow;

            var expected = new IExecutionResult[]
            {
                new CommandAResult(
                    new CommandA(
                        new IdempotencyIdentity(Guid.NewGuid()),
                        correlationId,
                        identity, timestamp),
                    default,
                    timestamp,
                    identity.Value.ToString()),
                new CommandBResult(
                    new CommandB(
                        new IdempotencyIdentity(Guid.NewGuid()),
                        correlationId,
                        identity, timestamp),
                    default,
                    timestamp,
                    1)
            };

            foreach (var result in expected)
            {
                await _mongoWriter.WriteAsync(result, default)
                .ConfigureAwait(false);
            }

            for (var i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], await _mongoReader
                    .ReadAsync<IExecutionResult>(
                    expected[i]?.Command?.IdempotencyIdentity.Value ?? Guid.Empty,
                    correlationId,
                    default).ConfigureAwait(false));
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MongoDBCommandStoreTests() => Dispose(false);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _mongoContainer.Dispose();
            }
        }
    }
}
