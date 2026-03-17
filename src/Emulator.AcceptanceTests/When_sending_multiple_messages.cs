namespace NServiceBus.Transport.AzureServiceBus.Emulator.AcceptanceTests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTesting.Customization;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NServiceBus.Logging;
using NUnit.Framework;

[RunOnlyWithEmulator]
public class When_sending_multiple_messages : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_receive_them()
    {
        var kickOffMessageId = Guid.NewGuid().ToString();

        var listOfMessagesForBatching = new List<string>();
        for (var i = 0; i < 50; i++)
        {
            listOfMessagesForBatching.Add(Guid.NewGuid().ToString());
        }

        var listOfMessagesForImmediateDispatch = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            listOfMessagesForImmediateDispatch.Add(Guid.NewGuid().ToString());
        }

        var context = await Scenario.Define<Context>(c =>
            {

                c.LogLevel = LogLevel.Debug;
                c.MessageIdsForBatching = listOfMessagesForBatching;
                c.MessageIdsForImmediateDispatch = listOfMessagesForImmediateDispatch;
                c.AllMessageIds = new ConcurrentDictionary<string, string>(listOfMessagesForBatching.Union(listOfMessagesForImmediateDispatch).ToDictionary(x => x, x => x));
            })
            .WithEndpoint<Sender>(b => b.When(session =>
            {
                var options = new SendOptions();
                options.RouteToThisEndpoint();
                options.SetMessageId(kickOffMessageId);

                return session.Send(new SendMessagesInBatches(), options);
            }))
            .WithEndpoint<Receiver>()
            .Run();

        Assert.That(context.AllMessageIds, Is.Empty, "All messages should have been received");
    }

    public class Context : ScenarioContext
    {
        public List<string> MessageIdsForBatching { get; set; }
        public List<string> MessageIdsForImmediateDispatch { get; set; }
        public ConcurrentDictionary<string, string> AllMessageIds { get; set; }
    }

    public class Sender : EndpointConfigurationBuilder
    {
        public Sender() =>
            EndpointSetup<DefaultServer>(builder =>
            {
                builder.ConfigureRouting().RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
            });

        public class BatchHandler(Context testContext) : IHandleMessages<SendMessagesInBatches>
        {
            public async Task Handle(SendMessagesInBatches message, IMessageHandlerContext context)
            {
                foreach (var messageId in testContext.MessageIdsForBatching)
                {
                    var options = new SendOptions();
                    options.SetMessageId(messageId);

                    await context.Send(new MyMessage(), options);
                }

                foreach (var messageId in testContext.MessageIdsForImmediateDispatch)
                {
                    var options = new SendOptions();
                    options.SetMessageId(messageId);
                    options.RequireImmediateDispatch();

                    await context.Send(new MyMessage(), options);
                }
            }
        }
    }

    public class Receiver : EndpointConfigurationBuilder
    {
        public Receiver() => EndpointSetup<DefaultServer>(c => c.LimitMessageProcessingConcurrencyTo(60));

        public class MyMessageHandler(Context testContext) : IHandleMessages<MyMessage>
        {
            public Task Handle(MyMessage messageWithLargePayload, IMessageHandlerContext context)
            {
                testContext.AllMessageIds.TryRemove(context.MessageId, out _);
                testContext.MarkAsCompleted(testContext.AllMessageIds.IsEmpty);
                return Task.CompletedTask;
            }
        }
    }

    public class SendMessagesInBatches : ICommand;

    public class MyMessage : ICommand;
}