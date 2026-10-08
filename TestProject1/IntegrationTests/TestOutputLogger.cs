using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Xunit.Abstractions;

namespace TestProject1.IntegrationTests
{
    public sealed class TestOutputLogger(ITestOutputHelper output, string categoryName, LogLevel? minLogLevel = null) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => minLogLevel.HasValue ? logLevel >= minLogLevel.Value : logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            Console.WriteLine($"[{categoryName}] {logLevel}: {message}");

            if (exception is not null)
            {
                Console.WriteLine(exception.ToString());
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
