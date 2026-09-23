#nullable enable
namespace NServiceBus.Transport.AzureServiceBus.TransportTests.SessionEnabled;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.TransportTests;
using NUnit.Framework;
using NUnit.Framework.Internal;

[TestFixture]
public class When_distributed_tracing : NServiceBusTransportTest
{
    [TestCase(TransportTransactionMode.ReceiveOnly)]
    [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
    public async Task Should_not_have_ambient_activity_when_not_listening_to_sdk_activity(TransportTransactionMode transactionMode)
    {
        await Initialize(
            (_, _) =>
            {
                Assert.That(Activity.Current, Is.Null, "no ambient activity should be left after dispatch");
                return Task.CompletedTask;
            },
            (_, _) => Task.FromResult(ErrorHandleResult.Handled),
            transactionMode,
            cancellationToken: TestTimeoutCancellationToken);

        // Send multiple messages before starting the pump
        await SendMessage(Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(InputQueueName), new Dictionary<string, string> { ["Order"] = "1" });
        await SendMessage(Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(InputQueueName), new Dictionary<string, string> { ["Order"] = "2" });

        await receiver.StartReceive(TestTimeoutCancellationToken);

        await Task.Delay(TimeSpan.FromSeconds(2));

        await StopPump(TestTimeoutCancellationToken);
    }

    [TestCase(TransportTransactionMode.ReceiveOnly)]
    [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
    public async Task Should_have_ambient_activity_when_listening_to_sdk_activity(TransportTransactionMode transactionMode)
    {
        AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

        var messageProcessed = CreateTaskCompletionSource();
        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == "Azure.Messaging.ServiceBus";
        listener.Sample = (ref options) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStarted = activity =>
        {
            Console.WriteLine($"[START] Operation: {activity.OperationName}");
        };
        listener.ActivityStopped = activity =>
        {
            Console.WriteLine($"[STOP] Operation: {activity.OperationName}");
        };

        ActivitySource.AddActivityListener(listener);

        await Initialize(
            (_, _) =>
            {
                Assert.That(Activity.Current, Is.Not.Null, "Expected an ambient Azure SDK activity while processing the message.");
                Assert.That(Activity.Current.Tags, Does.ContainKey("nservicebus.azureservicebus.session_id"), "Has expected tags");
                messageProcessed.SetResult();
                return Task.CompletedTask;
            },
            (_, _) => Task.FromResult(ErrorHandleResult.Handled),
            transactionMode,
            cancellationToken: TestTimeoutCancellationToken);


        // Send multiple messages before starting the pump
        await SendMessage(Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(InputQueueName), new Dictionary<string, string> { ["Order"] = "1" });
        await SendMessage(Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(InputQueueName), new Dictionary<string, string> { ["Order"] = "2" });

        await receiver.StartReceive(TestTimeoutCancellationToken);

        await StopPump(TestTimeoutCancellationToken);
    }
}