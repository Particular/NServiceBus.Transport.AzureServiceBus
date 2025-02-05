namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.ServiceBus.Administration;

public class RecordingServiceBusAdministrationClient : ServiceBusAdministrationClient
{
    readonly StringBuilder builder = new();

    public override Task<Response<SubscriptionProperties>> CreateSubscriptionAsync(CreateSubscriptionOptions options, CreateRuleOptions rule,
        CancellationToken cancellationToken = default)
    {
        builder.AppendLine($"CreateSubscriptionOptions: {JsonSerializer.Serialize(options, SkdJsonSerializerContext.Default.CreateSubscriptionOptions)}");
        builder.AppendLine($"CreateRuleOptions: {JsonSerializer.Serialize(rule, SkdJsonSerializerContext.Default.CreateRuleOptions)}");
        return Task.FromResult<Response<SubscriptionProperties>>(null);
    }

    public override Task<Response<TopicProperties>> CreateTopicAsync(CreateTopicOptions options, CancellationToken cancellationToken = new CancellationToken())
    {
        builder.AppendLine($"CreateTopicOptions: {JsonSerializer.Serialize(options, SkdJsonSerializerContext.Default.CreateTopicOptions)}");
        return Task.FromResult<Response<TopicProperties>>(null);
    }

    public override Task<Response<RuleProperties>> CreateRuleAsync(string topicName, string subscriptionName, CreateRuleOptions options,
        CancellationToken cancellationToken = default)
    {
        builder.AppendLine($"CreateRuleOptions(topicName: '{topicName}', subscriptionName: '{subscriptionName}'): {JsonSerializer.Serialize(options, SkdJsonSerializerContext.Default.CreateRuleOptions)}");
        return Task.FromResult<Response<RuleProperties>>(null);
    }

    public override string ToString() => builder.ToString();
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(CreateSubscriptionOptions))]
[JsonSerializable(typeof(CreateTopicOptions))]
[JsonSerializable(typeof(CreateRuleOptions))]
public partial class SkdJsonSerializerContext : JsonSerializerContext;