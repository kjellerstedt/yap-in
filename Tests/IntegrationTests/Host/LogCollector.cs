using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace YapIn.IntegrationTests.Host;

/// <summary>Keeps what the server logs, subject to the same level filters as the console, so a test can read it.</summary>
internal sealed class LogCollector : ILoggerProvider
{
    public ConcurrentQueue<LogEntry> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CollectingLogger(categoryName, Entries);

    public void Dispose()
    {
    }

    internal sealed record LogEntry(string Category, LogLevel Level, string Message);

    private sealed class CollectingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new LogEntry(category, logLevel, formatter(state, exception)));
    }
}
