#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace NServiceBus
{
    using System;
    using Azure.Core;
    using Azure.Messaging.ServiceBus;
    using Extensibility;

    public static partial class AzureServiceBusTransportSettingsExtensions
    {
        [ObsoleteEx(ReplacementTypeOrMember = "SubscriptionRuleNamingConvention",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public static TransportExtensions<AzureServiceBusTransport> RuleNameShortener(
            this TransportExtensions<AzureServiceBusTransport> transportExtensions,
            Func<string, string> ruleNameShortener)
            => throw new NotImplementedException();

        [ObsoleteEx(ReplacementTypeOrMember = "SubscriptionNamingConvention",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public static TransportExtensions<AzureServiceBusTransport> SubscriptionNameShortener(
            this TransportExtensions<AzureServiceBusTransport> transportExtensions,
            Func<string, string> subscriptionNameShortener)
            => throw new NotImplementedException();

        [ObsoleteEx(ReplacementTypeOrMember = "CustomTokenCredential(string fullyQualifiedNamespace, TokenCredential tokenCredential)",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public static TransportExtensions<AzureServiceBusTransport> CustomTokenCredential(this TransportExtensions<AzureServiceBusTransport> transportExtensions, TokenCredential tokenCredential)
            => throw new NotImplementedException();
    }

    public static partial class CustomizeNativeMessageExtensions
    {
        [ObsoleteEx(
            Message = "Use overload that does not require IPipelineContext",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public static void CustomizeNativeMessage(this ExtendableOptions options, IPipelineContext context, Action<ServiceBusMessage> customization)
            => throw new NotImplementedException();
    }
}

namespace NServiceBus.Testing
{
    using System;
    using Azure.Messaging.ServiceBus;
    using NServiceBus.Extensibility;

    public static partial class TestableCustomizeNativeMessageExtensions
    {
        [ObsoleteEx(
            Message = "Use overload that does not require IPipelineContext",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public static Action<ServiceBusMessage> GetNativeMessageCustomization(this ExtendableOptions options,
            IPipelineContext context)
            => throw new NotImplementedException();
    }
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member