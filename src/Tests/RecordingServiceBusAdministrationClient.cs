namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.ServiceBus.Administration;

public class RecordingServiceBusAdministrationClient(StringBuilder builder = null) : ServiceBusAdministrationClient
{
    readonly StringBuilder builder = builder ?? new StringBuilder();

    public override Task<Response<SubscriptionProperties>> CreateSubscriptionAsync(CreateSubscriptionOptions options, CreateRuleOptions rule,
        CancellationToken cancellationToken = default)
    {
        builder.AppendLine($"CreateSubscriptionOptions: {JsonSerializer.Serialize(options, SkdJsonSerializerContext.PolymorphicOptions)}");
        builder.AppendLine($"CreateRuleOptions: {JsonSerializer.Serialize(rule, SkdJsonSerializerContext.PolymorphicOptions)}");
        return Task.FromResult<Response<SubscriptionProperties>>(null);
    }

    public override Task<Response<TopicProperties>> CreateTopicAsync(CreateTopicOptions options, CancellationToken cancellationToken = default)
    {
        builder.AppendLine($"CreateTopicOptions: {JsonSerializer.Serialize(options, SkdJsonSerializerContext.PolymorphicOptions)}");
        return Task.FromResult<Response<TopicProperties>>(null);
    }

    public override Task<Response<RuleProperties>> CreateRuleAsync(string topicName, string subscriptionName, CreateRuleOptions options,
        CancellationToken cancellationToken = default)
    {
        builder.AppendLine($"CreateRuleOptions(topicName: '{topicName}', subscriptionName: '{subscriptionName}'): {JsonSerializer.Serialize(options, SkdJsonSerializerContext.PolymorphicOptions)}");
        return Task.FromResult<Response<RuleProperties>>(null);
    }

    public override string ToString() => builder.ToString();
}