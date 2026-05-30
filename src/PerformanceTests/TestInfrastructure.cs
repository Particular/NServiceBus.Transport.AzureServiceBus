namespace NServiceBus.PerformanceTests.Infrastructure;

using System.IO.Hashing;
using System.Text;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Messages;

static partial class TestInfrastructure
{
    static readonly string ConnectionString = GetConnectionString();

    internal static partial string GetTestSuiteName() =>
        "Azure Service Bus Transport Performance Tests";

    internal static partial string FormatQueueName(string name) =>
        Sanitize(name);

    internal static partial void ConfigureTransport(
        EndpointConfiguration config,
        TransportTransactionMode transactionMode,
        Dictionary<Type, string>? routing)
    {
        var transport = new AzureServiceBusTransport(ConnectionString, TopicTopology.Default);

        var routingConfig = config.UseTransport(transport);

        if (routing is { Count: > 0 })
        {
            foreach (var (messageType, destination) in routing)
            {
                routingConfig.RouteToEndpoint(messageType, destination);
            }
        }
    }

    internal static partial void EnsureQueueExists(string queueName, int maxDepth)
    {
        var adminClient = new ServiceBusAdministrationClient(ConnectionString);

        try
        {
            if (adminClient.QueueExistsAsync(queueName).GetAwaiter().GetResult())
            {
                return;
            }

            var options = new CreateQueueOptions(queueName)
            {
                EnableBatchedOperations = true,
                LockDuration = TimeSpan.FromMinutes(5),
                MaxDeliveryCount = int.MaxValue,
                MaxSizeInMegabytes = 5120
            };

            adminClient.CreateQueueAsync(options).GetAwaiter().GetResult();
        }
        catch (ServiceBusException sbe) when (sbe.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            // Queue already exists
        }
    }

    internal static partial void PurgeQueue(string queueName)
    {
        var adminClient = new ServiceBusAdministrationClient(ConnectionString);

        try
        {
            if (!adminClient.QueueExistsAsync(queueName).GetAwaiter().GetResult())
            {
                return;
            }

            adminClient.DeleteQueueAsync(queueName).GetAwaiter().GetResult();

            var options = new CreateQueueOptions(queueName)
            {
                EnableBatchedOperations = true,
                LockDuration = TimeSpan.FromMinutes(5),
                MaxDeliveryCount = int.MaxValue,
                MaxSizeInMegabytes = 5120
            };

            adminClient.CreateQueueAsync(options).GetAwaiter().GetResult();
        }
        catch (ServiceBusException)
        {
            // Queue may not exist
        }
    }

    internal static partial void SeedQueue(string queueName, int messageCount) =>
        SeedMessages(queueName, messageCount, typeof(PerfTestMessage), "Send");

    internal static partial void SeedQueueAsEvents(string queueName, int messageCount) =>
        SeedMessages(queueName, messageCount, typeof(PerfTestEvent), "Publish");

    internal static partial void SeedQueueAsFailures(string queueName, int messageCount) =>
        SeedMessages(queueName, messageCount, typeof(PerfTestFailureMessage), "Send");

    static void SeedMessages(string queueName, int messageCount, Type messageType, string intent)
    {
        var client = new ServiceBusClient(ConnectionString);
        var sender = client.CreateSender(queueName);

        for (int i = 0; i < messageCount; i++)
        {
            var messageId = Guid.NewGuid().ToString();

            var message = new ServiceBusMessage(Encoding.UTF8.GetBytes($"{{\"Index\":{i}}}"))
            {
                MessageId = messageId,
                ContentType = "application/json"
            };

            message.ApplicationProperties[Headers.MessageId] = messageId;
            message.ApplicationProperties[Headers.EnclosedMessageTypes] = messageType.FullName!;
            message.ApplicationProperties[Headers.ContentType] = "application/json";
            message.ApplicationProperties[Headers.MessageIntent] = intent;

            sender.SendMessageAsync(message).GetAwaiter().GetResult();
        }

        sender.DisposeAsync().AsTask().GetAwaiter().GetResult();
        client.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    static string Sanitize(string name)
    {
        if (name.Length <= 50)
        {
            return name;
        }

        var nameBytes = Encoding.UTF8.GetBytes(name);
        var hashHex = Convert.ToHexString(XxHash32.Hash(nameBytes));
        int prefixLength = 50 - hashHex.Length;
        var prefix = name[..Math.Min(prefixLength, name.Length)];
        return $"{prefix}{hashHex}";
    }

    static string GetConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Environment variable 'AzureServiceBus_ConnectionString' must be set to run performance tests.");
        }

        return connectionString;
    }
}
