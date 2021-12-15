namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests.Receiving
{
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
            await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When((session, c) =>
                {
                    var sendOptions = new SendOptions();
                    sendOptions.RouteToThisEndpoint();
                    sendOptions.CustomizeNativeMessage(msg =>
                    {
                        msg.ApplicationProperties["NServiceBus.Transport.Encoding"] = "wcf/byte-array";

                        var serializer = new DataContractSerializer(typeof(byte[]));
                        using (var stream = new MemoryStream())
                        {
                            using (var writer = XmlDictionaryWriter.CreateBinaryWriter(stream))
                            {
                                serializer.WriteObject(writer, msg.Body.ToArray());
                                writer.Flush();

                                msg.Body = new BinaryData(stream.ToArray());
                            }
                        }
                    });
                    return session.Send(new Message(), sendOptions);
                }))
                .Done(c => c.MessageRecieved)
                .Run();
        }

        public class Context : ScenarioContext
        {
            public bool MessageRecieved { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>();
            }

            public class Handler : IHandleMessages<Message>
            {
                Context testContext;

                public Handler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(Message request, IMessageHandlerContext context)
                {
                    testContext.MessageRecieved = true;

                    return Task.CompletedTask;
                }
            }
        }

        public class Message : IMessage { }
    }
}
