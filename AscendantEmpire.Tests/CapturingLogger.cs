using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AscendantEmpire.Tests;

/// <summary>
/// A minimal <see cref="ILogger{T}"/> implementation that captures the last logged exception. 
/// Useful in tests where the system under test catches and swallows exceptions and you need to surface the real cause from a failed assertion.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public Exception? LastException { get; private set; }

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
    }
}
