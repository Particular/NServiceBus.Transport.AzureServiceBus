namespace NServiceBus.Transport.AzureServiceBus;

using Azure.Messaging.ServiceBus;

static class ProcessErrorEventArgsExtensions
{
    // A failed lock renewal affects only the message (or session) whose lock was lost; the processor keeps receiving.
    // Arming the circuit breaker for it throttles the processor to a single concurrent call, and lowering
    // concurrency while handlers are slow can leave the processor unable to scale back up.
    public static bool ShouldArmCircuitBreaker(this ProcessErrorEventArgs args) =>
        args.ErrorSource != ServiceBusErrorSource.RenewLock;
}
