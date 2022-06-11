using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

namespace Nd.Aggregates.Tests
{
    public class AggregateReaderTests
    {
        #region Test types definitions

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        public sealed record class TestIdentity : GuidIdentity, IAggregateIdentity
        {
            public TestIdentity(Guid value) : base(value) { }

            public TestIdentity(IGuidFactory factory) : base(factory) { }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
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


            [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1725:Parameter names should match base declaration", Justification = "Useless parameter.")]
            public void Handle(TestEventCount _) => Counter++;

            public IAggregateEvent<TestAggregateState>? Yield() => _events.TryDequeue(out var e) ? e : default;
        }

        internal interface IHasValue
        {
            string Value { get; }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        [VersionedEvent(nameof(TestEventA), 1)]
        public sealed record class TestEventA(string Value) : AggregateEvent<TestAggregateState>, IHasValue;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        [VersionedEvent(nameof(TestEventB), 1)]
        public sealed record class TestEventB : AggregateEvent<TestAggregateState>;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        [VersionedEvent("TestEventC", 1)]
        public sealed record class TestEventC1(string Value) : AggregateEvent<TestAggregateState>, IHasValue, IVersionedType
        {
            Task<IVersionedType?> IVersionedType.UpgradeAsync(CancellationToken _) => Task.FromResult<IVersionedType?>(new TestEventC2(Value));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        [VersionedEvent("TestEventC", 2)]
        public sealed record class TestEventC2(string Value) : AggregateEvent<TestAggregateState>, IHasValue;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
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
                IAggregateEventReader<TestIdentity, TestAggregateState> eventReader) : base(stateFactory, aggregateFactory, eventReader)
            {
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Needs to be public to be faked by FakeItEasy.")]
        public record class TestAggregateCommittedEvent : ICommittedEvent<TestIdentity, TestAggregateState>
        {
            public TestAggregateCommittedEvent(IAggregateEvent<TestAggregateState> aggregateEvent, IAggregateEventMetadata<TestIdentity> metadata)
            {
                AggregateEvent = aggregateEvent;
                Metadata = metadata;
            }

            public IAggregateEvent<TestAggregateState> AggregateEvent { get; }

            public IAggregateEventMetadata<TestIdentity> Metadata { get; }
        }

        #endregion

        [Fact]
        public void CanReadEvents()
        {
            var expectedIdentity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var expectedAggregateName = typeof(TestAggregateRoot).GetName();

            var expectedEventA = new TestEventA(RandomGuidFactory.Instance.Create().ToString());
            var expectedEventB = new TestEventB();

            var eventReader = A.Fake<IAggregateEventReader<TestIdentity, TestAggregateState>>();

            IAggregateReader<TestIdentity, TestAggregateState> reader = new TestAggregateReader(
                () => new TestAggregateState(),
                (identity, stateFactory, version) => new TestAggregateRoot(identity, stateFactory, version),
                eventReader);

            ExpectEvents(expectedIdentity, expectedAggregateName, new AggregateEvent<TestAggregateState>[] { expectedEventA, expectedEventB }, eventReader);

            var aggregate = reader.ReadAsync<TestAggregateRoot>(expectedIdentity, CancellationToken.None).GetAwaiter().GetResult();

            Assert.NotNull(aggregate);

            AssertEvent(expectedEventA, aggregate!.State.Yield());
            AssertEvent(expectedEventB, aggregate!.State.Yield());
        }

        [Fact]
        public void CanUpgradeEvents()
        {
            var expectedIdentity = new TestIdentity(RandomGuidFactory.Instance.Create());
            var expectedAggregateName = typeof(TestAggregateRoot).GetName();

            var expectedEventA = new TestEventC1(RandomGuidFactory.Instance.Create().ToString());
            var expectedEventB = new TestEventC2(RandomGuidFactory.Instance.Create().ToString());

            var eventReader = A.Fake<IAggregateEventReader<TestIdentity, TestAggregateState>>();

            IAggregateReader<TestIdentity, TestAggregateState> reader = new TestAggregateReader(
                () => new TestAggregateState(),
                (identity, stateFactory, version) => new TestAggregateRoot(identity, stateFactory, version),
                eventReader);

            ExpectEvents(expectedIdentity, expectedAggregateName, new AggregateEvent<TestAggregateState>[] { expectedEventA, expectedEventB }, eventReader);

            var aggregate = reader.ReadAsync<TestAggregateRoot>(expectedIdentity, CancellationToken.None).GetAwaiter().GetResult();

            Assert.NotNull(aggregate);

            AssertEvent(new TestEventC2(expectedEventA.Value), aggregate!.State.Yield());
            AssertEvent(expectedEventB, aggregate!.State.Yield());
        }

        private static void ExpectEvents(TestIdentity expectedIdentity, string expectedAggregateName, IEnumerable<AggregateEvent<TestAggregateState>> events, IAggregateEventReader<TestIdentity, TestAggregateState> eventReader)
        {
            _ = A.CallTo(() => eventReader.ReadAsync<ICommittedEvent<TestIdentity, TestAggregateState>>(A<TestIdentity>._, A<uint>._, A<CancellationToken>._))
                .ReturnsLazily(() => events.Select(e =>
                {
                    var eventType = Definitions.NamesAndVersionsTypes.TryGetValue((e.TypeName, e.TypeVersion), out var type) ?
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
                }).ToList());
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
