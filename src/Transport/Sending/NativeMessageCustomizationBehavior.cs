﻿namespace NServiceBus
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus;
    using Pipeline;
    using Transport;

    class NativeMessageCustomizationBehavior : Behavior<IRoutingContext>
    {
        readonly bool isOutboxEnabled;

        public NativeMessageCustomizationBehavior(bool isOutboxEnabled)
        {
            this.isOutboxEnabled = isOutboxEnabled;
        }

        internal const string CustomizationKey = "$ASB.CustomizationId";

        public override Task Invoke(IRoutingContext context, Func<Task> next)
        {
            if (!context.Extensions.TryGet(CustomizationKey, out Action<Message> customization))
            {
                return next();
            }

            if (isOutboxEnabled)
            {
                throw new Exception("Native message customization cannot be used together with the Outbox as customizations are not persistent. Disable the outbox to use native message customization.");
            }

            // When part of an incoming message, the transport transaction is set by the transport.
            // Otherwise it will be created at this point which works because there is no batched dispatch for IMessageSession operations.
            var transportTransaction = context.Extensions.GetOrCreate<TransportTransaction>();

            // Use the TransportTransaction to store complex objects and pass them to the transport. The transaction might be shared by multiple send operations.
            var customizer = transportTransaction.GetOrCreate<NativeMessageCustomizer>();

            var customizationId = Guid.NewGuid().ToString();
            customizer.Customizations.TryAdd(customizationId, customization);

            // Store they key to the customization in the dispatch properties that are passed to the transport
            context.Extensions.Get<DispatchProperties>()[CustomizationKey] = customizationId;

            return next();
        }
    }
}