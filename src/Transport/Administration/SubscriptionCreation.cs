namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Logging;

static class SubscriptionCreation
{
    // A topic or queue another client just created can stay invisible to the subscription request for a few seconds
    public static async Task CreateWithRetries(CreateSubscriptionOptions options, ServiceBusRetryOptions? retryOptions,
        Func<CancellationToken, Task> createSubscription, CancellationToken cancellationToken = default)
    {
        // Not RetryPolicyOptions, so tuning messaging retries doesn't change how long we wait for new entities
        retryOptions ??= new ServiceBusRetryOptions();

        for (var retry = 1; ; retry++)
        {
            try
            {
                await createSubscription(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (!ex.IsCausedBy(cancellationToken) && retry <= retryOptions.MaxRetries && IsEntityNotVisibleYet(ex, options))
            {
                var delay = Delay(retryOptions, retry);
                if (Log.IsInfoEnabled)
                {
                    Log.Info($"Subscription {options.SubscriptionName} on topic {options.TopicName} could not be created yet because the topic or forwarding destination is not available. Retrying in {delay}.");
                }

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    // Matched on the exception shape because the service's message text isn't a stable contract
    static bool IsEntityNotVisibleYet(Exception exception, CreateSubscriptionOptions options) => exception switch
    {
        // The service answers with 404 "not recognized as a namespace policy put request" when it can't resolve the topic
        ServiceBusException { Reason: ServiceBusFailureReason.MessagingEntityNotFound } => true,
        // A missing forwarding destination is a plain 400, so only consider it when the subscription forwards
        ArgumentException { InnerException: RequestFailedException { Status: 400 } } => options.ForwardTo is not null,
        _ => false
    };

    static TimeSpan Delay(ServiceBusRetryOptions retryOptions, int retry)
    {
        var delay = retryOptions.Mode == ServiceBusRetryMode.Exponential
            ? retryOptions.Delay * Math.Pow(2, retry - 1)
            : retryOptions.Delay;
        return delay < retryOptions.MaxDelay ? delay : retryOptions.MaxDelay;
    }

    static readonly ILog Log = LogManager.GetLogger(typeof(SubscriptionCreation));
}
