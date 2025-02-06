namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

public class RecordingServiceBusClient(StringBuilder builder = null) : ServiceBusClient
{
    readonly StringBuilder builder = builder ?? new StringBuilder();

    public override ServiceBusRuleManager CreateRuleManager(string topicName, string subscriptionName) => new RecordingRuleManager(topicName, subscriptionName, builder);

    public override string ToString() => builder.ToString();

    class RecordingRuleManager(string topicName, string subscriptionName, StringBuilder builder) : ServiceBusRuleManager
    {
        public override Task CreateRuleAsync(CreateRuleOptions options, CancellationToken cancellationToken = default)
        {
            builder.AppendLine($"CreateRuleOptions(topicName: '{topicName}', subscriptionName: '{subscriptionName}'): {JsonSerializer.Serialize(options, SkdJsonSerializerContext.PolymorphicOptions)}");
            return Task.CompletedTask;
        }

        public override Task CreateRuleAsync(string ruleName, RuleFilter filter, CancellationToken cancellationToken = default)
        {
            builder.AppendLine($"RuleFilter(topicName: '{topicName}', subscriptionName: '{subscriptionName}', , ruleName: '{ruleName}'): {JsonSerializer.Serialize(filter, SkdJsonSerializerContext.PolymorphicOptions)}");
            return Task.CompletedTask;
        }

        public override Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}