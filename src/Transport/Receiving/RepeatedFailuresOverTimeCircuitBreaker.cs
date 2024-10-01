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

        public void Success() => DisarmIfArmedOrTriggered();

        public Task Failure(Exception exception, CancellationToken cancellationToken = default)
        {
            UpdateLastException(exception);

            ArmIfDisarmed(exception);

            return DelayIfTriggered(cancellationToken);
        }

        public void Dispose() => timer?.Dispose();

        void CircuitBreakerTriggered(object state) => TriggerIfArmed();

        void DisarmIfArmedOrTriggered()
        {
            if (TryDisarmIfArmed() || TryDisarmIfTriggered())
            {
                Disarm();
            }

            bool TryDisarmIfArmed()
            {
                return Interlocked.CompareExchange(ref circuitBreakerState, Disarmed, Armed) == Armed;
            }

            bool TryDisarmIfTriggered()
            {
                return Interlocked.CompareExchange(ref circuitBreakerState, Disarmed, Triggered) == Triggered;
            }

            void Disarm()
            {
                DisableTriggerTimer();
                LogDisarmed();
                OnDisarmed();
            }

            void LogDisarmed()
            {
                Logger.InfoFormat("The circuit breaker for {0} is now disarmed", name);
            }

            void OnDisarmed()
            {
                disarmedAction?.Invoke();
            }

            void DisableTriggerTimer()
            {
                _ = timer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        void UpdateLastException(Exception exception)
        {
            _ = Interlocked.Exchange(ref lastException, exception);
        }

        void ArmIfDisarmed(Exception exception)
        {
            if (TryArmIfDisarmed())
            {
                Arm();
            }

            bool TryArmIfDisarmed()
            {
                return Interlocked.CompareExchange(ref circuitBreakerState, Armed, Disarmed) == Disarmed;
            }

            void Arm()
            {
                SetTimeToTrigger();

                LogArmed(exception);

                OnArmed();
            }

            void OnArmed() => armedAction?.Invoke();

            void SetTimeToTrigger()
            {
                _ = timer.Change(timeToWaitBeforeTriggering, NoPeriodicTriggering);
            }

            void LogArmed(Exception exception)
            {
                Logger.WarnFormat("The circuit breaker for {0} is now in the armed state due to {1}", name, exception);
            }
        }

        Task DelayIfTriggered(CancellationToken cancellationToken)
        {
            if (IsTriggered())
            {
                return DelayFor10Seconds();
            }

            return DelayFor1Second();

            bool IsTriggered()
            {
                return Interlocked.CompareExchange(ref circuitBreakerState, Triggered, Triggered) == Triggered;
            }

            Task DelayFor10Seconds()
            {
                return Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }

            Task DelayFor1Second()
            {
                return Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }

        void TriggerIfArmed()
        {
            if (TryTriggerIfArmed())
            {
                LogTriggered();

                OnTriggered();
            }

            bool TryTriggerIfArmed()
            {
                return Interlocked.CompareExchange(ref circuitBreakerState, Triggered, Armed) == Armed;
            }

            void LogTriggered()
            {
                Logger.WarnFormat("The circuit breaker for {0} will now be triggered with exception {1}", name, lastException);
            }

            void OnTriggered()
            {
                triggerAction?.Invoke(lastException);
            }

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