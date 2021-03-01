namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    class RepeatedFailuresOverTimeCircuitBreaker
    {
        public RepeatedFailuresOverTimeCircuitBreaker(string name, TimeSpan timeToWaitBeforeTriggering, Action<string, Exception, CancellationToken> criticalError)
        {
            this.name = name;
            this.criticalError = criticalError;
            this.timeToWaitBeforeTriggering = timeToWaitBeforeTriggering;

            timer = new Timer(CircuitBreakerTriggered);
        }

        public void Success()
        {
            var oldValue = Interlocked.Exchange(ref failureCount, 0);

            if (oldValue == 0)
            {
                return;
            }

            timer.Change(Timeout.Infinite, Timeout.Infinite);
            Logger.InfoFormat("The circuit breaker for {0} is now disarmed", name);
        }

        public Task Failure(Exception exception)
        {
            lastException = exception;
            var newValue = Interlocked.Increment(ref failureCount);

            if (newValue == 1)
            {
                timer.Change(timeToWaitBeforeTriggering, NoPeriodicTriggering);
                Logger.WarnFormat("The circuit breaker for {0} is now in the armed state", name);
            }

            return Task.Delay(TimeSpan.FromSeconds(1));
        }

        public void Dispose()
        {
            timer?.Dispose();
        }

        void CircuitBreakerTriggered(object state)
        {
            if (Interlocked.Read(ref failureCount) > 0)
            {
                Logger.WarnFormat("The circuit breaker for {0} will now be triggered", name);
                criticalError("Failed to receive message from Azure Service Bus.", lastException, CancellationToken.None);
            }
        }

        long failureCount;
        Exception lastException;

        readonly string name;
        readonly Timer timer;
        readonly TimeSpan timeToWaitBeforeTriggering;
        readonly Action<string, Exception, CancellationToken> criticalError;

        static readonly TimeSpan NoPeriodicTriggering = TimeSpan.FromMilliseconds(-1);
        static readonly ILog Logger = LogManager.GetLogger<RepeatedFailuresOverTimeCircuitBreaker>();
    }
}