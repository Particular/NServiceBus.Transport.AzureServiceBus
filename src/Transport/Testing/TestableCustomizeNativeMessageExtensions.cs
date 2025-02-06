namespace NServiceBus.Testing;

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
    public static Action<ServiceBusMessage>? GetNativeMessageCustomization(this ExtendableOptions options)
        => options.GetExtensions().TryGet<Action<ServiceBusMessage>>(NativeMessageCustomizationBehavior.CustomizationKey, out var customization) ? customization : null;
}