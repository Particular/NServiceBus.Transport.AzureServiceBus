namespace NServiceBus
{
    using System;
    using Microsoft.Azure.ServiceBus;
    using NServiceBus.Extensibility;

    /// <summary>
    /// Allows the users to customize outgoing native messages.
    /// </summary>
    /// <remarks>
    /// The behavior of this class is exposed via extension methods.
    /// </remarks>
    public static class CustomizeNativeMessageExtensions
    {
        internal const string CustomizationHeader = "$ASB.CustomizationId";

        /// <summary>
        /// Allows customization of the outgoing native message sent using <see cref="IMessageSession"/>.
        /// </summary>
        /// <param name="options">Option being extended.</param>
        /// <param name="customization">Customization action.</param>
        public static void CustomizeNativeMessage(this ExtendableOptions options, Action<Message> customization)
        {
            if (options.GetHeaders().ContainsKey(CustomizationHeader))
            {
                throw new InvalidOperationException("Native outgoing message has already been customized. Do not apply native outgoing message customization more than once per message.");
            }

            var customizationId = Guid.NewGuid().ToString();
            options.SetHeader(CustomizationHeader, customizationId);

            //TODO retrieve NativeMessageCustomizer from the message properties instead of the context
            var nativePropertiesCustomizer = options.GetExtensions().GetOrCreate<NativeMessageCustomizer>();
            if (!nativePropertiesCustomizer.Customizations.TryAdd(customizationId, customization))
            {
                throw new Exception("Failed to apply an outgoing message customization");
            }
        }

        /// <summary>
        /// Allows customization of the outgoing native message.
        /// </summary>
        /// <remarks>
        /// Messages can be sent using <see cref="IPipelineContext"/> or any of its derived variants such as <see cref="IMessageHandlerContext"/>.
        /// </remarks>
        /// <param name="options">Option being extended.</param>
        /// <param name="context">Context used to dispatch messages in the message handler.</param>
        /// <param name="customization">Customization action.</param>
        public static void CustomizeNativeMessage(this ExtendableOptions options, IPipelineContext context, Action<Message> customization)
        {
            if (options.GetHeaders().ContainsKey(CustomizationHeader))
            {
                throw new InvalidOperationException("Native outgoing message has already been customized. Do not apply native outgoing message customization more than once per message.");
            }

            var customizationId = Guid.NewGuid().ToString();
            options.SetHeader(CustomizationHeader, customizationId);

            // Assumption: NativeMessageCustomizer is added to the context bag associated with each incoming message handled in IMessageHandlerContext and should not be re-created
            var nativePropertiesCustomizer = context.Extensions.Get<NativeMessageCustomizer>();
            if (!nativePropertiesCustomizer.Customizations.TryAdd(customizationId, customization))
            {
                throw new Exception("Failed to apply an outgoing message customization");
            }
        }
    }
}