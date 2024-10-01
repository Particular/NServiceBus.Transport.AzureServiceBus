namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    sealed class RepeatedFailuresOverTimeCircuitBreaker
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
            var previousState = Interlocked.CompareExchange(ref circuitBreakerState, Disarmed, Armed);

            // If the circuit breaker was Armed or triggered before, disarm it
            if (previousState == Armed || Interlocked.CompareExchange(ref circuitBreakerState, Disarmed, Triggered) == Triggered)
            {
                _ = timer.Change(Timeout.Infinite, Timeout.Infinite);
                Logger.InfoFormat("The circuit breaker for {0} is now disarmed", name);
                disarmedAction();
            }
        }

        public Task Failure(Exception exception, CancellationToken cancellationToken = default)
        {
            _ = Interlocked.Exchange(ref lastException, exception);

            // Atomically set state to Armed if it was previously Disarmed
            var previousState = Interlocked.CompareExchange(ref circuitBreakerState, Armed, Disarmed);

            if (previousState == Disarmed)
            {
                armedAction();
                _ = timer.Change(timeToWaitBeforeTriggering, NoPeriodicTriggering);
                Logger.WarnFormat("The circuit breaker for {0} is now in the armed state", name);
            }

            // If the circuit breaker has been triggered, wait for 10 seconds before proceeding to prevent flooding the logs and hammering the ServiceBus
            return Task.Delay(previousState == Triggered ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(1), cancellationToken);
        }

        public void Dispose() => timer?.Dispose();

        void CircuitBreakerTriggered(object state)
        {
            if (Interlocked.CompareExchange(ref circuitBreakerState, Triggered, Armed) != Armed)
            {
                return;
            }

            Logger.WarnFormat("The circuit breaker for {0} will now be triggered", name);
            triggerAction(lastException);
        }

        int circuitBreakerState = Disarmed;
        Exception lastException;

        readonly string name;
        readonly Timer timer;
        readonly TimeSpan timeToWaitBeforeTriggering;
        readonly Action<Exception> triggerAction;
        readonly Action armedAction;
        readonly Action disarmedAction;

        const int Disarmed = 0;
        const int Armed = 1;
        const int Triggered = 2;

        static readonly TimeSpan NoPeriodicTriggering = TimeSpan.FromMilliseconds(-1);
        static readonly ILog Logger = LogManager.GetLogger<RepeatedFailuresOverTimeCircuitBreaker>();
    }
}