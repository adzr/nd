using System.Collections.Concurrent;
using System.Collections.Immutable;
using FakeItEasy;
using Microsoft.Extensions.Logging;

namespace Nd.Fakes
{
    public static class LoggerExtensions
    {
        public static ILogger WithFakeIsEnabled(this ILogger logger, Func<LogLevel, bool> func)
        {
            _ = A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).ReturnsLazily((LogLevel level) => func(level));
            return logger;
        }

        public static ILogger<TLogger> WithFakeIsEnabled<TLogger>(this ILogger<TLogger> logger, Func<LogLevel, bool> func)
        {
            _ = A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).ReturnsLazily((LogLevel level) => func(level));
            return logger;
        }

        public static ILogger WithFakeBeginScope(this ILogger logger, Func<IEnumerable<KeyValuePair<string, object?>>, IDisposable> func)
        {
            _ = A.CallTo(() => logger.BeginScope(A<IEnumerable<KeyValuePair<string, object?>>>._))
                .ReturnsLazily((IEnumerable<KeyValuePair<string, object?>> state) => func(state));
            return logger;
        }

        public static ILogger<TLogger> WithFakeBeginScope<TLogger>(this ILogger<TLogger> logger, Func<IEnumerable<KeyValuePair<string, object?>>, IDisposable> func)
        {
            _ = A.CallTo(() => logger.BeginScope(A<IEnumerable<KeyValuePair<string, object?>>>._))
                .ReturnsLazily((IEnumerable<KeyValuePair<string, object?>> state) => func(state));
            return logger;
        }

        public static ILogger WithFakeBeginScope(this ILogger logger, FakeLoggingContext context) =>
            logger.WithFakeBeginScope((IEnumerable<KeyValuePair<string, object?>> state) =>
            {
                context.Scopes.Push(state);
                return new FakeScope<IEnumerable<KeyValuePair<string, object?>>>(context.Scopes);
            });

        public static ILogger<TLogger> WithFakeBeginScope<TLogger>(this ILogger<TLogger> logger, FakeLoggingContext context) =>
            logger.WithFakeBeginScope((IEnumerable<KeyValuePair<string, object?>> state) =>
            {
                context.Scopes.Push(state);
                return new FakeScope<IEnumerable<KeyValuePair<string, object?>>>(context.Scopes);
            });

        public static ILogger WithFakeLog(this ILogger logger,
            FakeLoggingContext context, Action<LogLevel, EventId, IEnumerable<KeyValuePair<string, object?>>, Exception?, FakeLoggingContext> action)
        {
            _ = A.CallTo(logger)
                .Where(call => call.Method.Name.Equals(nameof(logger.Log), StringComparison.Ordinal))
                .WithAnyArguments()
                .Invokes((obj) => action(
                    obj.GetArgument<LogLevel>("logLevel"),
                    obj.GetArgument<EventId>("eventId"),
                    obj.GetArgument<IEnumerable<KeyValuePair<string, object?>>>("state")!,
                    obj.GetArgument<Exception?>("exception"),
                    context));
            return logger;
        }

        public static ILogger<TLogger> WithFakeLog<TLogger>(this ILogger<TLogger> logger,
            FakeLoggingContext context, Action<LogLevel, EventId, IEnumerable<KeyValuePair<string, object?>>, Exception?, FakeLoggingContext> action)
        {
            _ = A.CallTo(logger)
                .Where(call => call.Method.Name.Equals(nameof(logger.Log), StringComparison.Ordinal))
                .WithAnyArguments()
                .Invokes((obj) => action(
                    obj.GetArgument<LogLevel>("logLevel"),
                    obj.GetArgument<EventId>("eventId"),
                    obj.GetArgument<IEnumerable<KeyValuePair<string, object?>>>("state")!,
                    obj.GetArgument<Exception?>("exception"),
                    context));
            return logger;
        }

        public static ILogger WithFakeLog(this ILogger logger, FakeLoggingContext context) =>
            logger.WithFakeLog(context, (logLevel, eventId, state, exception, context) => context.Logs.Add((
                logLevel,
                eventId,
                context.Scopes
                .ToImmutableList()
                .SelectMany(r => r)
                .GroupBy(r => r.Key, r => r.Value)
                .Select(g => new KeyValuePair<string, object?>(g.Key, g.FirstOrDefault()))
                .ToImmutableList(),
                exception)));

        public static ILogger<TLogger> WithFakeLog<TLogger>(this ILogger<TLogger> logger, FakeLoggingContext context) =>
            logger.WithFakeLog(context, (logLevel, eventId, state, exception, context) => context.Logs.Add((
                logLevel,
                eventId,
                context.Scopes
                .ToImmutableList()
                .SelectMany(r => r)
                .GroupBy(r => r.Key, r => r.Value)
                .Select(g => new KeyValuePair<string, object?>(g.Key, g.FirstOrDefault()))
                .ToImmutableList(),
                exception)));

        public static FakeLoggingContext WithFakeDefaults(this ILogger logger)
        {
            var context = new FakeLoggingContext();

            _ = logger
            .WithFakeIsEnabled(_ => true)
            .WithFakeBeginScope(context)
            .WithFakeLog(context);

            return context;
        }

        public static FakeLoggingContext WithFakeDefaults<TLogger>(this ILogger<TLogger> logger)
        {
            var context = new FakeLoggingContext();

            _ = logger
            .WithFakeIsEnabled(_ => true)
            .WithFakeBeginScope(context)
            .WithFakeLog(context);

            return context;
        }

        public static bool ContainsState(this IEnumerable<KeyValuePair<string, object?>> state, string key, object? value) =>
            state?.Any(s => string.Equals(s.Key, key, StringComparison.Ordinal) && Equals(s.Value, value)) ?? false;
    }

    internal class FakeScope<TState> : IDisposable
    {
        private readonly ConcurrentStack<TState> _scopes;

        public FakeScope(ConcurrentStack<TState> scopes)
        {
            _scopes = scopes;
        }

        public void Dispose() => _scopes.TryPop(out var _);
    }

    public class FakeLoggingContext
    {
        public ConcurrentStack<IEnumerable<KeyValuePair<string, object?>>> Scopes { get; } = new();

        public ConcurrentBag<(
            LogLevel Level,
            EventId EventId,
            IEnumerable<KeyValuePair<string, object?>> State,
            Exception? Exception)> Logs
        { get; } = new();

        public (LogLevel Level, EventId EventId, IEnumerable<KeyValuePair<string, object?>> State, Exception? Exception)[] OfLevel(LogLevel level) =>
            Logs.Where(l => l.Level.Equals(level)).ToArray();

        public (LogLevel Level, EventId EventId, IEnumerable<KeyValuePair<string, object?>> State, Exception? Exception)[] GetTraces() =>
            OfLevel(LogLevel.Trace);

        public (LogLevel Level, EventId EventId, IEnumerable<KeyValuePair<string, object?>> State, Exception? Exception)[] GetDebugs() =>
            OfLevel(LogLevel.Debug);

        public (LogLevel Level, EventId EventId, IEnumerable<KeyValuePair<string, object?>> State, Exception? Exception)[] GetInfos() =>
            OfLevel(LogLevel.Information);

        public (LogLevel Level, EventId EventId, IEnumerable<KeyValuePair<string, object?>> State, Exception? Exception)[] GetWarnings() =>
            OfLevel(LogLevel.Warning);

        public (LogLevel Level, EventId EventId, IEnumerable<KeyValuePair<string, object?>> State, Exception? Exception)[] GetErrors() =>
            OfLevel(LogLevel.Error);

        public (LogLevel Level, EventId EventId, IEnumerable<KeyValuePair<string, object?>> State, Exception? Exception)[] GetCriticals() =>
            OfLevel(LogLevel.Critical);

        public IEnumerable<Exception> Exceptions => Logs.Where(l => l.Exception is not null).Select(l => l.Exception!).ToArray();
    }

    public static class FakeLogger
    {
        public static (ILogger<TLogger> Logger, FakeLoggingContext Context) Create<TLogger>()
        {
            var logger = A.Fake<ILogger<TLogger>>();

            return (logger, logger.WithFakeDefaults());
        }

        public static (ILogger Logger, FakeLoggingContext Context) Create()
        {
            var logger = A.Fake<ILogger>();

            return (logger, logger.WithFakeDefaults());
        }
    }
}
