namespace NServiceBus;

using System;
using Azure.Messaging.ServiceBus;
using Extensibility;

/// <summary>
/// Allows the users to customize outgoing native messages.
/// </summary>
/// <remarks>
/// The behavior of this class is exposed via extension methods.
/// </remarks>
public static class CustomizeNativeMessageExtensions
{
    /// <summary>
    /// Allows customization of the outgoing native message sent using <see cref="IMessageSession"/>.
    /// </summary>
    /// <param name="options">Option being extended.</param>
    /// <param name="customization">Customization action.</param>
    public static void CustomizeNativeMessage(this ExtendableOptions options, Action<ServiceBusMessage> customization)
    {
        var extensions = options.GetExtensions();
        if (extensions.TryGet<Action<ServiceBusMessage>>(NativeMessageCustomizationBehavior.CustomizationKey, out _))
        {
            throw new InvalidOperationException("Native outgoing message has already been customized. Do not apply native outgoing message customization more than once per message.");
        }

        extensions.Set(NativeMessageCustomizationBehavior.CustomizationKey, customization);
    }
}