namespace NServiceBus.Transport.AzureServiceBus.TransportTests.SessionEnabled;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Logging;
using NServiceBus.TransportTests;
using NUnit.Framework;

[TestFixture]
public class When_session_enabled_receiver_uses_non_session_enabled_queue : NServiceBusTransportTest
{
    [TestCase(TransportTransactionMode.ReceiveOnly)]
    [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
    public async Task Should_log_specific_error_message(TransportTransactionMode transactionMode)
    {
        await Initialize(
            (_, _) => Task.CompletedTask,
            (_, _) => Task.FromResult(ErrorHandleResult.Handled),
            transactionMode,
            cancellationToken: TestTimeoutCancellationToken);

        await RecreateInputQueueWithoutSessions();

        // Send multiple messages before starting the pump
        await SendMessage(Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(InputQueueName), new Dictionary<string, string> { ["Order"] = "1" });
        await SendMessage(Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(InputQueueName), new Dictionary<string, string> { ["Order"] = "2" });

        await receiver.StartReceive(TestTimeoutCancellationToken);

        await Task.Delay(TimeSpan.FromSeconds(2));

        await StopPump(TestTimeoutCancellationToken);

        Assert.That(
            LogFactory.LogItems.Any(logItem =>
                logItem.Level == LogLevel.Error &&
                logItem.Message.Contains("Endpoint is configured for session-based processing", StringComparison.Ordinal) &&
                logItem.Message.Contains(InputQueueName, StringComparison.Ordinal) &&
                logItem.Message.Contains("is not session-enabled", StringComparison.Ordinal)),
            Is.True,
            "The session-specific configuration error should be logged.");
    }

#pragma warning disable PS0018
    async Task RecreateInputQueueWithoutSessions()
#pragma warning restore PS0018
    {
        var adminClient = new ServiceBusAdministrationClient(ConfigureAzureServiceBusTransportInfrastructure.ConnectionString);

        await DeleteQueueIfExists(adminClient, Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(InputQueueName));

        await adminClient.CreateQueueAsync(new CreateQueueOptions(Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(InputQueueName)) { RequiresSession = false }, TestTimeoutCancellationToken);
    }

#pragma warning disable PS0018
    static async Task DeleteQueueIfExists(ServiceBusAdministrationClient adminClient, string queueName)
#pragma warning restore PS0018
    {
        try
        {
            await adminClient.DeleteQueueAsync(queueName);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
        }
    }

#pragma warning disable PS0018
}