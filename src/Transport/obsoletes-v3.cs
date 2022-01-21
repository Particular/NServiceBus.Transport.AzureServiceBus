namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using Azure.Messaging.ServiceBus;
    using Extensibility;

    /// <summary>
    /// Adds access to the Azure Service Bus transport config to the global Transport object.
    /// </summary>
    public static partial class AzureServiceBusTransportSettingsExtensions
    {
        /// <summary>
        /// Specifies a callback to apply to a subscription rule name when a subscribed event's name is longer than 50 characters.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="ruleNameShortener">The callback to apply.</param>
        [ObsoleteEx(ReplacementTypeOrMember = "SubscriptionRuleNamingConvention",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public static TransportExtensions<AzureServiceBusTransport> RuleNameShortener(
            this TransportExtensions<AzureServiceBusTransport> transportExtensions,
            Func<string, string> ruleNameShortener)
            => throw new NotImplementedException();

        /// <summary>
        /// Specifies a callback to apply to the subscription name when the endpoint's name is longer than 50 characters.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="subscriptionNameShortener">The callback to apply.</param>
        [ObsoleteEx(ReplacementTypeOrMember = "SubscriptionNamingConvention",
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public static TransportExtensions<AzureServiceBusTransport> SubscriptionNameShortener(
            this TransportExtensions<AzureServiceBusTransport> transportExtensions,
            Func<string, string> subscriptionNameShortener)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Provides helper implementations for the native message customization for testing purposes.
    /// </summary>
    public static partial class TestableCustomizeNativeMessageExtensions
    {
        /// <summary>
        /// Gets the customization of the outgoing native message sent using <see cref="SendOptions"/>, <see cref="PublishOptions"/> or <see cref="ReplyOptions"/>.
        /// </summary>
        /// <param name="options">Option being extended.</param>
        /// <param name="context">Context used to dispatch messages in the message handler.</param>
        /// <returns>The customization action or null.</returns>
        [ObsoleteEx(
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public static Action<ServiceBusMessage> GetNativeMessageCustomization(this ExtendableOptions options,
            IPipelineContext context)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Allows the users to customize outgoing native messages.
    /// </summary>
    /// <remarks>
    /// The behavior of this class is exposed via extension methods.
    /// </remarks>
    public static partial class CustomizeNativeMessageExtensions
    {
        /// <summary>
        /// Allows customization of the outgoing native message.
        /// </summary>
        /// <remarks>
        /// Messages can be sent using <see cref="IPipelineContext"/> or any of its derived variants such as <see cref="IMessageHandlerContext"/>.
        /// </remarks>
        /// <param name="options">Option being extended.</param>
        /// <param name="context">Context used to dispatch messages in the message handler.</param>
        /// <param name="customization">Customization action.</param>
        [ObsoleteEx(
            TreatAsErrorFromVersion = "3",
            RemoveInVersion = "4")]
        public static void CustomizeNativeMessage(this ExtendableOptions options, IPipelineContext context, Action<ServiceBusMessage> customization)
            => throw new NotImplementedException();
    }
}