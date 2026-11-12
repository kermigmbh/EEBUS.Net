using Microsoft.Extensions.Logging;

namespace ConsoleDemo
{
    /// <summary>
    /// Minimal console logger for the RPC server: prints every log line with a timestamp.
    /// </summary>
    internal sealed class RpcConsoleLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {formatter(state, exception)}");
            if (exception != null)
                Console.WriteLine("    " + exception.Message);
        }
    }
}
