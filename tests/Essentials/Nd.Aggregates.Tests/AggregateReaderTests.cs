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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Nd.Aggregates.Events;
using Nd.Aggregates.Identities;
using Nd.Aggregates.Persistence;
using Nd.Core.Extensions;
using Nd.Core.Factories;
using Nd.Core.Types;
using Nd.Core.Types.Versions;
using Nd.Identities;
using Xunit;
using Xunit.Categories;

namespace Nd.Aggregates.Tests
{
    [UnitTest]
    public class AggregateReaderTests
    {
        #region Test types definitions

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        public sealed record class TestIdentity : GuidAggregateIdentity
        {
            public TestIdentity(Guid value) : base(value) { }

            public TestIdentity(IGuidFactory factory) : base(factory) { }
        }

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        public sealed record class TestAggregateState : AggregateState<TestAggregateState>,
            ICanHandleAggregateEvent<TestEventA>,
            ICanHandleAggregateEvent<TestEventB>,
            ICanHandleAggregateEvent<TestEventC2>,
            ICanHandleAggregateEvent<TestEventCount>
        {
            private readonly ConcurrentQueue<IAggregateEvent<TestAggregateState>> _events = new();

            public IReadOnlyCollection<IAggregateEvent<TestAggregateState>> Events => _events;

            public uint Counter { get; private set; }

            public override TestAggregateState State => this;

            public void Handle(TestEventA aggregateEvent) => _events.Enqueue(aggregateEvent);

            public void Handle(TestEventB aggregateEvent) => _events.Enqueue(aggregateEvent);

            public void Handle(TestEventC2 aggregateEvent) => _events.Enqueue(aggregateEvent);


            [SuppressMessage("Naming", "CA1725:Parameter names should match base declaration", Justification = "Useless parameter.")]
            public void Handle(TestEventCount _) => Counter++;

            public IAggregateEvent<TestAggregateState>? Yield() => _events.TryDequeue(out var e) ? e : default;
        }

        internal interface IHasValue
        {
            string Value { get; }
        }

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        [Event(nameof(TestEventA), 1)]
        public sealed record class TestEventA(string Value) : AggregateEvent<TestAggregateState>, IHasValue;

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        [Event(nameof(TestEventB), 1)]
        public sealed record class TestEventB : AggregateEvent<TestAggregateState>;

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        [Event("TestEventC", 1)]
        public sealed record class TestEventC1(string Value) : AggregateEvent<TestAggregateState>, IHasValue, IVersionedType
        {
            Task<IVersionedType?> IVersionedType.UpgradeAsync(CancellationToken _) => Task.FromResult<IVersionedType?>(new TestEventC2(Value));
        }

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        [Event("TestEventC", 2)]
        public sealed record class TestEventC2(string Value) : AggregateEvent<TestAggregateState>, IHasValue;

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        public sealed record class TestEventCount : AggregateEvent<TestAggregateState>;

        internal class TestAggregateRoot : AggregateRoot<TestIdentity, TestAggregateState>
        {
            public TestAggregateRoot(TestIdentity identity, AggregateStateFactoryFunc<TestAggregateState> stateFactory, uint version) : base(identity, stateFactory, version) { }

            public void LetThisHappen<T>(IAggregateEventMetadata? meta = default, bool failOnDuplicates = true, Func<DateTimeOffset>? currentTimestampProvider = default)
                where T : AggregateEvent<TestAggregateState> => Emit(Activator.CreateInstance<T>(), meta, failOnDuplicates, currentTimestampProvider);

            public void LetThisHappen<T>(T @event, IAggregateEventMetadata? meta = default, bool failOnDuplicates = true, Func<DateTimeOffset>? currentTimestampProvider = default)
                where T : AggregateEvent<TestAggregateState> => Emit(@event, meta, failOnDuplicates, currentTimestampProvider);

            public void Increment() => Emit(new TestEventCount(),
                new AggregateEventMetadata(
                    new IdempotencyIdentity(RandomGuidFactory.Instance.Create()),
                    new CorrelationIdentity(RandomGuidFactory.Instance.Create())));
        }

        internal class TestAggregateReader : AggregateReader<TestIdentity, TestAggregateState>
        {
            public TestAggregateReader(
                AggregateStateFactoryFunc<TestAggregateState> stateFactory,
                AggregateFactoryFunc<TestIdentity, TestAggregateState, IAggregateRoot<TestIdentity, TestAggregateState>> aggregateFactory,
                IAggregateEventReader<TestIdentity> eventReader) : base(stateFactory, aggregateFactory, eventReader)
            {
            }
        }

        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        public record class TestAggregateCommittedEvent : ICommittedEvent<TestIdentity>
        {
            public TestAggregateCommittedEvent(IAggregateEvent aggregateEvent, IAggregateEventMetadata<TestIdentity> metadata)
            {
                AggregateEvent = aggregateEvent;
                Metadata = metadata;
            }

            public IAggregateEvent AggregateEvent { get; }

            public IAggregateEventMetadata<TestIdentity> Metadata { get; }
        }

