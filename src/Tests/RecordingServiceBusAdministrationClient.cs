namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

public class RecordingServiceBusAdministrationClient : ServiceBusAdministrationClient
{
    readonly StringBuilder builder = new();

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

public class RecordingServiceBusClient : ServiceBusClient
{
    readonly StringBuilder builder = new();

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

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(CreateSubscriptionOptions))]
[JsonSerializable(typeof(CreateTopicOptions))]
[JsonSerializable(typeof(CreateRuleOptions))]
public partial class SkdJsonSerializerContext : JsonSerializerContext
{
    static JsonSerializerOptions options;

    public static JsonSerializerOptions PolymorphicOptions =>
        options ??= new JsonSerializerOptions(Default.Options)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    typeInfo =>
                    {
                        if (typeInfo.Type == typeof(RuleFilter))
                        {
                            typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
                            {
                                TypeDiscriminatorPropertyName = "filter-type",
                                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
                                DerivedTypes =
                                {
                                    new JsonDerivedType(typeof(CorrelationRuleFilter), "correlation"),
                                    new JsonDerivedType(typeof(SqlRuleFilter), "sql"),
                                    new JsonDerivedType(typeof(TrueRuleFilter), "true"),
                                    new JsonDerivedType(typeof(FalseRuleFilter), "false")
                                }
                            };
                        }
                    }
                }
            }
        };
}