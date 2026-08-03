using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests;

/// <summary>
/// A minimal <see cref="ILogger{T}"/> implementation that captures the last logged exception, and every entry written.
/// Useful in tests where the system under test catches and swallows exceptions and you need to surface the real cause from a failed assertion.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public Exception? LastException { get; private set; }

    /// <summary>
    /// Every entry written, in order, with its level and its rendered message.
    /// </summary>
    /// <remarks>
    /// The message is rendered rather than kept as structured state deliberately: what a test usually needs to assert is that a value the caller was given —
    /// a correlation id, say — actually reached the text a human reads in the log file, not merely that it was attached as a property.
    /// </remarks>
    public List<CapturedLogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => NullLogger.Instance.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (exception is not null)
        {
            LastException = exception;
        }

        Entries.Add(new CapturedLogEntry(logLevel, formatter(state, exception), exception));
    }
}

/// <summary>
/// One entry captured by <see cref="CapturingLogger{T}"/>.
/// </summary>
internal sealed record CapturedLogEntry(LogLevel Level, string Message, Exception? Exception);
