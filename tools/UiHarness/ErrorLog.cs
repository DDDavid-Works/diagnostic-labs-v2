using Microsoft.Extensions.Logging;

namespace UiHarness;

internal sealed class ErrorLogProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new ErrorLogger();

    public void Dispose()
    {
    }

    private sealed class ErrorLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
                Console.WriteLine("LOGGED ERROR: " + formatter(state, exception) + " " + exception);
        }
    }
}
