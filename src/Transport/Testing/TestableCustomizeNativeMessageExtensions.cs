namespace NServiceBus.Testing
{
    using System;
    using Azure.Messaging.ServiceBus;
    using Extensibility;

    /// <summary>
    /// Provides helper implementations for the native message customization for testing purposes.
    /// </summary>
    public static class TestableCustomizeNativeMessageExtensions
    {
        /// <summary>
        /// Gets the customization of the outgoing native message sent using <see cref="SendOptions"/>, <see cref="PublishOptions"/> or <see cref="ReplyOptions"/>.
        /// </summary>
        /// <param name="options">Option being extended.</param>
        /// <returns>The customization action or null.</returns>
        public static Action<ServiceBusMessage> GetNativeMessageCustomization(this ExtendableOptions options)
        {
            if (!options.GetHeaders().TryGetValue(CustomizeNativeMessageExtensions.CustomizationHeader, out var customizationId))
            {
                return null;
            }

            var messageCustomizer = options.GetExtensions().GetOrCreate<NativeMessageCustomizer>();
            return messageCustomizer.Customizations.TryGetValue(customizationId, out var customization) ? customization : null;
        }

        /// <summary>
        /// Gets the customization of the outgoing native message sent using <see cref="SendOptions"/>, <see cref="PublishOptions"/> or <see cref="ReplyOptions"/>.
        /// </summary>
        /// <param name="options">Option being extended.</param>
        /// <param name="context">Context used to dispatch messages in the message handler.</param>
        /// <returns>The customization action or null.</returns>
        public static Action<ServiceBusMessage> GetNativeMessageCustomization(this ExtendableOptions options, IPipelineContext context)
        {
            if (!options.GetHeaders().TryGetValue(CustomizeNativeMessageExtensions.CustomizationHeader, out var customizationId))
            {
                return null;
            }

            var messageCustomizer = context.Extensions.GetOrCreate<NativeMessageCustomizer>();
            return messageCustomizer.Customizations.TryGetValue(customizationId, out var customization) ? customization : null;
        }
    }
}