namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Sending;

using System;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using Azure.Messaging.ServiceBus.Administration;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Configuration.AdvancedExtensibility;
using NUnit.Framework;

// Azure Service Bus SDK doesn't differentiate between sending to a topic or a queue
// There are valid scenarios where a message is sent to a topic and then forwarded to a specific queue
// using rules. This is for example common when you to do some form of replication like describe in
// https://github.com/Azure-Samples/azure-messaging-replication-dotnet/tree/main/functions/code/ServiceBusActivePassive or
// in cases where you want to route the command to a specific endpoint based on some property of the message
// This test makes sure that this scenario is supported and broken over time.
public class When_sending_to_a_topic : NServiceBusAcceptanceTest
{
    static string TopicName;

    [SetUp]
    public async Task Setup()
    {
        TopicName = "SendingToATopic";

        var adminClient =
            new ServiceBusAdministrationClient(
                Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString"));

        if (await adminClient.TopicExistsAsync(TopicName))
        {
            // makes sure during local development the topic gets cleared before each test run
            await adminClient.DeleteTopicAsync(TopicName);
        }

        await adminClient.CreateTopicAsync(TopicName);
        string endpointName = Conventions.EndpointNamingConvention(typeof(Receiver)).Shorten();
        if (!await adminClient.QueueExistsAsync(endpointName))
        {
            await adminClient.CreateQueueAsync(endpointName);
        }
        await adminClient.CreateSubscriptionAsync(new CreateSubscriptionOptions(TopicName, endpointName)
        {
            ForwardTo = endpointName,
        });
    }

    [Test]
    public async Task Should_receive_the_message_assuming_correct_forwarding_rules()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<Sender>(b => b.When(session => session.Send(new MyMessage())))
            .WithEndpoint<Receiver>()
            .Run();

        Assert.That(context.MessageReceived, Is.True);
    }

    public class Context : ScenarioContext
    {
        public bool MessageReceived { get; set; }
    }

    public class Sender : EndpointConfigurationBuilder
    {
        public Sender() =>
            EndpointSetup<DefaultServer>(builder =>
            {
                builder.ConfigureRouting().RouteToEndpoint(typeof(MyMessage), TopicName);
            });
    }

    public class Receiver : EndpointConfigurationBuilder
    {
        public Receiver() => EndpointSetup<DefaultServer>(c =>
        {
            c.GetSettings().Set("Installers.Enable", false);
        });

        public class MyMessageHandler(Context testContext) : IHandleMessages<MyMessage>
        {
            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                testContext.MessageReceived = true;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class MyMessage : ICommand;
}