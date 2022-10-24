namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System;
    using Microsoft.Extensions.Logging;

    public class ConsoleLogger : ILogger
    {
        public ConsoleLogger(bool verbose) => this.verbose = verbose;

        public IDisposable BeginScope<TState>(TState state)
        {
            throw new NotImplementedException();
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!verbose && logLevel < LogLevel.Information)
            {
                return;
            }

            var logStatement = formatter(state, exception);
            if (verbose)
            {
                logStatement = $"{DateTime.UtcNow}: {logLevel} {logStatement}";
            }

            Console.Error.WriteLine(logStatement);
        }

        readonly bool verbose;
    }
}