namespace NServiceBus.AcceptanceTests.Routing;

using System;
using AcceptanceTesting;
using EndpointTemplates;
using NUnit.Framework;

public class When_endpoint_is_send_only : NServiceBusAcceptanceTest
{
    [Test]
    public void Should_throw_exception()
    {
        var exception = Assert.ThrowsAsync<Exception>(async () =>
            await Scenario.Define<MyContext>()
                .WithEndpoint<SendOnlyEndpoint>()
                .Run());

        Assert.That(exception.ToString(), Does.Contain("Sessions cannot be enabled for send-only endpoints"));
    }

    public class MyContext : ScenarioContext;

    public class SendOnlyEndpoint : EndpointConfigurationBuilder
    {
        public SendOnlyEndpoint() =>
            EndpointSetup<DefaultServer>(config =>
            {
                config.SendOnly();
            });
    }
}