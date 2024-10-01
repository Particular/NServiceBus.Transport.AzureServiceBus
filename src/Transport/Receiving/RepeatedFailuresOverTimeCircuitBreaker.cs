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
            Action disarmedAction,
            TimeSpan? timeToWaitWhenTriggered = default,
            TimeSpan? timeToWaitWhenArmed = default)
        {
            this.name = name;
            this.triggerAction = triggerAction;
            this.armedAction = armedAction;
            this.disarmedAction = disarmedAction;
            this.timeToWaitBeforeTriggering = timeToWaitBeforeTriggering;
            this.timeToWaitWhenTriggered = timeToWaitWhenTriggered ?? TimeSpan.FromSeconds(10);
            this.timeToWaitWhenArmed = timeToWaitWhenArmed ?? TimeSpan.FromSeconds(1);

            timer = new Timer(CircuitBreakerTriggered);
        }

        public void Success()
        {
            // Take a snapshot of the current state of the circuit breaker
            var previousState = circuitBreakerState;
            if (previousState is not (Armed or Triggered))
            {
                return;
            }

            // Try to transition to the disarmed state if the circuit breaker is armed or triggered
            // and the previous state is the same that we read before. If that is not the case
            // then another thread has already transitioned the circuit breaker to another state.
            var originalState = Interlocked.CompareExchange(ref circuitBreakerState, Disarmed, previousState);
            if (originalState != previousState)
            {
                return;
            }

            _ = timer.Change(Timeout.Infinite, Timeout.Infinite);
            Logger.InfoFormat("The circuit breaker for {0} is now disarmed", name);
            disarmedAction();
        }

        public Task Failure(Exception exception, CancellationToken cancellationToken = default)
        {
            // Atomically store the exception that caused the circuit breaker to trip
            _ = Interlocked.Exchange(ref lastException, exception);

            // Take a snapshot of the current state of the circuit breaker
            var previousState = circuitBreakerState;
            // If the circuit breaker is disarmed, try to transition to the armed state but the previous state must be disarmed
            // otherwise another thread has already transitioned the circuit breaker to another state
            if (previousState == Disarmed && Interlocked.CompareExchange(ref circuitBreakerState, Armed, Disarmed) == Disarmed)
            {
                armedAction();
                _ = timer.Change(timeToWaitBeforeTriggering, NoPeriodicTriggering);
                Logger.WarnFormat("The circuit breaker for {0} is now in the armed state due to {1}", name, exception);
            }

            return Task.Delay(previousState == Triggered ? timeToWaitWhenTriggered : timeToWaitWhenArmed, cancellationToken);
        }

        public void Dispose() => timer?.Dispose();

        void CircuitBreakerTriggered(object state)
        {
            if (Interlocked.CompareExchange(ref circuitBreakerState, Triggered, Armed) != Armed)
            {
                return;
            }

            Logger.WarnFormat("The circuit breaker for {0} will now be triggered with exception {1}", name, lastException);
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
        readonly TimeSpan timeToWaitWhenTriggered;
        readonly TimeSpan timeToWaitWhenArmed;

        const int Disarmed = 0;
        const int Armed = 1;
        const int Triggered = 2;

        static readonly TimeSpan NoPeriodicTriggering = TimeSpan.FromMilliseconds(-1);
        static readonly ILog Logger = LogManager.GetLogger<RepeatedFailuresOverTimeCircuitBreaker>();
    }
}