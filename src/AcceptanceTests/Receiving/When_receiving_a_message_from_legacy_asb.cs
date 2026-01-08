namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Receiving;

using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Xml;
using NServiceBus.AcceptanceTesting;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

class When_receiving_a_message_from_legacy_asb
{
    [Test]
    public async Task Should_process_message_correctly()
    {
        var context = await Scenario.Define<Context>()
            .WithEndpoint<Endpoint>(b => b.When((session, c) =>
            {
                var sendOptions = new SendOptions();
                sendOptions.RouteToThisEndpoint();
                sendOptions.CustomizeNativeMessage(msg =>
                {
                    msg.ApplicationProperties["NServiceBus.Transport.Encoding"] = "wcf/byte-array";

                    var serializer = new DataContractSerializer(typeof(byte[]));
                    using var stream = new MemoryStream();
                    using var writer = XmlDictionaryWriter.CreateBinaryWriter(stream);
                    serializer.WriteObject(writer, msg.Body.ToArray());
                    writer.Flush();

                    msg.Body = new BinaryData(stream.ToArray());
                });
                return session.Send(new Message(), sendOptions);
            }))
            .Run();

        Assert.That(context.MessageReceived, Is.True);
    }

    public class Context : ScenarioContext
    {
        public bool MessageReceived { get; set; }
    }

    public class Endpoint : EndpointConfigurationBuilder
    {
        public Endpoint() => EndpointSetup<DefaultServer>();

        public class Handler(Context testContext) : IHandleMessages<Message>
        {
            public Task Handle(Message request, IMessageHandlerContext context)
            {
                testContext.MessageReceived = true;
                testContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class Message : IMessage;
}