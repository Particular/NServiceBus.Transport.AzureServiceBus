namespace NServiceBus.Transport.AzureServiceBus.Tests.Receiving;

using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using NUnit.Framework;

[TestFixture]
public class SessionsEnabledMessagePumpTests
{
    [Test]
    public async Task Should_not_arm_circuit_breaker_when_lock_renewal_fails()
    {
        var fakeClient = new FakeServiceBusClient();
        await StartPumpWithConcurrency(fakeClient, 4);
        var processor = fakeClient.SessionProcessors["receiveAddress"];

        await processor.RaiseError(new ProcessErrorEventArgs(
            new ServiceBusException("lock lost", ServiceBusFailureReason.SessionLockLost),
            ServiceBusErrorSource.RenewLock, "namespace", "receiveAddress", "identifier", CancellationToken.None));

        Assert.That(processor.MaxConcurrentSessions, Is.EqualTo(4));
    }

    [Test]
    public async Task Should_arm_circuit_breaker_when_receive_fails()
    {
        var fakeClient = new FakeServiceBusClient();
        await StartPumpWithConcurrency(fakeClient, 4);
        var processor = fakeClient.SessionProcessors["receiveAddress"];

        await processor.RaiseError(new ProcessErrorEventArgs(
            new ServiceBusException("not found", ServiceBusFailureReason.MessagingEntityNotFound),
            ServiceBusErrorSource.Receive, "namespace", "receiveAddress", "identifier", CancellationToken.None));

        Assert.That(processor.MaxConcurrentSessions, Is.EqualTo(1));
    }

    static async Task<SessionsEnabledMessagePump> StartPumpWithConcurrency(FakeServiceBusClient fakeClient, int maxConcurrency)
    {
        var pump = new SessionsEnabledMessagePump(fakeClient, new AzureServiceBusTransport("connection-string", TopicTopology.Default), "receiveAddress",
            new ReceiveSettings("TestReceiver", new QueueAddress("receiveAddress"), false, false, "error"), (_, _, _) => { }, null);

        await pump.Initialize(new PushRuntimeSettings(maxConcurrency), (_, _) => Task.CompletedTask,
            (_, _) => Task.FromResult(ErrorHandleResult.Handled), CancellationToken.None);
        await pump.StartReceive();
        Assert.That(fakeClient.SessionProcessors["receiveAddress"].MaxConcurrentSessions, Is.EqualTo(maxConcurrency));
        return pump;
    }
}
