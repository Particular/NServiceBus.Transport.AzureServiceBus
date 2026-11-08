namespace NServiceBus.Transport.AzureServiceBus.Tests.Receiving;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using NServiceBus.Logging;
using NServiceBus.Testing;
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

    [TestCase(ServiceBusFailureReason.ServiceBusy, true)]
    [TestCase(ServiceBusFailureReason.ServiceTimeout, true)]
    [TestCase(ServiceBusFailureReason.MessageLockLost, false)]
    [TestCase(ServiceBusFailureReason.SessionLockLost, false)]
    public async Task Should_warn_only_for_transient_recoverability_failures(ServiceBusFailureReason reason, bool warns)
    {
        var logOutput = new StringWriter();
#pragma warning disable CS0618 // Type or member is obsolete - Test is testing logging
        using var logScope = LogManager.Use<TestingLoggerFactory>().BeginScope(logOutput, LogLevel.Warn);
#pragma warning restore CS0618 // Type or member is obsolete

        var fakeClient = new FakeServiceBusClient();
        var criticalErrors = 0;
        var pump = new SessionsEnabledMessagePump(fakeClient, new AzureServiceBusTransport("connection-string", TopicTopology.Default), "receiveAddress",
            new ReceiveSettings("TestReceiver", new QueueAddress("receiveAddress"), false, false, "error"), (_, _, _) => criticalErrors++, null);
        await pump.Initialize(new PushRuntimeSettings(1), (_, _) => Task.FromException(new InvalidOperationException()),
            (_, _) => Task.FromException<ErrorHandleResult>(new ServiceBusException("Sending to the error queue failed", reason)), CancellationToken.None);
        await pump.StartReceive();

        var receiver = new FakeSessionReceiver("session");
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: "SomeId", sessionId: "session", lockedUntil: DateTimeOffset.UtcNow.AddSeconds(60));

        await fakeClient.SessionProcessors["receiveAddress"].ProcessMessage(message, receiver);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(logOutput.ToString(), warns ? Does.Contain("due to a transient error") : Is.Empty);
            Assert.That(criticalErrors, Is.Zero);
            Assert.That(receiver.AbandonedMessages, Has.Exactly(1).Matches<ServiceBusReceivedMessage>(abandoned => abandoned.MessageId == "SomeId"));
        }
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