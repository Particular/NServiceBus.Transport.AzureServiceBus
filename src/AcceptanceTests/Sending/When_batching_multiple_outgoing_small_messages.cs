namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Sending;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using Logging;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_batching_multiple_outgoing_small_messages : NServiceBusAcceptanceTest
{
    [Test]
    public async Task Should_batch_as_many_as_possible_for_non_immediate_dispatches()
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

        var logoutput = AggregateBatchLogOutput(context);

        Assert.That(logoutput, Does.Contain(kickOffMessageId), "Kickoff message was not present in any of the batches");
        Assert.That(logoutput, Does.Contain("Sent batch '1' with '1'"), "Should have used 1 batch for the initial kickoff message but didn't");
        Assert.That(logoutput, Does.Contain($"Sent batch '1' with '{context.MessageIdsForBatching.Count}'"), "Should have used 1 batch for the batched message dispatch but didn't");

        foreach (var messageIdForBatching in listOfMessagesForBatching)
        {
            Assert.That(logoutput, Does.Contain(messageIdForBatching), $"{messageIdForBatching} not found in any of the batches. Output: {logoutput}");
        }

        foreach (var messageIdForImmediateDispatch in listOfMessagesForImmediateDispatch)
        {
            Assert.That(logoutput, Does.Not.Contain(messageIdForImmediateDispatch), $"{messageIdForImmediateDispatch} should not be included in any of the batches. Output: {logoutput}");
        }
    }

    static string AggregateBatchLogOutput(ScenarioContext context)
    {
        var builder = new StringBuilder();
        foreach (var logItem in context.Logs)
        {
            if (logItem.Message.StartsWith("Sent batch"))
            {
                builder.AppendLine(logItem.Message);
            }
        }

        return builder.ToString();
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