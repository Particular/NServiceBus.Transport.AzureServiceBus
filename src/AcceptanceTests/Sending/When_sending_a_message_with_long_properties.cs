namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Sending;

using System.Threading.Tasks;
using AcceptanceTesting;
using Logging;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_sending_a_message_with_long_properties : NServiceBusAcceptanceTest
{
    const int MaxPropertySize = 32767;
    static readonly string[] HeadersToTrim = ["NServiceBus.ExceptionInfo.StackTrace", "NServiceBus.ExceptionInfo.Message"];

    [Test]
    public async Task Should_trim_properties_before_sending()
    {
        static SendOptions CreateOptions()
        {
            var options = new SendOptions();
            options.RouteToThisEndpoint();
            foreach (var header in HeadersToTrim)
            {
                options.SetHeader(header, new string('x', MaxPropertySize * 2));
            }

            return options;
        }

        var context = await Scenario.Define<Context>(c =>
            {
                c.LogLevel = LogLevel.Debug;
            })
            .WithEndpoint<Sender>(b => b.When(async session =>
            {
                var options = CreateOptions();
                options.RequireImmediateDispatch();
                await session.Send(new MyMessage(), options);

                await session.Send(new MyMessage(), CreateOptions());
            }))
            .Done(c => c.ReceivedCount == 2)
            .Run();

        Assert.That(context.ReceivedCount, Is.EqualTo(2), "Message was not received");
    }


    public class Context : ScenarioContext
    {
        public int ReceivedCount { get; set; }
    }

    public class Sender : EndpointConfigurationBuilder
    {
        public Sender() =>
            EndpointSetup<DefaultServer>(c => c.LimitMessageProcessingConcurrencyTo(1));

        public class MyMessageHandler(Context testContext) : IHandleMessages<MyMessage>
        {
            public Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                testContext.ReceivedCount++;
                return Task.CompletedTask;
            }
        }
    }

    public class MyMessage : ICommand;
}