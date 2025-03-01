using Microsoft.Extensions.Logging;

namespace Tests.App;

public class TestLogger<T> : ILogger<T>
{
    public ICollection<string> LogMessages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => throw new NotImplementedException();
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        LogMessages.Add(formatter(state, exception));
    }
}