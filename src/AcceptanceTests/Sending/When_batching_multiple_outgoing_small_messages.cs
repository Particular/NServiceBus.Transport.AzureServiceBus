namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Sending
{
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

            var allMessageids = listOfMessagesForBatching.Union(listOfMessagesForImmediateDispatch).ToList();

            var context = await Scenario.Define<Context>(c =>
                {

                    c.LogLevel = LogLevel.Debug;
                    c.MessageIdsForBatching = listOfMessagesForBatching;
                    c.MessageIdsForImmediateDispatch = listOfMessagesForImmediateDispatch;
                })
                .WithEndpoint<Sender>(b => b.When(session =>
                {
                    var options = new SendOptions();
                    options.RouteToThisEndpoint();
                    options.SetMessageId(kickOffMessageId);

                    return session.Send(new SendMessagesInBatches(), options);
                }))
                .WithEndpoint<Receiver>()
                .Done(c => allMessageids.All(id => c.MessageIdsReceived.Contains(id)))
                .Run();

            var logoutput = AggregateBatchLogOutput(context);

            StringAssert.Contains(kickOffMessageId, logoutput, "Kickoff message was not present in any of the batches");
            StringAssert.Contains("Sent batch '1' with '1'", logoutput, "Should have used 1 batch for the initial kickoff message but didn't");
            StringAssert.Contains($"Sent batch '1' with '{context.MessageIdsForBatching.Count}'", logoutput, "Should have used 1 batch for the batched message dispatch but didn't");

            foreach (var messageIdForBatching in listOfMessagesForBatching)
            {
                StringAssert.Contains(messageIdForBatching, logoutput, $"{messageIdForBatching} not found in any of the batches. Output: {logoutput}");
            }

            foreach (var messageIdForImmediateDispatch in listOfMessagesForImmediateDispatch)
            {
                StringAssert.DoesNotContain(messageIdForImmediateDispatch, logoutput, $"{messageIdForImmediateDispatch} should not be included in any of the batches. Output: {logoutput}");
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
            public ConcurrentBag<string> MessageIdsReceived { get; } = [];
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(builder =>
                {
                    builder.ConfigureRouting().RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
                });
            }

            public class BatchHandler : IHandleMessages<SendMessagesInBatches>
            {
                readonly Context testContext;

                public BatchHandler(Context testContext) => this.testContext = testContext;

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

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                readonly Context testContext;

                public MyMessageHandler(Context testContext) => this.testContext = testContext;

                public Task Handle(MyMessage messageWithLargePayload, IMessageHandlerContext context)
                {
                    testContext.MessageIdsReceived.Add(context.MessageId);
                    return Task.FromResult(0);
                }
            }
        }

        public class SendMessagesInBatches : ICommand
        {
        }

        public class MyMessage : ICommand
        {
        }
    }
}