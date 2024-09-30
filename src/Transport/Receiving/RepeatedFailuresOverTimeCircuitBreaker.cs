namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    class RepeatedFailuresOverTimeCircuitBreaker
    {
        public RepeatedFailuresOverTimeCircuitBreaker(string name, TimeSpan timeToWaitBeforeTriggering,
            Action<Exception> triggerAction,
            Action armedAction,
            Action disarmedAction)
        {
            this.name = name;
            this.triggerAction = triggerAction;
            this.armedAction = armedAction;
            this.disarmedAction = disarmedAction;
            this.timeToWaitBeforeTriggering = timeToWaitBeforeTriggering;

            timer = new Timer(CircuitBreakerTriggered);
        }

        public void Success()
        {
            // If the failure count was already zero, replace it with zero (no change) and then return original
            if (Interlocked.CompareExchange(ref failureCount, 0, 0) == 0)
            {
                return;
            }

            timer.Change(Timeout.Infinite, Timeout.Infinite);
            Logger.InfoFormat("The circuit breaker for {0} is now disarmed", name);
            disarmedAction();
            triggered = false;
        }

        public Task Failure(Exception exception, CancellationToken cancellationToken = default)
        {
            lastException = exception;
            var newValue = Interlocked.Increment(ref failureCount);

            if (newValue == 1)
            {
                armedAction();
                timer.Change(timeToWaitBeforeTriggering, NoPeriodicTriggering);
                Logger.WarnFormat("The circuit breaker for {0} is now in the armed state", name);
            }

            //If the circuit breaker has been triggered, wait for 10 seconds before proceeding to prevent flooding the logs and hammering the ServiceBus
            var delay = triggered ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(1);

            return Task.Delay(delay, cancellationToken);
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
                triggered = true;
                triggerAction(lastException);
            }
        }

        long failureCount;
        volatile bool triggered;
        Exception lastException;

        readonly string name;
        readonly Timer timer;
        readonly TimeSpan timeToWaitBeforeTriggering;
        readonly Action<Exception> triggerAction;
        readonly Action armedAction;
        readonly Action disarmedAction;

        static readonly TimeSpan NoPeriodicTriggering = TimeSpan.FromMilliseconds(-1);
        static readonly ILog Logger = LogManager.GetLogger<RepeatedFailuresOverTimeCircuitBreaker>();
    }
}