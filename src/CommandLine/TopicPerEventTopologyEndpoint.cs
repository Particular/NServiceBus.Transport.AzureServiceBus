namespace NServiceBus.Transport.AzureServiceBus.CommandLine;

using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using McMaster.Extensions.CommandLineUtils;

static class TopicPerEventTopologyEndpoint
{
    public static async Task Create(ServiceBusAdministrationClient client, CommandArgument name, CommandOption<int> size, CommandOption partitioning)
    {
        try
        {
            await Queue.Create(client, name, size, partitioning);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            Console.WriteLine($"Queue '{name.Value}' already exists, skipping creation");
        }
    }

    public static async Task Subscribe(ServiceBusAdministrationClient client, CommandArgument name, CommandArgument topicName, CommandOption subscriptionName, CommandOption<int> size, CommandOption partitioning)
    {
        try
        {
            await Topic.Create(client, topicName, size, partitioning);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            Console.WriteLine($"Topic '{topicName.Value}' already exists, skipping creation");
        }

        try
        {
            await Subscription.CreateWithMatchAll(client, name, topicName, subscriptionName);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            Console.WriteLine($"Subscription '{name.Value}' already exists, skipping creation");
        }
    }

    public static async Task Unsubscribe(ServiceBusAdministrationClient client, CommandArgument name, CommandArgument topicName, CommandOption subscriptionName)
    {
        try
        {
            await Subscription.Delete(client, name, topicName, subscriptionName);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            Console.WriteLine($"Subscription '{name.Value}' already exists, skipping creation");
        }
    }
}