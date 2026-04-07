using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace JuntosSomosMais.Utils.GlobalExceptionHandler.Tests.Fixtures;

public sealed record LogEntry(LogLevel Level, string CategoryName, string Message, Exception? Exception);

public sealed class FakeLogCollector
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => _entries.ToArray();

    internal void Add(LogEntry entry) => _entries.Enqueue(entry);

    public void Clear() => _entries.Clear();
}

public sealed class FakeLogger(string categoryName, FakeLogCollector collector) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        collector.Add(new LogEntry(logLevel, categoryName, formatter(state, exception), exception));
    }
}

public sealed class FakeLoggerProvider(FakeLogCollector collector) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new FakeLogger(categoryName, collector);

    public void Dispose() { }
}
