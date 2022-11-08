namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Azure.Messaging.ServiceBus.Administration;
    using McMaster.Extensions.CommandLineUtils;

    static class Endpoint
    {
        public static async Task Create(ServiceBusAdministrationClient client, CommandArgument name, CommandOption topicName, CommandOption topicToPublishTo, CommandOption topicToSubscribeOn, CommandOption subscriptionName, CommandOption<int> size, CommandOption partitioning)
        {
            try
            {
                await Queue.Create(client, name, size, partitioning);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                Console.WriteLine($"Queue '{name.Value}' already exists, skipping creation");
            }

            if (topicName.HasValue())
            {
                try
                {
                    await Topic.Create(client, topicName, size, partitioning);
                }
                catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    Console.WriteLine($"Topic '{topicName.Value()}' already exists, skipping creation");
                }

                try
                {
                    await Subscription.Create(client, name, topicName, subscriptionName);
                }
                catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    Console.WriteLine($"Subscription '{name.Value}' already exists, skipping creation");
                }

                // Validation takes care when the topic name is set the other options are not valid
                return;
            }

            // validation takes care when one is set the other is also set
            if (topicToPublishTo.HasValue())
            {
                try
                {
                    await Topic.Create(client, topicToPublishTo, size, partitioning);
                }
                catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    Console.WriteLine($"Topic '{topicToPublishTo.Value()}' already exists, skipping creation");
                }

                try
                {
                    await Topic.Create(client, topicToSubscribeOn, size, partitioning);
                }
                catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    Console.WriteLine($"Topic '{topicToSubscribeOn.Value()}' already exists, skipping creation");
                }

                var forwardingSubscriptionName = $"forwardTo-{topicToSubscribeOn.Value()}";
                try
                {
                    await Subscription.CreateForwarding(client, topicToPublishTo, topicToSubscribeOn, forwardingSubscriptionName);
                }
                catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    Console.WriteLine($"Subscription '{forwardingSubscriptionName}' already exists, skipping creation");
                }

                try
                {
                    await Subscription.Create(client, name, topicToSubscribeOn, subscriptionName);
                }
                catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                {
                    Console.WriteLine($"Subscription '{name.Value}' already exists, skipping creation");
                }
            }
        }

        public static async Task Subscribe(ServiceBusAdministrationClient client, CommandArgument name, CommandOption topicName, CommandOption subscriptionName, CommandArgument eventType, CommandOption ruleName)
        {
            try
            {
                await Rule.Create(client, name, topicName, subscriptionName, eventType, ruleName);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
                Console.WriteLine($"Rule '{name}' for topic '{topicName.Value()}' and subscription '{subscriptionName.Value()}' already exists, skipping creation. Verify SQL filter matches '[NServiceBus.EnclosedMessageTypes] LIKE '%{eventType.Value}%'.");
            }
        }

        public static async Task Unsubscribe(ServiceBusAdministrationClient client, CommandArgument name, CommandOption topicName, CommandOption subscriptionName, CommandArgument eventType, CommandOption ruleName)
        {
            try
            {
                await Rule.Delete(client, name, topicName, subscriptionName, eventType, ruleName);
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            {
                Console.WriteLine($"Rule '{name}' for topic '{topicName.Value()}' and subscription '{subscriptionName.Value()}' does not exist, skipping deletion");
            }
        }
    }
}