        #endregion

        [Fact]
        public void CanReadEvents()
        {
            var expectedCorrelationId = new CorrelationIdentity(Guid.NewGuid());
            var expectedIdentity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var expectedAggregateName = typeof(TestAggregateRoot).ResolveName();

            var expectedEventA = new TestEventA(RandomGuidFactory.Instance.Create().ToString());
            var expectedEventB = new TestEventB();

            var eventReader = A.Fake<IAggregateEventReader<TestIdentity>>();

            IAggregateReader<TestIdentity> reader = new TestAggregateReader(
                () => new TestAggregateState(),
                (identity, stateFactory, version) => new TestAggregateRoot(identity, stateFactory, version),
                eventReader);

            ExpectEvents(expectedIdentity, expectedAggregateName, new AggregateEvent<TestAggregateState>[] { expectedEventA, expectedEventB }, eventReader);

            var aggregate = reader.ReadAsync<TestAggregateRoot>(expectedIdentity, expectedCorrelationId, cancellation: CancellationToken.None).GetAwaiter().GetResult();

            Assert.NotNull(aggregate);

            AssertEvent(expectedEventA, aggregate!.State.Yield());
            AssertEvent(expectedEventB, aggregate!.State.Yield());
        }

        [Fact]
        public void CanUpgradeEvents()
        {
            var expectedCorrelationId = new CorrelationIdentity(Guid.NewGuid());
            var expectedIdentity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var expectedAggregateName = typeof(TestAggregateRoot).ResolveName();

            var expectedEventA = new TestEventC1(RandomGuidFactory.Instance.Create().ToString());
            var expectedEventB = new TestEventC2(RandomGuidFactory.Instance.Create().ToString());

            var eventReader = A.Fake<IAggregateEventReader<TestIdentity>>();

            IAggregateReader<TestIdentity> reader = new TestAggregateReader(
                () => new TestAggregateState(),
                (identity, stateFactory, version) => new TestAggregateRoot(identity, stateFactory, version),
                eventReader);

            ExpectEvents(expectedIdentity, expectedAggregateName, new AggregateEvent<TestAggregateState>[] { expectedEventA, expectedEventB }, eventReader);

            var aggregate = reader.ReadAsync<TestAggregateRoot>(expectedIdentity, expectedCorrelationId, cancellation: CancellationToken.None).GetAwaiter().GetResult();

            Assert.NotNull(aggregate);

            AssertEvent(new TestEventC2(expectedEventA.Value), aggregate!.State.Yield());
            AssertEvent(expectedEventB, aggregate!.State.Yield());
        }

        private static void ExpectEvents(TestIdentity expectedIdentity, string expectedAggregateName, IEnumerable<AggregateEvent<TestAggregateState>> events, IAggregateEventReader<TestIdentity> eventReader)
        {
            _ = A.CallTo(() => eventReader.ReadAsync(A<TestIdentity>._, A<ICorrelationIdentity>._, A<uint>._, A<CancellationToken>._))
                .ReturnsLazily((fakeObjCall) => YieldEvents(expectedIdentity, expectedAggregateName, events));
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private static async IAsyncEnumerable<TestAggregateCommittedEvent> YieldEvents(TestIdentity expectedIdentity, string expectedAggregateName, IEnumerable<AggregateEvent<TestAggregateState>> events)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            foreach (var @event in events
                    .Select(e =>
                    {
                        var eventType = TypeDefinitions.NamesAndVersionsTypes.TryGetValue((e.TypeName, e.TypeVersion), out var type) ?
                            type : throw new InvalidOperationException($"Definition of type name and version: ({e.TypeName}, {e.TypeVersion}) has no Type defined");

                        return new TestAggregateCommittedEvent(e,
                            new AggregateEventMetadata<TestIdentity>(
                                new IdempotencyIdentity(RandomGuidFactory.Instance.Create()),
                                new CorrelationIdentity(RandomGuidFactory.Instance.Create()),
                                new AggregateEventIdentity(RandomGuidFactory.Instance.Create()),
                                e.TypeName,
                                e.TypeVersion,
                                expectedIdentity,
                                expectedAggregateName,
                                1u,
                                DateTimeOffset.UtcNow));
                    })
                    .OrderBy(e => e.Metadata.AggregateVersion)
                    .ToList())
            {
                yield return @event;
            }
        }

        private static void AssertEvent(AggregateEvent<TestAggregateState>? expected, IAggregateEvent<TestAggregateState>? actual)
        {
            if (expected is null && actual is null)
            {
                return;
            }

            Assert.NotNull(expected);
            Assert.NotNull(actual);

            Assert.Equal(expected!.TypeName, actual!.TypeName);
            Assert.Equal(expected!.TypeVersion, actual!.TypeVersion);

            if (expected is IHasValue v)
            {
                Assert.Equal(v.Value, (actual as IHasValue)?.Value);
            }
        }
    }
}
