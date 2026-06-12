namespace NServiceBus.Transport.AzureServiceBus.CommandLine;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using McMaster.Extensions.CommandLineUtils;

static class TopicPerEventTopologyEndpoint
{
    public static async Task Create(ServiceBusAdministrationClient client, CommandArgument name, CommandOption<int> size, CommandOption<int> deliveryCount, CommandOption partitioning, CommandOption hierarchyNamespace, CommandOption forwardDlqTo)
    {
        try
        {
            await Queue.Create(client, name, size, deliveryCount, partitioning, hierarchyNamespace, forwardDlqTo);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            Console.WriteLine($"Queue '{name.Value}' already exists, skipping creation");
        }
    }

    public static async Task Subscribe(ServiceBusAdministrationClient client, CommandArgument name, CommandArgument topicName, CommandOption subscriptionName, CommandOption<int> size, CommandOption partitioning, CommandOption hierarchyNamespace)
    {
        try
        {
            await Topic.Create(client, topicName, size, partitioning, hierarchyNamespace);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            Console.WriteLine($"Topic '{topicName.Value}' already exists, skipping creation");
        }

        try
        {
            await Subscription.CreateWithMatchAll(client, name, topicName, subscriptionName, hierarchyNamespace);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            Console.WriteLine($"Subscription '{name.Value}' already exists, skipping creation");
        }
    }

    public static async Task SubscribeWithFilter(ServiceBusAdministrationClient client, CommandArgument name, CommandArgument topicName, CommandOption subscriptionName, CommandOption<int> size, CommandOption partitioning, CommandOption hierarchyNamespace, List<string> eventTypes, bool useCorrelationFilter)
    {
        try
        {
            await Topic.Create(client, topicName, size, partitioning, hierarchyNamespace);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            Console.WriteLine($"Topic '{topicName.Value}' already exists, skipping creation");
        }

        await Subscription.CreateWithFilteredRules(client, name, topicName, subscriptionName, hierarchyNamespace, eventTypes, useCorrelationFilter);
    }

    public static async Task Unsubscribe(ServiceBusAdministrationClient client, CommandArgument name, CommandArgument topicName, CommandOption subscriptionName, CommandOption hierarchyNamespace)
    {
        try
        {
            await Subscription.Delete(client, name, topicName, subscriptionName, hierarchyNamespace);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            Console.WriteLine($"Subscription '{name.Value}' already exists, skipping creation");
        }
    }

    public static async Task UnsubscribeWithFilter(ServiceBusAdministrationClient client, CommandArgument name, CommandArgument topicName, CommandOption subscriptionName, CommandOption hierarchyNamespace, List<string> eventTypes)
    {
        await Subscription.DeleteRules(client, name, topicName, subscriptionName, hierarchyNamespace, eventTypes);
    }
}