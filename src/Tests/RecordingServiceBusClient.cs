namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

public class RecordingServiceBusClient(StringBuilder builder = null, bool subscriptionsMustExist = false) : ServiceBusClient
{
    static readonly ConditionalWeakTable<StringBuilder, HashSet<string>> CreatedSubscriptions = [];

    readonly StringBuilder builder = builder ?? new StringBuilder();

    // The administration client shares the builder with this client, which is how it tells us about created subscriptions.
    internal static void RecordSubscriptionCreated(StringBuilder builder, string topicName, string subscriptionName) =>
        CreatedSubscriptions.GetOrCreateValue(builder).Add($"{topicName}/{subscriptionName}");

    public override ServiceBusRuleManager CreateRuleManager(string topicName, string subscriptionName) =>
        new RecordingRuleManager(topicName, subscriptionName, builder, subscriptionsMustExist);

    public override string ToString() => builder.ToString();

    class RecordingRuleManager(string topicName, string subscriptionName, StringBuilder builder, bool subscriptionMustExist) : ServiceBusRuleManager
    {
        public override Task CreateRuleAsync(CreateRuleOptions options, CancellationToken cancellationToken = default)
        {
            if (subscriptionMustExist && !CreatedSubscriptions.GetOrCreateValue(builder).Contains($"{topicName}/{subscriptionName}"))
            {
                throw new ServiceBusException("The subscription does not exist", ServiceBusFailureReason.MessagingEntityNotFound);
            }

            builder.AppendLine($"CreateRuleOptions(topicName: '{topicName}', subscriptionName: '{subscriptionName}'): {JsonSerializer.Serialize(options, SkdJsonSerializerContext.PolymorphicOptions)}");
            return Task.CompletedTask;
        }

        public override Task DeleteRuleAsync(string ruleName, CancellationToken cancellationToken = default)
        {
            builder.AppendLine($"DeleteRule(topicName: '{topicName}', subscriptionName: '{subscriptionName}', ruleName: '{ruleName}')");
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
