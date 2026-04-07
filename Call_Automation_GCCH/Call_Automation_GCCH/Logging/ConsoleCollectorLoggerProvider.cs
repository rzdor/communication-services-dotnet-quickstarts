using Microsoft.Extensions.Logging;

namespace Call_Automation_GCCH.Logging
{
    /// <summary>
    /// Provider that creates <see cref="ConsoleCollectorLogger"/> instances for the DI logging pipeline.
    /// </summary>
    public class ConsoleCollectorLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new ConsoleCollectorLogger(categoryName);
        }

        public void Dispose()
        {
        }
    }
}
