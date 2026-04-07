using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging;
using System;

namespace Call_Automation_GCCH.Logging
{
    /// <summary>
    /// Custom ILogger implementation that writes to both the console
    /// and the in-memory <see cref="LogCollector"/> for UI display.
    /// </summary>
    public class ConsoleCollectorLogger : ILogger
    {
        private readonly string _categoryName;

        public ConsoleCollectorLogger(string categoryName)
        {
            _categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state) => default!;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            if (!string.IsNullOrEmpty(message))
            {
                string logOutput = $"{DateTime.UtcNow:HH:mm:ss} [{logLevel}] {_categoryName}: {message}";
                if (exception != null)
                {
                    logOutput += Environment.NewLine + exception;
                }

                Console.WriteLine(logOutput);
                LogCollector.Log(logOutput);
            }
        }
    }
}
