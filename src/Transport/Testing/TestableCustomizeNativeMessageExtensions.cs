namespace NServiceBus.Testing
{
    using System;
    using Extensibility;
    using Microsoft.Azure.ServiceBus;

    /// <summary>
    /// Provides helper implementations for the native message customization for testing purposes.
    /// </summary>
    public static class TestableCustomizeNativeMessageExtensions
    {
        /// <summary>
        /// Gets the customization of the outgoing native message sent using <see cref="NServiceBus.SendOptions"/>, <see cref="NServiceBus.PublishOptions"/> or <see cref="NServiceBus.ReplyOptions"/>.
        /// </summary>
        /// <param name="options">Option being extended.</param>
        /// <returns>The customization action or null.</returns>
        public static Action<Message> GetNativeMessageCustomization(this ExtendableOptions options)
        {
            if (!options.GetHeaders().TryGetValue(CustomizeNativeMessageExtensions.CustomizationHeader, out var customizationId))
            {
                return null;
            }

            var messageCustomizer = options.GetExtensions().GetOrCreate<NativeMessageCustomizer>();
            return messageCustomizer.Customizations.TryGetValue(customizationId, out var customization) ? customization : null;
        }

        /// <summary>
        /// Gets the customization of the outgoing native message sent using <see cref="NServiceBus.SendOptions"/>, <see cref="NServiceBus.PublishOptions"/> or <see cref="NServiceBus.ReplyOptions"/>.
        /// </summary>
        /// <param name="options">Option being extended.</param>
        /// <param name="context">Context used to dispatch messages in the message handler.</param>
        /// <returns>The customization action or null.</returns>
        public static Action<Message> GetNativeMessageCustomization(this ExtendableOptions options, IPipelineContext context)
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